using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Xml.XPath;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using System.Threading.Tasks;
using SAM.API;
namespace SAM.Picker.Modern {
    public partial class MainWindow : Window {
        private Client _SteamClient;
        private List<GameInfo> _AllGames = new List<GameInfo>();
        private ObservableCollection<GameViewModel> _FilteredGames = new ObservableCollection<GameViewModel>();
        private bool _WantGames = true;
        private bool _WantMods = false;
        private bool _WantDemos = false;
        private bool _WantJunk = false;
        private DispatcherTimer _CallbackTimer;
        private ICollectionView _AchievementView;
        public MainWindow() {
            InitializeComponent();
            _SteamClient = new Client();
            try {
                _SteamClient.Initialize(0);
            } catch (Exception ex) {
                MessageBox.Show("Failed to initialize Steam: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }
            GamesList.ItemsSource = _FilteredGames;
            SearchBox.TextChanged += (s, e) => RefreshFilter();
            this.StateChanged += OnWindowStateChanged;
            _CallbackTimer = new DispatcherTimer();
            _CallbackTimer.Interval = TimeSpan.FromMilliseconds(100);
            _CallbackTimer.Tick += (s, e) => _SteamClient.RunCallbacks(false);
            _CallbackTimer.Start();
            LoadData();
        }
        internal class AchievementDefinition {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string IconNormal { get; set; }
            public string IconLocked { get; set; }
        }
        private void OnWindowStateChanged(object sender, EventArgs e) {
            if (MaximizeBtn == null) return;
            MaximizeBtn.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        }
        protected override void OnClosed(EventArgs e) {
            _CallbackTimer?.Stop();
            _SteamClient?.Dispose();
            SAM.API.Steam.Unload();
            base.OnClosed(e);
        }

        private async void LoadData() {
            await System.Threading.Tasks.Task.Run(() => FetchGames());
            Dispatcher.Invoke(() => RefreshFilter());
        }
        private void FetchGames() {
            byte[] bytes;
            using (WebClient downloader = new WebClient()) {
                try {
                    bytes = downloader.DownloadData(new Uri("https://gib.me/sam/games.xml"));
                } catch {
                    bytes = System.Text.Encoding.UTF8.GetBytes("<games><game type=\"normal\">480</game></games>");
                }
            }
            List<KeyValuePair<uint, string>> pairs = new List<KeyValuePair<uint, string>>();
            using (MemoryStream stream = new MemoryStream(bytes, false)) {
                XPathDocument document = new XPathDocument(stream);
                var navigator = document.CreateNavigator();
                var nodes = navigator.Select("/games/game");
                while (nodes.MoveNext()) {
                    string type = nodes.Current.GetAttribute("type", "");
                    if (string.IsNullOrEmpty(type)) type = "normal";
                    pairs.Add(new KeyValuePair<uint, string>((uint)nodes.Current.ValueAsLong, type));
                }
            }
            var fetchedGames = new List<GameInfo>();
            foreach (var kv in pairs) {
                if (_SteamClient.SteamApps008.IsSubscribedApp(kv.Key)) {
                    var name = _SteamClient.SteamApps001.GetAppData(kv.Key, "name") ?? $"App {kv.Key}";
                    fetchedGames.Add(new GameInfo {
                        Id = kv.Key,
                        Name = name,
                        Type = kv.Value,
                        ImageUrl = $"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{kv.Key}/capsule_231x87.jpg"
                    });
                }
            }
            Dispatcher.Invoke(() => {
                _AllGames.Clear();
                _AllGames.AddRange(fetchedGames);
                HomeLoadingOverlay.Visibility = Visibility.Collapsed;
                RefreshFilter();
            });
        }
        private void RefreshFilter() {
            if (SearchBox == null) return;
            var searchText = SearchBox.Text.ToLower(CultureInfo.InvariantCulture);
            if (ClearSearchBtn != null) ClearSearchBtn.Visibility = string.IsNullOrEmpty(searchText) ? Visibility.Collapsed : Visibility.Visible;
            var filtered = _AllGames.Where(g =>
                (string.IsNullOrEmpty(searchText) || g.Name.ToLower(CultureInfo.InvariantCulture).Contains(searchText)) &&
                ((g.Type == "normal" && _WantGames) ||
                 (g.Type == "mod" && _WantMods) ||
                 (g.Type == "demo" && _WantDemos) ||
                 (g.Type == "junk" && _WantJunk))
            ).OrderBy(g => g.Name).ToList();
            _FilteredGames.Clear();
            foreach (var g in filtered) {
                _FilteredGames.Add(new GameViewModel {
                    Id = g.Id,
                    Name = g.Name,
                    Image = new BitmapImage(new Uri(g.ImageUrl))
                });
            }
        }
        private ObservableCollection<AchievementViewModel> _Achievements = new ObservableCollection<AchievementViewModel>();
        private uint _SelectedGameId;
        private SAM.API.Callbacks.UserStatsReceived _UserStatsReceivedCallback;
        private void Game_Click(object sender, MouseButtonEventArgs e) {
            if (sender is FrameworkElement element && element.DataContext is GameViewModel game) {
                _SelectedGameId = game.Id;
                SelectedGameName.Text = game.Name;
                if (WindowTitleText != null) WindowTitleText.Text = $"{game.Name} - SAM Reborn 2026";
                HomeView.Visibility = Visibility.Collapsed;
                GameDetailsView.Visibility = Visibility.Visible;
                LoadGameData();
            }
        }
        private void LoadGameData() {
            _AchievementView = CollectionViewSource.GetDefaultView(_Achievements);
            AchievementList.ItemsSource = _AchievementView;
            AchievementFilter.SelectedIndex = 0;
            _Achievements.Clear();
            SelectedGameInfo.Text = "Loading achievements...";
            LoadingOverlay.Visibility = Visibility.Visible;
            DetailsStatus.Text = "Switching Steam context...";

            _CallbackTimer.Stop();
            try {
                _SteamClient?.Dispose();
                _SteamClient = null;
                SAM.API.Steam.Unload();
                
                _SteamClient = new Client();
                _SteamClient.Initialize(_SelectedGameId);
                
                _UserStatsReceivedCallback = _SteamClient.CreateAndRegisterCallback<SAM.API.Callbacks.UserStatsReceived>();
                _UserStatsReceivedCallback.OnRun += (p) => {
                    Dispatcher.Invoke(() => {
                        if (p.Result == 1) {
                            FetchAchievements();
                        } else {
                            DetailsStatus.Text = $"Steam error {p.Result} (UserStatsReceived).";
                            LoadingOverlay.Visibility = Visibility.Collapsed;
                        }
                    });
                };
            } catch (Exception ex) {
                MessageBox.Show("Failed to switch Steam context: " + ex.Message);
                LoadingOverlay.Visibility = Visibility.Collapsed;
                _CallbackTimer.Start();
                return;
            }
            _CallbackTimer.Start();

            var steamId = _SteamClient.SteamUser.GetSteamId();
            _SteamClient.SteamUserStats.RequestUserStats(steamId);
            _SteamClient.SteamUserStats.RequestGlobalAchievementPercentages();
            DetailsStatus.Text = "Requesting stats from Steam...";
        }
        private void FetchAchievements() {
            var definitions = new List<AchievementDefinition>();
            if (!LoadUserGameStatsSchema(definitions)) {
                DetailsStatus.Text = "Failed to load schema. Some info might be missing.";
            }
            _Achievements.Clear();
            foreach (var def in definitions) {
                if (_SteamClient.SteamUserStats.GetAchievementAndUnlockTime(def.Id, out bool isAchieved, out uint unlockTime)) {
                    _SteamClient.SteamUserStats.GetAchievementAchievedPercent(def.Id, out float globalPercent);
                    var avm = new AchievementViewModel {
                        Id = def.Id,
                        Name = def.Name,
                        Description = def.Description,
                        IsAchieved = isAchieved,
                        UnlockTime = isAchieved && unlockTime > 0 ? (DateTime?)DateTimeOffset.FromUnixTimeSeconds(unlockTime).LocalDateTime : null,
                        GlobalPercent = globalPercent,
                        IconUrl = $"https://cdn.steamstatic.com/steamcommunity/public/images/apps/{_SelectedGameId}/{(isAchieved ? def.IconNormal : def.IconLocked)}"
                    };
                    LoadIcon(avm);
                    _Achievements.Add(avm);
                }
            }
            DetailsStatus.Text = $"Loaded {_Achievements.Count} achievements.";
            SelectedGameInfo.Text = $"{_Achievements.Count} achievements available";
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
        private void AchievementFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (_AchievementView == null) return;
            
            var selectedItem = (ComboBoxItem)AchievementFilter.SelectedItem;
            if (selectedItem == null) return;

            string filter = selectedItem.Tag?.ToString();
            
            _AchievementView.Filter = (item) => {
                var avm = item as AchievementViewModel;
                if (avm == null) return true;

                if (filter == "locked") return !avm.IsAchieved;
                if (filter == "unlocked") return avm.IsAchieved;
                return true;
            };
            _AchievementView.Refresh();
        }

        private void Sort_Click(object sender, RoutedEventArgs e) {
            if (SortButton.ContextMenu != null) {
                SortButton.ContextMenu.PlacementTarget = SortButton;
                SortButton.ContextMenu.IsOpen = true;
            }
        }

        private void SortOption_Click(object sender, RoutedEventArgs e) {
            if (_AchievementView == null) return;
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;

            string tag = menuItem.Tag?.ToString();
            if (string.IsNullOrEmpty(tag)) return;

            _AchievementView.SortDescriptions.Clear();
            
            if (tag == "Name_Asc") {
                _AchievementView.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
            } else if (tag == "Name_Desc") {
                _AchievementView.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Descending));
            } else if (tag == "Rarity_Asc") {
                _AchievementView.SortDescriptions.Add(new SortDescription("GlobalPercent", ListSortDirection.Ascending));
            } else if (tag == "Rarity_Desc") {
                _AchievementView.SortDescriptions.Add(new SortDescription("GlobalPercent", ListSortDirection.Descending));
            }
            
            _AchievementView.Refresh();
        }

        private void BulkAction_Click(object sender, RoutedEventArgs e) {
            if (sender is Button btn && btn.ContextMenu != null) {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void UnlockAll_Click(object sender, RoutedEventArgs e) {
            foreach (var ach in _Achievements) {
                ach.IsAchieved = true;
            }
        }

        private void LockAll_Click(object sender, RoutedEventArgs e) {
            foreach (var ach in _Achievements) {
                ach.IsAchieved = false;
            }
        }
        private async void LoadIcon(AchievementViewModel vm) {
            try {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(vm.IconUrl);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                vm.Icon = bitmap;
            } catch {
            }
        }
        private bool LoadUserGameStatsSchema(List<AchievementDefinition> definitions) {
            try {
                string installPath = SAM.API.Steam.GetInstallPath();
                string path = Path.Combine(installPath, "appcache", "stats", $"UserGameStatsSchema_{_SelectedGameId}.bin");
                var kv = KeyValue.LoadAsBinary(path);
                if (kv == null) return false;
                var currentLanguage = _SteamClient.SteamApps008.GetCurrentGameLanguage() ?? "english";
                var stats = kv[_SelectedGameId.ToString()]["stats"];
                if (!stats.Valid || stats.Children == null) return false;
                foreach (var stat in stats.Children) {
                    var type = (SAM.API.Types.UserStatType)stat["type"].AsInteger(0);
                    if (type == SAM.API.Types.UserStatType.Achievements || type == SAM.API.Types.UserStatType.GroupAchievements) {
                        foreach (var bits in stat.Children.Where(b => b.Name.Equals("bits", StringComparison.OrdinalIgnoreCase))) {
                            if (bits.Children == null) continue;
                            foreach (var bit in bits.Children) {
                                definitions.Add(new AchievementDefinition {
                                    Id = bit["name"].AsString(""),
                                    Name = GetLocalizedString(bit["display"]["name"], currentLanguage, bit["name"].AsString("")),
                                    Description = GetLocalizedString(bit["display"]["desc"], currentLanguage, ""),
                                    IconNormal = bit["display"]["icon"].AsString(""),
                                    IconLocked = bit["display"]["icon_gray"].AsString("")
                                });
                            }
                        }
                    }
                }
                return true;
            } catch { return false; }
        }
        private string GetLocalizedString(KeyValue kv, string language, string defaultValue) {
            var name = kv[language].AsString("");
            if (!string.IsNullOrEmpty(name)) return name;
            if (language != "english") {
                name = kv["english"].AsString("");
                if (!string.IsNullOrEmpty(name)) return name;
            }
            return kv.AsString(defaultValue);
        }
        private void Back_Click(object sender, RoutedEventArgs e) {
            GameDetailsView.Visibility = Visibility.Collapsed;
            HomeView.Visibility = Visibility.Visible;
            if (WindowTitleText != null) WindowTitleText.Text = "SAM Reborn 2026";
            
            _CallbackTimer.Stop();
            _SteamClient?.Dispose();
            _SteamClient = null;
            SAM.API.Steam.Unload();
            _SteamClient = new Client();
            _SteamClient.Initialize(0);
            _CallbackTimer.Start();
        }
        private void RefreshGame_Click(object sender, RoutedEventArgs e) => LoadGameData();
        private void Store_Click(object sender, RoutedEventArgs e) {
            bool success = true;
            foreach (var ach in _Achievements) {
                if (!_SteamClient.SteamUserStats.SetAchievement(ach.Id, ach.IsAchieved)) {
                    success = false;
                }
            }
            if (success && _SteamClient.SteamUserStats.StoreStats()) {
                MessageBox.Show("Changes stored successfully!");
                LoadGameData();
            } else {
                MessageBox.Show("Failed to store changes.");
            }
        }
        private void ClearSearch_Click(object sender, RoutedEventArgs e) {
            SearchBox.Text = string.Empty;
        }
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Maximize_Click(object sender, RoutedEventArgs e) {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Refresh_Click(object sender, RoutedEventArgs e) {
            _AllGames.Clear();
            HomeLoadingOverlay.Visibility = Visibility.Visible;
            LoadData();
        }
        private void Filter_Click(object sender, RoutedEventArgs e) {
            if (sender is Button btn && btn.ContextMenu != null) {
                FilterGames.IsChecked = _WantGames;
                FilterMods.IsChecked = _WantMods;
                FilterDemos.IsChecked = _WantDemos;
                FilterJunk.IsChecked = _WantJunk;
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }
        private void FilterOption_Click(object sender, RoutedEventArgs e) {
            if (sender is MenuItem item) {
                if (item == FilterGames) _WantGames = item.IsChecked;
                if (item == FilterMods) _WantMods = item.IsChecked;
                if (item == FilterDemos) _WantDemos = item.IsChecked;
                if (item == FilterJunk) _WantJunk = item.IsChecked;
                RefreshFilter();
            }
        }
        public static readonly DependencyProperty IsTimerModeProperty = DependencyProperty.Register(
            "IsTimerMode", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

        public bool IsTimerMode {
            get { return (bool)GetValue(IsTimerModeProperty); }
            set { SetValue(IsTimerModeProperty, value); }
        }

        private void EnableTimer_Click(object sender, RoutedEventArgs e) {
            IsTimerMode = !IsTimerMode;
            var visibility = IsTimerMode ? Visibility.Collapsed : Visibility.Visible;
            if (BulkActionsButton != null) BulkActionsButton.Visibility = visibility;

            if (RefreshButton != null) RefreshButton.Visibility = visibility;
            if (SaveButton != null) SaveButton.Visibility = visibility;
            if (AchievementFilter != null) AchievementFilter.Visibility = visibility;
            if (StartTimerButton != null) StartTimerButton.Visibility = IsTimerMode ? Visibility.Visible : Visibility.Collapsed;
            if (RandomTimerButton != null) RandomTimerButton.Visibility = IsTimerMode ? Visibility.Visible : Visibility.Collapsed;

            // Update button text
            if (EnableTimerText != null) {
                EnableTimerText.Text = IsTimerMode ? "Disable timer" : "Timer Mode";
            }

            if (IsTimerMode) {
                if (_AchievementView != null) {
                    _AchievementView.SortDescriptions.Clear();
                    _AchievementView.Refresh();
                }

                foreach (ComboBoxItem item in AchievementFilter.Items) {
                    if (item.Tag?.ToString() == "locked") {
                        AchievementFilter.SelectedItem = item;
                        break;
                    }
                }
                
                RefreshFilter();
                if (DetailsStatus != null) DetailsStatus.Text = "Drag and drop the achievements, set the delay in minutes, and click 'Start Timer'.";
            } else {
                // Reset timer state when disabling
                _IsTimerActive = false;
                _IsTimerPaused = false;
                UpdateTimerButtonState();
                if (DetailsStatus != null) DetailsStatus.Text = "Ready";

                foreach (ComboBoxItem item in AchievementFilter.Items) {
                    if (item.Tag?.ToString() == "all") {
                        AchievementFilter.SelectedItem = item;
                        break;
                    }
                }
                RefreshFilter();
            }
        }

        private void RandomTimerButton_Click(object sender, RoutedEventArgs e) {
            if (RandomTimerPopup != null) {
                RandomTimerPopup.IsOpen = true;
            }
        }

        private void ApplyRandomTimer_Click(object sender, RoutedEventArgs e) {
            if (int.TryParse(RandMinInput.Text, out int min) && int.TryParse(RandMaxInput.Text, out int max)) {
                if (min > max) {
                    int temp = min;
                    min = max;
                    max = temp;
                }

                var random = new Random();
                foreach (var ach in _Achievements) {
                    if (!ach.IsAchieved) {
                        ach.TimerMinutes = random.Next(min, max + 1).ToString();
                    }
                }
                
                if (RandomTimerPopup != null) {
                    RandomTimerPopup.IsOpen = false;
                }
            } else {
                MessageBox.Show("Please enter valid numeric values for Min and Max minutes.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool _IsTimerActive = false;
        private bool _IsTimerPaused = false;

        private void UpdateTimerButtonState() {
            if (!_IsTimerActive) {
                StartTimerText.Text = "Start timer";
                StartTimerIcon.Data = (Geometry)FindResource("Icon.Play");
            } else if (_IsTimerPaused) {
                StartTimerText.Text = "Resume timer";
                StartTimerIcon.Data = (Geometry)FindResource("Icon.Play");
            } else {
                StartTimerText.Text = "Pause timer";
                StartTimerIcon.Data = (Geometry)FindResource("Icon.Pause");
            }
        }

        private async void StartTimer_Click(object sender, RoutedEventArgs e) {
            if (_IsTimerActive) {
                _IsTimerPaused = !_IsTimerPaused;
                UpdateTimerButtonState();
                return;
            }

            try {
                var achievements = _Achievements.ToList();
                
                if (!achievements.Any()) {
                     MessageBox.Show("No achievements in the collection.", "Timer Info", MessageBoxButton.OK, MessageBoxImage.Warning);
                     return;
                }

                _IsTimerActive = true;
                _IsTimerPaused = false;
                UpdateTimerButtonState();

                string originalStatus = DetailsStatus.Text;
                int processedCount = 0;

                foreach (var ach in achievements) {
                    // Check if timer was stopped
                    if (!_IsTimerActive) break;

                    if (ach.IsAchieved) continue;

                    bool hasTimer = double.TryParse(ach.TimerMinutes, out double minutes) && minutes > 0;
                    
                    if (!hasTimer) continue;

                    var totalSeconds = (int)(minutes * 60);
                    while (totalSeconds > 0) {
                        // Check if timer was stopped
                        if (!_IsTimerActive) break;

                        while (_IsTimerPaused) {
                            DetailsStatus.Text = "Timer Paused";
                            await Task.Delay(200);
                            if (!_IsTimerActive) break;
                        }
                        if (!_IsTimerActive) break;

                        TimeSpan remaining = TimeSpan.FromSeconds(totalSeconds);
                        DetailsStatus.Text = $"Unlocking '{ach.Name}' in {remaining:mm\\:ss}...";
                        await Task.Delay(1000);
                        totalSeconds--;
                    }
                    
                    while (_IsTimerPaused) {
                        DetailsStatus.Text = "Timer Paused";
                        await Task.Delay(200);
                        if (!_IsTimerActive) break;
                    }

                    if (!_IsTimerActive) break;

                    ach.IsAchieved = true;
                    processedCount++;
                    DetailsStatus.Text = $"Unlocked '{ach.Name}'!";
                    if (_SteamClient.SteamUserStats.SetAchievement(ach.Id, true)) {
                        _SteamClient.SteamUserStats.StoreStats();
                    }
                    await Task.Delay(1000); 
                }
                
                if (processedCount == 0) {
                     MessageBox.Show("No locked achievements with a valid timer were found.", "Timer Info", MessageBoxButton.OK, MessageBoxImage.Information);
                } else {
                     MessageBox.Show($"Timer sequence complete. {processedCount} achievements unlocked.", "Timer Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                
                DetailsStatus.Text = originalStatus;

            } catch (Exception ex) {
                MessageBox.Show($"Error running timer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            } finally {
                _IsTimerActive = false;
                _IsTimerPaused = false;
                UpdateTimerButtonState();
            }
        }

        private Point _startPoint;

        private void AchievementList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (!IsTimerMode) return;
            _startPoint = e.GetPosition(null);
        }

        private void AchievementList_PreviewMouseMove(object sender, MouseEventArgs e) {
            if (!IsTimerMode || e.LeftButton != MouseButtonState.Pressed) return;

            Point mousePos = e.GetPosition(null);
            Vector diff = _startPoint - mousePos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance) {
                
                ListView listView = sender as ListView;
                ListViewItem listViewItem = FindAncestor<ListViewItem>((DependencyObject)e.OriginalSource);

                if (listViewItem == null) return;

                 AchievementViewModel contact = (AchievementViewModel)listView.ItemContainerGenerator.ItemFromContainer(listViewItem);
                                
                DataObject dragData = new DataObject("myFormat", contact);
                DragDrop.DoDragDrop(listViewItem, dragData, DragDropEffects.Move);
            }
        }

        private void AchievementList_Drop(object sender, DragEventArgs e) {
            if (!IsTimerMode) return;

            if (e.Data.GetDataPresent("myFormat")) {
                AchievementViewModel source = e.Data.GetData("myFormat") as AchievementViewModel;
                AchievementViewModel target = ((FrameworkElement)e.OriginalSource).DataContext as AchievementViewModel;

                if (source != null && target != null && source != target) {
                    int oldIndex = _Achievements.IndexOf(source);
                    int newIndex = _Achievements.IndexOf(target);

                    if (oldIndex != -1 && newIndex != -1) {
                        _Achievements.Move(oldIndex, newIndex);
                    }
                }
            }
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject {
            do {
                if (current is T) {
                    return (T)current;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }
    }
    public class GameInfo {
        public uint Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string ImageUrl { get; set; }
    }
    public class GameViewModel {
        public uint Id { get; set; }
        public string Name { get; set; }
        public System.Windows.Media.ImageSource Image { get; set; }
    }
    public class AchievementViewModel : INotifyPropertyChanged {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public float GlobalPercent { get; set; }
        public string IconUrl { get; set; }
        public System.Windows.Media.ImageSource Icon { get; set; }
        private bool _IsAchieved;
        public bool IsAchieved {
            get => _IsAchieved;
            set {
                if (_IsAchieved != value) {
                    _IsAchieved = value;
                    OnPropertyChanged(nameof(IsAchieved));
                }
            }
        }
        private string _TimerMinutes = "10";
        public string TimerMinutes {
            get => _TimerMinutes;
            set {
                if (_TimerMinutes != value) {
                    _TimerMinutes = value;
                    OnPropertyChanged(nameof(TimerMinutes));
                }
            }
        }
        public DateTime? UnlockTime { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
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
        private System.Windows.Threading.DispatcherTimer _CallbackTimer;
        private ICollectionView _AchievementView;
        private ListSortDirection? _currentSortDirection = null;
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
            GameList.ItemsSource = _FilteredGames;
            SearchBox.TextChanged += (s, e) => RefreshFilter();
            this.StateChanged += OnWindowStateChanged;
            _CallbackTimer = new System.Windows.Threading.DispatcherTimer();
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
            if (sender is FrameworkElement fe && fe.DataContext is GameViewModel vm) {
                _SelectedGameId = vm.Id;
                SelectedGameName.Text = vm.Name;
                HomeView.Visibility = Visibility.Collapsed;
                GameDetailsView.Visibility = Visibility.Visible;
                LoadGameData();
            }
        }
        private void LoadGameData() {
            _AchievementView = CollectionViewSource.GetDefaultView(_Achievements);
            AchievementList.ItemsSource = _AchievementView;
            AchievementFilter.SelectedIndex = 0;
            _currentSortDirection = null;
            if (SortIcon != null) SortIcon.Data = (Geometry)FindResource("Icon.SortAz");
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
                    var avm = new AchievementViewModel {
                        Id = def.Id,
                        Name = def.Name,
                        Description = def.Description,
                        IsAchieved = isAchieved,
                        UnlockTime = isAchieved && unlockTime > 0 ? (DateTime?)DateTimeOffset.FromUnixTimeSeconds(unlockTime).LocalDateTime : null,
                        IconUrl = $"https://cdn.steamstatic.com/steamcommunity/public/images/apps/{_SelectedGameId}/{(isAchieved ? def.IconNormal : def.IconLocked)}"
                    };
                    // Load icon asynchronously
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
            if (_AchievementView == null) return;

            _AchievementView.SortDescriptions.Clear();

            if (_currentSortDirection == null) {
                _currentSortDirection = ListSortDirection.Ascending;
                SortIcon.Data = (Geometry)FindResource("Icon.SortAz");
            } else if (_currentSortDirection == ListSortDirection.Ascending) {
                _currentSortDirection = ListSortDirection.Descending;
                SortIcon.Data = (Geometry)FindResource("Icon.SortZa");
            } else {
                _currentSortDirection = null;
                SortIcon.Data = (Geometry)FindResource("Icon.SortAz");
                _AchievementView.Refresh();
                return;
            }

            _AchievementView.SortDescriptions.Add(new SortDescription("Name", _currentSortDirection.Value));
            _AchievementView.Refresh();
        }

        private void BulkActions_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (BulkActions == null || BulkActions.SelectedIndex <= 0) return;

            var selected = (ComboBoxItem)BulkActions.SelectedItem;
            if (selected == null) return;

            string action = selected.Tag?.ToString();
            bool targetState = action == "unlock_all";

            foreach (var ach in _Achievements) {
                ach.IsAchieved = targetState;
            }

            // Reset back to "Bulk Actions" label
            BulkActions.SelectedIndex = 0;
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
                // Ignore icon load failures
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
            
            // Restore Picker context (AppID 0)
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
        public DateTime? UnlockTime { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
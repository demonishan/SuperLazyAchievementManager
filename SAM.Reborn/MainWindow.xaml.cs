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
        DisplayAlert("Failed to initialize Steam: " + ex.Message, true);
        HomeLoadingOverlay.Visibility = Visibility.Collapsed;
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
    private void DisplayAlert(string message, bool isError) {
      var color = isError ? new SolidColorBrush(Color.FromRgb(232, 17, 35)) : new SolidColorBrush(Color.FromRgb(0, 122, 204));
      SharedStatusText.Text = message;
      if (SharedStatusText.Parent is Border b) b.Background = color;
    }
    internal class AchievementDefinition {
      public string Id { get; set; }
      public string Name { get; set; }
      public string Description { get; set; }
      public string IconNormal { get; set; }
      public string IconLocked { get; set; }
      public int Permission { get; set; }
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
      Environment.Exit(0);
    }
    private async void LoadData() {
      await System.Threading.Tasks.Task.Run(() => FetchGames());
      Dispatcher.Invoke(() => RefreshFilter());
    }
    private void FetchGames() {
      try {
        byte[] bytes;
        using (WebClient downloader = new WebClient()) {
          try { bytes = downloader.DownloadData(new Uri("https://gib.me/sam/games.xml")); }
          catch { bytes = System.Text.Encoding.UTF8.GetBytes("<games><game type=\"normal\">480</game></games>"); }
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
          try {
            if (_SteamClient.SteamApps008.IsSubscribedApp(kv.Key)) {
              var name = _SteamClient.SteamApps001.GetAppData(kv.Key, "name") ?? $"App {kv.Key}";
              fetchedGames.Add(new GameInfo { Id = kv.Key, Name = name, Type = kv.Value, ImageUrl = $"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{kv.Key}/capsule_231x87.jpg" });
            }
          } catch { /* Skip games that cause errors */ }
        }
        Dispatcher.Invoke(() => {
          _AllGames.Clear();
          _AllGames.AddRange(fetchedGames);
          SharedStatusText.Text = $"{fetchedGames.Count} games detected! Your wallet sends its regards.";
          HomeLoadingOverlay.Visibility = Visibility.Collapsed;
          RefreshFilter();
          StartImageCaching();
        });
      } catch (Exception ex) {
        Dispatcher.Invoke(() => {
          DisplayAlert($"Error fetching games: {ex.Message}", true);
          HomeLoadingOverlay.Visibility = Visibility.Collapsed;
        });
      }
    }
    private void StartImageCaching() {
      var games = _AllGames.ToList();
      Task.Run(async () => {
        try {
          var cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");
          if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);
          using (var client = new WebClient()) {
            foreach (var game in games) {
              if (game.CachedIcon != null) continue;
              var path = Path.Combine(cacheDir, $"{game.Id}.jpg");
              bool needsDownload = !File.Exists(path) || new FileInfo(path).Length == 0;
              if (needsDownload) {
                try {
                  var data = await client.DownloadDataTaskAsync(new Uri(game.ImageUrl));
                  if (data.Length > 0) File.WriteAllBytes(path, data);
                } catch { }
                await Task.Delay(50);
              }
              if (File.Exists(path) && new FileInfo(path).Length > 0) {
                await Dispatcher.InvokeAsync(() => {
                  try {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(path, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    game.CachedIcon = bitmap;
                    var vm = _FilteredGames.FirstOrDefault(x => x.Id == game.Id);
                    if (vm != null) vm.Image = bitmap;
                  } catch { }
                }, DispatcherPriority.Background);
              }
            }
          }
        } catch { }
      });
    }
    private void RefreshFilter() {
      if (SearchBox == null) return;
      var searchText = SearchBox.Text.ToLower(CultureInfo.InvariantCulture);
      if (ClearSearchBtn != null) ClearSearchBtn.Visibility = string.IsNullOrEmpty(searchText) ? Visibility.Collapsed : Visibility.Visible;
      var filtered = _AllGames.Where(g => (string.IsNullOrEmpty(searchText) || g.Name.ToLower(CultureInfo.InvariantCulture).Contains(searchText)) && ((g.Type == "normal" && _WantGames) || (g.Type == "mod" && _WantMods) || (g.Type == "demo" && _WantDemos) || (g.Type == "junk" && _WantJunk))).OrderBy(g => g.Name).ToList();
      _FilteredGames.Clear();
      foreach (var g in filtered)
        _FilteredGames.Add(new GameViewModel { Id = g.Id, Name = g.Name, Image = g.CachedIcon });
    }
    private ObservableCollection<AchievementViewModel> _Achievements = new ObservableCollection<AchievementViewModel>();
    private uint _SelectedGameId;
    private SAM.API.UserStatsReceived _UserStatsReceivedCallback;
    private void Game_Click(object sender, MouseButtonEventArgs e) {
      if (sender is FrameworkElement element && element.DataContext is GameViewModel game) {
        _SelectedGameId = game.Id;
        SelectedGameName.Text = game.Name;
        if (WindowTitleText != null) WindowTitleText.Text = $"{game.Name} - SAM Reborn 2026";
        Title = $"{game.Name} - SAM Reborn 2026";
        HomeView.Visibility = Visibility.Collapsed;
        GameDetailsView.Visibility = Visibility.Visible;
        SharedStatusText.Text = $"Checking if you actually beat {game.Name}";
        SharedStatusText.Text = $"Checking if you actually beat {game.Name}";
        LoadGameData(true);
      }
    }
    private void ClearAchievementSearch_Click(object sender, RoutedEventArgs e) => AchievementSearchBox.Text = string.Empty;
    private void AchievementSearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshAchievementFilter();
    private void RefreshAchievementFilter() {
      if (_AchievementView == null) return;
      var selectedItem = (ComboBoxItem)AchievementFilter.SelectedItem;
      string filter = selectedItem?.Tag?.ToString();
      string searchText = AchievementSearchBox.Text.ToLower(CultureInfo.InvariantCulture);
      if (ClearAchievementSearchBtn != null) ClearAchievementSearchBtn.Visibility = string.IsNullOrEmpty(searchText) ? Visibility.Collapsed : Visibility.Visible;
      _AchievementView.Filter = (item) => {
        var avm = item as AchievementViewModel;
        if (avm == null) return true;
        bool matchesFilter = true;
        if (filter == "locked") matchesFilter = !avm.IsAchieved;
        if (filter == "unlocked") matchesFilter = avm.IsAchieved;
        bool matchesSearch = string.IsNullOrEmpty(searchText) || avm.Name.ToLower(CultureInfo.InvariantCulture).Contains(searchText) || avm.Description.ToLower(CultureInfo.InvariantCulture).Contains(searchText);
        return matchesFilter && matchesSearch;
      };
      _AchievementView.Refresh();
    }
    private void LoadGameData(bool resetFilters = true) {
      _AchievementView = CollectionViewSource.GetDefaultView(_Achievements);
      AchievementList.ItemsSource = _AchievementView;
      if (resetFilters) {
        AchievementFilter.SelectedIndex = 0;
        if (SortFilter != null) SortFilter.SelectedIndex = 0;
        if (AchievementSearchBox != null) AchievementSearchBox.Text = string.Empty;
      } else {
        RefreshAchievementFilter();
        ApplySort();
      }
      _Achievements.Clear();
      _Achievements.Clear();
      LoadingOverlay.Visibility = Visibility.Visible;
      SharedStatusText.Text = "Switching Steam context...";
      _CallbackTimer.Stop();
      try {
        _SteamClient?.Dispose();
        _SteamClient = null;
        SAM.API.Steam.Unload();
        _SteamClient = new Client();
        _SteamClient.Initialize(_SelectedGameId);
        _UserStatsReceivedCallback = _SteamClient.CreateAndRegisterCallback<SAM.API.UserStatsReceived>();
        _UserStatsReceivedCallback.OnRun += (p) => {
          Dispatcher.Invoke(() => {
            if (p.Result == 1) FetchAchievements();
            else { SharedStatusText.Text = $"Steam error {p.Result} (UserStatsReceived)."; LoadingOverlay.Visibility = Visibility.Collapsed; }
          });
        };
      } catch (Exception ex) {
        DisplayAlert("Failed to switch Steam context: " + ex.Message, true);
        LoadingOverlay.Visibility = Visibility.Collapsed;
        _CallbackTimer.Start();
        return;
      }
      _CallbackTimer.Start();
      var steamId = _SteamClient.SteamUser.GetSteamId();
      _SteamClient.SteamUserStats.RequestUserStats(steamId);
      _SteamClient.SteamUserStats.RequestGlobalAchievementPercentages();
      SharedStatusText.Text = "Requesting stats from Steam...";
    }
    private void FetchAchievements() {
      var definitions = new List<AchievementDefinition>();
      if (!LoadUserGameStatsSchema(definitions)) SharedStatusText.Text = "Failed to load schema. Some info might be missing.";
      bool anyProtected = definitions.Any(x => x.Permission > 0);
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
            IconUrl = $"https://cdn.steamstatic.com/steamcommunity/public/images/apps/{_SelectedGameId}/{(isAchieved ? def.IconNormal : def.IconLocked)}",
            Permission = anyProtected ? 3 : def.Permission
          };
          LoadIcon(avm);
          _Achievements.Add(avm);
        }
      }
      SharedStatusText.Text = $"Loaded {_Achievements.Count} achievements.";
      int unlocked = _Achievements.Count(x => x.IsAchieved);
      int total = _Achievements.Count;
      int locked = total - unlocked;
      if (anyProtected) {
        AreModificationsAllowed = false;
        SharedStatusText.Text = "These achievements are protected. Can't modify them through SAM Reborn.";
        DisplayAlert(SharedStatusText.Text, true);
        UpdateProtectionState();
      }
      else {
        AreModificationsAllowed = true;
        UpdateProtectionState();
        if (unlocked == total && total > 0) SharedStatusText.Text = $"Unlocked all {total}. Please, go touch some grass.";
        else if (unlocked == 0 && total > 0) SharedStatusText.Text = $"0 down, {total} to go. Do you even play this game?";
        else SharedStatusText.Text = $"{unlocked} out of {total} down, {locked} to go. Back to the grind.";
      }
      LoadingOverlay.Visibility = Visibility.Collapsed;
    }
    private void AchievementFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) { RefreshAchievementFilter(); UpdateTimerMetadata(); }
    private void SortFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplySort();
    private void ApplySort() {
      if (_AchievementView == null) return;
      if (SortFilter.SelectedItem is ComboBoxItem item) {
        _AchievementView.SortDescriptions.Clear();
        string tag = item.Tag?.ToString();
        if (tag == "Name_Asc") _AchievementView.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
        else if (tag == "Name_Desc") _AchievementView.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Descending));
        else if (tag == "Rarity_Asc") _AchievementView.SortDescriptions.Add(new SortDescription("GlobalPercent", ListSortDirection.Ascending));
        else if (tag == "Rarity_Desc") _AchievementView.SortDescriptions.Add(new SortDescription("GlobalPercent", ListSortDirection.Descending));
        _AchievementView.Refresh();
        UpdateTimerMetadata();
      }
    }
    private void BulkAction_Click(object sender, RoutedEventArgs e) {
      if (sender is Button btn && btn.ContextMenu != null) {
        btn.ContextMenu.PlacementTarget = btn;
        btn.ContextMenu.IsOpen = true;
      }
    }
    private void UnlockAll_Click(object sender, RoutedEventArgs e) {
      foreach (var ach in _Achievements) ach.IsAchieved = true;
    }
    private void Links_Click(object sender, RoutedEventArgs e) {
      if (LinksButton.ContextMenu != null) {
        LinksButton.ContextMenu.PlacementTarget = LinksButton;
        LinksButton.ContextMenu.IsOpen = true;
      }
    }
    private void LinkOption_Click(object sender, RoutedEventArgs e) {
      if (sender is MenuItem item) {
        try {
          var steamId = _SteamClient.SteamUser.GetSteamId();
          string url = "";
          if (item.Tag?.ToString() == "completionist") url = $"https://completionist.me/steam/profile/{steamId}/app/{_SelectedGameId}";
          else if (item.Tag?.ToString() == "steam_achievements") url = $"https://steamcommunity.com/profiles/{steamId}/stats/{_SelectedGameId}/achievements/";
          else if (item.Tag?.ToString() == "global_stats") url = $"https://steamcommunity.com/stats/{_SelectedGameId}/achievements/";
          else if (item.Tag?.ToString() == "steamdb") url = $"https://steamdb.info/app/{_SelectedGameId}/";
          else if (item.Tag?.ToString() == "store") url = $"https://store.steampowered.com/app/{_SelectedGameId}/";
          else if (item.Tag?.ToString() == "guides") url = $"https://steamcommunity.com/app/{_SelectedGameId}/guides/";
          if (!string.IsNullOrEmpty(url)) Process.Start(url);
        } catch { }
      }
    }
    private void ToggleSearch_Click(object sender, RoutedEventArgs e) {
      if (AchievementSearchRow.Visibility == Visibility.Visible) AchievementSearchRow.Visibility = Visibility.Collapsed;
      else AchievementSearchRow.Visibility = Visibility.Visible;
    }
    private void LockAll_Click(object sender, RoutedEventArgs e) {
      foreach (var ach in _Achievements) ach.IsAchieved = false;
    }
    private async void LoadIcon(AchievementViewModel vm) {
      try {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(vm.IconUrl);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        vm.Icon = bitmap;
      } catch { }
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
              foreach (var bit in bits.Children) definitions.Add(new AchievementDefinition { Id = bit["name"].AsString(""), Name = GetLocalizedString(bit["display"]["name"], currentLanguage, bit["name"].AsString("")), Description = GetLocalizedString(bit["display"]["desc"], currentLanguage, ""), IconNormal = bit["display"]["icon"].AsString(""), IconLocked = bit["display"]["icon_gray"].AsString(""), Permission = bit["permission"].AsInteger(0) });
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
      AreModificationsAllowed = true;
      UpdateProtectionState();
      DisplayAlert("Select a game to manage achievements.", false);
      if (_IsTimerActive || IsTimerMode) {
        _IsTimerActive = false;
        _IsTimerPaused = false;
        IsTimerRunning = false;
        IsTimerMode = false;
        if (EnableTimerText != null) EnableTimerText.Text = "Timer Mode";
        if (BulkActionsButton != null) BulkActionsButton.Visibility = Visibility.Visible;
        if (RefreshButton != null) RefreshButton.Visibility = Visibility.Visible;
        if (SaveButton != null) SaveButton.Visibility = Visibility.Visible;
        if (AchievementFilter != null) AchievementFilter.Visibility = Visibility.Visible;
        if (StartTimerButton != null) StartTimerButton.Visibility = Visibility.Collapsed;
        if (RandomTimerButton != null) RandomTimerButton.Visibility = Visibility.Collapsed;
        if (EnableTimerButton != null) EnableTimerButton.Visibility = Visibility.Visible;
        if (SortFilter != null) SortFilter.Visibility = Visibility.Visible;
        foreach (ComboBoxItem item in AchievementFilter.Items) {
          if (item.Tag?.ToString() == "all") { 
            AchievementFilter.SelectedItem = item; 
            break; 
          }
        }
      }
      GameDetailsView.Visibility = Visibility.Collapsed;
      HomeView.Visibility = Visibility.Visible;
      if (WindowTitleText != null) WindowTitleText.Text = "SAM Reborn 2026";
      Title = "SAM Reborn 2026";
      _CallbackTimer.Stop();
      _SteamClient?.Dispose();
      _SteamClient = null;
      SAM.API.Steam.Unload();
      _SteamClient = new Client();
      _SteamClient.Initialize(0);
      _CallbackTimer.Start();
      LoadData(); // Retain filters on Save/Refresh
    }
    private void RefreshGame_Click(object sender, RoutedEventArgs e) => LoadGameData(false);
    private void Store_Click(object sender, RoutedEventArgs e) {
      bool success = true;
      foreach (var ach in _Achievements) {
        if (!_SteamClient.SteamUserStats.SetAchievement(ach.Id, ach.IsAchieved)) success = false;
      }
      if (success && _SteamClient.SteamUserStats.StoreStats()) {
        DisplayAlert("Changes stored successfully!", false); 
        LoadGameData(false); 
      }
      else DisplayAlert("Failed to store changes.", true);
    }
    private void ClearSearch_Click(object sender, RoutedEventArgs e) { SearchBox.Text = string.Empty; }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Refresh_Click(object sender, RoutedEventArgs e) { 
      _AllGames.Clear(); 
      HomeLoadingOverlay.Visibility = Visibility.Visible; 
      LoadData(); 
    }
    private void Filter_Click(object sender, RoutedEventArgs e) {
      if (sender is Button btn && btn.ContextMenu != null) {
        FilterGames.IsChecked = _WantGames; FilterMods.IsChecked = _WantMods; FilterDemos.IsChecked = _WantDemos; FilterJunk.IsChecked = _WantJunk;
        btn.ContextMenu.PlacementTarget = btn; btn.ContextMenu.IsOpen = true;
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
    public static readonly DependencyProperty IsTimerModeProperty = DependencyProperty.Register("IsTimerMode", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));
    public bool IsTimerMode {
      get {
        return (bool)GetValue(IsTimerModeProperty); 
      } 
      set { 
        SetValue(IsTimerModeProperty, value); 
      } 
    }
    public static readonly DependencyProperty IsTimerRunningProperty = DependencyProperty.Register("IsTimerRunning", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));
    public bool IsTimerRunning {
      get {
        return (bool)GetValue(IsTimerRunningProperty); 
      } 
      set { 
        SetValue(IsTimerRunningProperty, value); 
      } 
    }
    public static readonly DependencyProperty AreModificationsAllowedProperty = DependencyProperty.Register("AreModificationsAllowed", typeof(bool), typeof(MainWindow), new PropertyMetadata(true));
    public bool AreModificationsAllowed {
      get { return (bool)GetValue(AreModificationsAllowedProperty); }
      set { SetValue(AreModificationsAllowedProperty, value); }
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
      if (EnableTimerText != null) EnableTimerText.Text = IsTimerMode ? "Normal Mode" : "Timer Mode";
      if (IsTimerMode) {
        if (_AchievementView != null) { _AchievementView.SortDescriptions.Clear(); _AchievementView.Refresh(); }
        foreach (ComboBoxItem item in AchievementFilter.Items) {
          if (item.Tag?.ToString() == "locked") { AchievementFilter.SelectedItem = item; break; }
        }
        UpdateTimerMetadata();
        RefreshFilter();
        if (SharedStatusText != null) SharedStatusText.Text = "Drag and drop the achievements, set the delay in minutes, and 'Start Timer'.";
      } else {
        _IsTimerActive = false; _IsTimerPaused = false; UpdateTimerButtonState();
        if (SharedStatusText != null) SharedStatusText.Text = "Ready";
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
      if (RandomTimerPopup != null) RandomTimerPopup.IsOpen = true; 
    }
    private void ApplyRandomTimer_Click(object sender, RoutedEventArgs e) {
      if (int.TryParse(RandMinInput.Text, out int min) && int.TryParse(RandMaxInput.Text, out int max)) {
        if (min > max) { int temp = min; min = max; max = temp; }
        var random = new Random();
        foreach (var ach in _Achievements) { if (!ach.IsAchieved) ach.TimerMinutes = random.Next(min, max + 1).ToString(); }
        if (RandomTimerPopup != null) RandomTimerPopup.IsOpen = false;
      } else DisplayAlert("Please enter valid numeric values for Min and Max minutes.", true);
    }
    private bool _IsTimerActive = false;
    private bool _IsTimerPaused = false;
    private void UpdateTimerButtonState() {
      if (!_IsTimerActive) {
        StartTimerText.Text = "Start Timer";
          StartTimerIcon.Data = (Geometry)FindResource("Icon.Play");
      } else if (_IsTimerPaused) {
        StartTimerText.Text = "Resume Timer";
        StartTimerIcon.Data = (Geometry)FindResource("Icon.Play");
      } else {
        StartTimerText.Text = "Pause Timer";
        StartTimerIcon.Data = (Geometry)FindResource("Icon.Pause");
      }
    }
    private void UpdateProtectionState() {
      if (!AreModificationsAllowed) {
        if (BulkActionsButton != null) BulkActionsButton.Visibility = Visibility.Collapsed;
        if (EnableTimerButton != null) EnableTimerButton.Visibility = Visibility.Collapsed;
        if (SaveButton != null) SaveButton.Visibility = Visibility.Collapsed;
      } else {
        var vis = IsTimerMode ? Visibility.Collapsed : Visibility.Visible;
        if (BulkActionsButton != null) BulkActionsButton.Visibility = vis;
        if (EnableTimerButton != null) EnableTimerButton.Visibility = Visibility.Visible;
        if (SaveButton != null) SaveButton.Visibility = vis;
      }
    }
    private async void StartTimer_Click(object sender, RoutedEventArgs e) {
      if (_IsTimerActive) {
        _IsTimerPaused = !_IsTimerPaused;
        UpdateTimerButtonState();
        return;
      }
      var achievements = _Achievements.ToList();
      if (!achievements.Any()) {
        DisplayAlert("No achievements in the collection.", true);
        return;
      }
      try {
        SetTimerUIState(true);
        int processedCount = await RunTimerSequence(achievements);
        if (_IsTimerActive) {
          if (processedCount == 0) DisplayAlert("No locked achievements with a valid timer were found.", false);
          else DisplayAlert($"Timer sequence complete. {processedCount} achievements unlocked.", false);
        }
      } catch (Exception ex) {
        DisplayAlert($"Error running timer: {ex.Message}", true); 
      }
      finally { SetTimerUIState(false); }
    }
    private void SetTimerUIState(bool active) {
      _IsTimerActive = active; _IsTimerPaused = false; IsTimerRunning = active; UpdateTimerButtonState();
      var visibility = active ? Visibility.Collapsed : Visibility.Visible;
      if (SortFilter != null) SortFilter.Visibility = visibility;
      if (EnableTimerButton != null) EnableTimerButton.Visibility = visibility;
      if (RandomTimerButton != null) RandomTimerButton.Visibility = active ? Visibility.Collapsed : (IsTimerMode ? Visibility.Visible : Visibility.Collapsed);
    }
    private async Task<int> RunTimerSequence(List<AchievementViewModel> achievements) {
      string originalStatus = SharedStatusText.Text;
      int count = 0;
      foreach (var ach in achievements) {
        if (!_IsTimerActive) break;
        if (ach.IsAchieved) continue;
        if (!double.TryParse(ach.TimerMinutes, out double minutes) || minutes <= 0) continue;
        int totalSeconds = (int)(minutes * 60);
        while (totalSeconds > 0) {
          if (!_IsTimerActive) break;
          while (_IsTimerPaused) {
            SharedStatusText.Text = "Timer Paused";
            await Task.Delay(200);
            if (!_IsTimerActive) break;
          }
          if (!_IsTimerActive) break;
          SharedStatusText.Text = $"Unlocking '{ach.Name}' in {TimeSpan.FromSeconds(totalSeconds):mm\\:ss}...";
          await Task.Delay(1000);
          totalSeconds--;
        }
        if (!_IsTimerActive) break;
        ach.IsAchieved = true; count++;
        SharedStatusText.Text = $"Unlocked '{ach.Name}'!";
        if (_SteamClient.SteamUserStats.SetAchievement(ach.Id, true)) _SteamClient.SteamUserStats.StoreStats();
        await Task.Delay(1000);
      }
      SharedStatusText.Text = originalStatus;
      return count;
    }
    private Point _startPoint;
    private void AchievementList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
      if (!IsTimerMode || IsTimerRunning) return;
      _startPoint = e.GetPosition(null);
    }
    private void AchievementList_PreviewMouseMove(object sender, MouseEventArgs e) {
      if (!IsTimerMode || IsTimerRunning || e.LeftButton != MouseButtonState.Pressed) return;
      Point mousePos = e.GetPosition(null);
      Vector diff = _startPoint - mousePos;
      if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance) {
        ListView listView = sender as ListView;
        ListViewItem listViewItem = FindAncestor<ListViewItem>((DependencyObject)e.OriginalSource);
        if (listViewItem == null) return;
        AchievementViewModel contact = (AchievementViewModel)listView.ItemContainerGenerator.ItemFromContainer(listViewItem);
        DataObject dragData = new DataObject("myFormat", contact);
        DragDrop.DoDragDrop(listViewItem, dragData, DragDropEffects.Move);
      }
    }
    private void AchievementList_Drop(object sender, DragEventArgs e) {
      if (!IsTimerMode || IsTimerRunning) return;
      if (_AchievementView.SortDescriptions.Count > 0) {
        var sd = _AchievementView.SortDescriptions[0];
        var sorted = _Achievements.ToList();
        if (sd.PropertyName == "Name") sorted = (sd.Direction == ListSortDirection.Ascending) ? sorted.OrderBy(x => x.Name).ToList() : sorted.OrderByDescending(x => x.Name).ToList();
        else if (sd.PropertyName == "GlobalPercent") sorted = (sd.Direction == ListSortDirection.Ascending) ? sorted.OrderBy(x => x.GlobalPercent).ToList() : sorted.OrderByDescending(x => x.GlobalPercent).ToList();
        _Achievements.Clear();
        foreach (var item in sorted) _Achievements.Add(item);
        _AchievementView.SortDescriptions.Clear();
        if (SortFilter != null) SortFilter.SelectedIndex = 0;
      }
      if (e.Data.GetDataPresent("myFormat")) {
        AchievementViewModel source = e.Data.GetData("myFormat") as AchievementViewModel;
        AchievementViewModel target = ((FrameworkElement)e.OriginalSource).DataContext as AchievementViewModel;
        if (source != null && target != null && source != target) {
          int oldIndex = _Achievements.IndexOf(source);
          int newIndex = _Achievements.IndexOf(target);
          if (oldIndex != -1 && newIndex != -1) {
            _Achievements.Move(oldIndex, newIndex);
            UpdateTimerMetadata();
          }
        }
      }
    }
    private void TimerDelay_TextChanged(object sender, TextChangedEventArgs e) => UpdateTimerMetadata();
    private void UpdateTimerMetadata() {
      if (_AchievementView == null) return;
      int i = 1;
      DateTime projection = DateTime.Now;
      foreach (AchievementViewModel ach in _AchievementView) {
        ach.Index = i++;
        if (!ach.IsAchieved && double.TryParse(ach.TimerMinutes, out double minutes) && minutes > 0) {
          projection = projection.AddMinutes(minutes);
          ach.ETA = $"{projection:t}";
          projection = projection.AddSeconds(1);
        } else {
          ach.ETA = "";
        }
      }
    }
    private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject {
      do {
        if (current is T) return (T)current;
        current = VisualTreeHelper.GetParent(current);
      } while (current != null);
      return null;
    }
  }
  public class GameInfo {
    public uint Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string ImageUrl { get; set; }
    public System.Windows.Media.ImageSource CachedIcon { get; set; }
  }
  public class GameViewModel : INotifyPropertyChanged {
    public uint Id { get; set; }
    public string Name { get; set; }
    private System.Windows.Media.ImageSource _Image;
    public System.Windows.Media.ImageSource Image {
      get => _Image;
      set {
        if (_Image != value) {
          _Image = value;
          OnPropertyChanged(nameof(Image));
        }
      }
    }
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
    private int _Index;
    public int Index {
      get => _Index;
      set {
        if (_Index != value) {
          _Index = value;
          OnPropertyChanged(nameof(Index));
        }
      }
    }
    private string _ETA;
    public string ETA {
      get => _ETA;
      set {
        if (_ETA != value) {
          _ETA = value;
          OnPropertyChanged(nameof(ETA));
        }
      }
    }
    public int Permission { get; set; }
    public bool IsProtected => Permission > 0;
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  }
}
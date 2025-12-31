using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.XPath;
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
    private string _AppVersion = "";
    public MainWindow() {
      InitializeComponent();
      try {
        var entry = System.Reflection.Assembly.GetEntryAssembly();
        string ver = null;
        if (entry != null) {
          var fvi = FileVersionInfo.GetVersionInfo(entry.Location);
          ver = fvi.FileVersion ?? fvi.ProductVersion ?? entry.GetName().Version?.ToString();
        }
        if (string.IsNullOrEmpty(ver)) ver = "0.0.0";
        var parts = ver.Split('.');
        _AppVersion = parts.Length >= 3 ? $"{parts[0]}.{parts[1]}.{parts[2]}" : ver;
        if (WindowTitleText != null) WindowTitleText.Text = $"Super Lazy Achievement Manager v{_AppVersion}";
        Title = $"SLAM v{_AppVersion}";
      } catch { _AppVersion = ""; }
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
      _CallbackTimer.Tick += (s, e) => {
        try { _SteamClient?.RunCallbacks(false); }
        catch (Exception ex) { SAM.Picker.Modern.App.LogCrash(ex, "CallbackTimer_RunCallbacks"); }
      };
      _CallbackTimer.Start();
      AppDomain.CurrentDomain.UnhandledException += (s, e) => {
        if (e.ExceptionObject is Exception ex) SAM.Picker.Modern.App.LogCrash(ex, "AppDomain_UnhandledException");
      };
      TaskScheduler.UnobservedTaskException += (s, e) => {
        SAM.Picker.Modern.App.LogCrash(e.Exception, "TaskScheduler_UnobservedTaskException");
        e.SetObserved();
      };
      if (Application.Current != null) Application.Current.DispatcherUnhandledException += (s, e) => {
        SAM.Picker.Modern.App.LogCrash(e.Exception, "DispatcherUnhandledException");
        e.Handled = true;
      };
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
      public int IsHidden { get; set; }
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
        try {
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
        } catch {
          pairs.Clear();
          pairs.Add(new KeyValuePair<uint, string>(480, "normal"));
        }
        var fetchedGames = new List<GameInfo>();
        foreach (var kv in pairs) {
          try {
            if (_SteamClient.SteamApps008.IsSubscribedApp(kv.Key)) {
              var name = _SteamClient.SteamApps001.GetAppData(kv.Key, "name") ?? $"App {kv.Key}";
              fetchedGames.Add(new GameInfo { Id = kv.Key, Name = name, Type = kv.Value, ImageUrl = $"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{kv.Key}/header.jpg" });
            }
          } catch { }
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
      var games = _AllGames.OrderBy(g => g.Name).ToList();
      Task.Run(async () => {
        try {
          var cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "games");
          if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);
          using (var client = new WebClient()) {
            foreach (var game in games) {
              await ProcessGameImage(game, client, cacheDir);
            }
          }
        } catch { }
      });
    }
    private async Task ProcessGameImage(GameInfo game, WebClient client, string cacheDir) {
      if (game.CachedIcon != null) return;
      var path = Path.Combine(cacheDir, $"{game.Id}.jpg");
      bool needsDownload = !File.Exists(path) || new FileInfo(path).Length == 0;
      if (needsDownload) {
        try {
          var data = await client.DownloadDataTaskAsync(new Uri(game.ImageUrl));
          if (data.Length > 0) File.WriteAllBytes(path, data);
        } catch {
          if (!File.Exists(path) || new FileInfo(path).Length == 0) {
            try {
              var json = await client.DownloadStringTaskAsync($"https://store.steampowered.com/api/appdetails?appids={game.Id}");
              var match = System.Text.RegularExpressions.Regex.Match(json, "\"header_image\"\\s*:\\s*\"(.*?)\"");
              if (match.Success) {
                var url = match.Groups[1].Value.Replace("\\/", "/");
                var data = await client.DownloadDataTaskAsync(new Uri(url));
                if (data.Length > 0) File.WriteAllBytes(path, data);
              }
            } catch { }
          }
        }
        await Task.Delay(25);
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
      } else {
        await Dispatcher.InvokeAsync(() => {
          try {
              var bitmap = new BitmapImage(new Uri("pack://application:,,,/SLAM;component/Resources/image-not-found.png"));
              game.CachedIcon = bitmap;
              var vm = _FilteredGames.FirstOrDefault(x => x.Id == game.Id);
              if (vm != null) vm.Image = bitmap;
          } catch { }
        }, DispatcherPriority.Background);
      }
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
      try {
        if (sender is FrameworkElement element && element.DataContext is GameViewModel game) {
          _SelectedGameId = game.Id;
          SelectedGameName.Text = game.Name;
          if (WindowTitleText != null) WindowTitleText.Text = $"{game.Name} - Super Lazy Achievement Manager v{_AppVersion}";
          Title = $"{game.Name} - SLAM v{_AppVersion}";
          if (FilterAllBtn != null) FilterAllBtn.IsChecked = true;
          if (FilterLockedBtn != null) FilterLockedBtn.IsChecked = false;
          if (FilterUnlockedBtn != null) FilterUnlockedBtn.IsChecked = false;
          if (RevealHiddenBtn != null) RevealHiddenBtn.IsChecked = false;
          if (SortFilter != null) SortFilter.SelectedIndex = 0;
          if (AchievementSearchBox != null) AchievementSearchBox.Text = string.Empty;
          if (_AchievementView != null) {
            _AchievementView.Refresh();
            ApplySort();
          }
          HomeView.Visibility = Visibility.Collapsed;
          GameDetailsView.Visibility = Visibility.Visible;
          SharedStatusText.Text = $"Checking if you actually beat {game.Name}";
          SharedStatusText.Text = $"Checking if you actually beat {game.Name}";
          LoadGameData(true);
        }
      } catch (Exception ex) {
        DisplayAlert($"Error switching game: {ex.Message}", true);
        SAM.Picker.Modern.App.LogCrash(ex, "Game_Click");
        HomeView.Visibility = Visibility.Visible;
        GameDetailsView.Visibility = Visibility.Collapsed;
      }
    }
    private void ClearAchievementSearch_Click(object sender, RoutedEventArgs e) => AchievementSearchBox.Text = string.Empty;
    private void AchievementSearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshAchievementFilter();
    private void RevealHidden_Click(object sender, RoutedEventArgs e) {
      if (RevealHiddenBtn == null) return;
      bool showHidden = RevealHiddenBtn.IsChecked == true;
      foreach (var avm in _Achievements) {
        if (avm.IsHiddenLocked) {
          if (showHidden) {
            avm.Name = avm.OriginalName;
            avm.Description = avm.OriginalDescription;
            avm.Icon = avm.RealIcon ?? avm.Icon;
          } else {
            avm.Name = "Hidden Achievement";
            avm.Description = "Details for this achievement Will be revealed once unlocked";
            try {
              avm.Icon = new BitmapImage(new Uri("pack://application:,,,/SLAM;component/Resources/hidden.png"));
            } catch { }
          }
        }
      }
    }
    private void FilterToggle_Click(object sender, RoutedEventArgs e) {
      if (sender == FilterAllBtn) {
        if (FilterAllBtn.IsChecked == true) {
          FilterLockedBtn.IsChecked = false;
          FilterUnlockedBtn.IsChecked = false;
        }
        else FilterAllBtn.IsChecked = true;
      }
      else if (sender == FilterLockedBtn) {
        if (FilterLockedBtn.IsChecked == true) {
          FilterAllBtn.IsChecked = false;
          FilterUnlockedBtn.IsChecked = false;
        }
        else FilterLockedBtn.IsChecked = true;
      }
      else if (sender == FilterUnlockedBtn) {
        if (FilterUnlockedBtn.IsChecked == true) {
          FilterAllBtn.IsChecked = false;
          FilterLockedBtn.IsChecked = false;
        }
        else FilterUnlockedBtn.IsChecked = true;
      }
      RefreshAchievementFilter();
      UpdateTimerMetadata();
    }
    private void RefreshAchievementFilter() {
      if (_AchievementView == null) return;
      bool showAll = FilterAllBtn.IsChecked == true;
      bool showLocked = FilterLockedBtn.IsChecked == true;
      bool showUnlocked = FilterUnlockedBtn.IsChecked == true;
      string searchText = AchievementSearchBox.Text.ToLower(CultureInfo.InvariantCulture);
      if (ClearAchievementSearchBtn != null) ClearAchievementSearchBtn.Visibility = string.IsNullOrEmpty(searchText) ? Visibility.Collapsed : Visibility.Visible;
      _AchievementView.Filter = (item) => {
        var avm = item as AchievementViewModel;
        if (avm == null) return true;
        bool matchesFilter = showAll || (showLocked && !avm.IsAchieved) || (showUnlocked && avm.IsAchieved);
        bool matchesSearch = string.IsNullOrEmpty(searchText) || avm.Name.ToLower(CultureInfo.InvariantCulture).Contains(searchText) || avm.Description.ToLower(CultureInfo.InvariantCulture).Contains(searchText);
        return matchesFilter && matchesSearch;
      };
      _AchievementView.Refresh();
    }
    private void LoadGameData(bool resetFilters = true) {
      try {
        IsTimerMode = false;
        _AchievementView = CollectionViewSource.GetDefaultView(_Achievements);
        AchievementList.ItemsSource = _AchievementView;
        if (resetFilters) {
          if (FilterAllBtn != null) FilterAllBtn.IsChecked = true;
          if (FilterLockedBtn != null) FilterLockedBtn.IsChecked = false;
          if (FilterUnlockedBtn != null) FilterUnlockedBtn.IsChecked = false;
          if (RevealHiddenBtn != null) {
            RevealHiddenBtn.IsChecked = false;
            RevealHiddenBtn.Visibility = Visibility.Collapsed;
          }
          if (SortFilter != null) SortFilter.SelectedIndex = 0;
          if (AchievementSearchBox != null) AchievementSearchBox.Text = string.Empty;
          ApplySort();
        } else {
          RefreshAchievementFilter();
          ApplySort();
        }
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
              if (p.Result == 1) {
                try { FetchAchievements(); }
                catch (Exception ex) {
                  DisplayAlert("Error fetching achievements: " + ex.Message, true);
                  SAM.Picker.Modern.App.LogCrash(ex, "LoadGameData_FetchAchievements");
                  LoadingOverlay.Visibility = Visibility.Collapsed;
                }
              }
              else {
                SharedStatusText.Text = $"Steam error {p.Result} (UserStatsReceived).";
                LoadingOverlay.Visibility = Visibility.Collapsed;
              }
            });
          };
        } catch (Exception ex) {
          DisplayAlert("Failed to switch Steam context: " + ex.Message, true);
          SAM.Picker.Modern.App.LogCrash(ex, "LoadGameData_SteamSwitch");
          LoadingOverlay.Visibility = Visibility.Collapsed;
          _CallbackTimer.Start();
          return;
        }
        _CallbackTimer.Start();
        var steamId = _SteamClient.SteamUser.GetSteamId();
        _SteamClient.SteamUserStats.RequestUserStats(steamId);
        _SteamClient.SteamUserStats.RequestGlobalAchievementPercentages();
        SharedStatusText.Text = "Requesting stats from Steam...";
      } catch (Exception ex) {
        DisplayAlert($"Critical error loading game data: {ex.Message}", true);
        SAM.Picker.Modern.App.LogCrash(ex, "LoadGameData_Critical");
        LoadingOverlay.Visibility = Visibility.Collapsed;
        _CallbackTimer.Start();
      }
    }
    private void FetchAchievements() {
      var definitions = new List<AchievementDefinition>();
      if (!LoadUserGameStatsSchema(definitions)) SharedStatusText.Text = "Failed to load schema. Some info might be missing.";
      bool anyProtected = definitions.Any(x => x.Permission > 0);
      _Achievements.Clear();
      foreach (var def in definitions) {
        if (_SteamClient.SteamUserStats.GetAchievementAndUnlockTime(def.Id, out bool isAchieved, out uint unlockTime)) {
          _SteamClient.SteamUserStats.GetAchievementAchievedPercent(def.Id, out float globalPercent);
          string iconUrl = $"https://cdn.steamstatic.com/steamcommunity/public/images/apps/{_SelectedGameId}/{(isAchieved ? def.IconNormal : def.IconLocked)}";
          string name = def.Name;
          string description = def.Description;
          bool isHiddenLocked = def.IsHidden == 1 && !isAchieved;
          string displayedIconUrl = iconUrl;
          if (isHiddenLocked) {
            bool showHidden = RevealHiddenBtn != null && RevealHiddenBtn.IsChecked == true;
            if (!showHidden) {
              name = "Hidden Achievement";
              description = "Details for this achievement Will be revealed once unlocked";
              displayedIconUrl = "pack://application:,,,/SLAM;component/Resources/hidden.png";
            }
          }
          var avm = new AchievementViewModel {
            Id = def.Id,
            Name = name,
            Description = description,
            OriginalName = def.Name,
            OriginalDescription = def.Description,
            RealIconUrl = iconUrl,
            IsHiddenLocked = isHiddenLocked,
            IsAchieved = isAchieved,
            UnlockTime = isAchieved && unlockTime > 0 ? (DateTime?)DateTimeOffset.FromUnixTimeSeconds(unlockTime).LocalDateTime : null,
            GlobalPercent = globalPercent,
            IconUrl = displayedIconUrl,
            Permission = anyProtected ? 3 : def.Permission,
            IsHidden = def.IsHidden == 1
          };
          _Achievements.Add(avm);
        }
      }
      bool hasHiddenLocked = _Achievements.Any(x => x.IsHiddenLocked);
      if (RevealHiddenBtn != null) RevealHiddenBtn.Visibility = hasHiddenLocked ? Visibility.Visible : Visibility.Collapsed;
      SharedStatusText.Text = $"Loaded {_Achievements.Count} achievements.";
      int unlocked = _Achievements.Count(x => x.IsAchieved);
      int total = _Achievements.Count;
      int locked = total - unlocked;
      if (AchievementProgressBar != null) AchievementProgressBar.Visibility = Visibility.Visible;
      UpdateProgressBar();
      if (anyProtected) {
        AreModificationsAllowed = false;
        SharedStatusText.Text = "These achievements are protected. Can't modify them through SLAM.";
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
      StartAchievementImageCaching(_Achievements.ToList());
      LoadingOverlay.Visibility = Visibility.Collapsed;
    }
    private void SortFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplySort();
    private void ApplySort() {
      if (_AchievementView == null) return;
      if (SortFilter.SelectedItem is ComboBoxItem item) {
        string tag = item.Tag?.ToString();
        var lcv = _AchievementView as ListCollectionView;
        if (tag?.StartsWith("Date_") == true) {
          if (lcv != null) lcv.CustomSort = new AchievementDateSorter(tag == "Date_Desc");
        } else {
          if (lcv != null) lcv.CustomSort = null;
          _AchievementView.SortDescriptions.Clear();
          ApplySortByTag(tag);
        }
        _AchievementView.Refresh();
        UpdateTimerMetadata();
      }
    }
    private void ApplySortByTag(string tag) {
      if (tag == null) return;
      var sortMap = new Dictionary<string, (string property, ListSortDirection direction, bool addNameSecondary)> {
        { "Name_Asc", ("Name", ListSortDirection.Ascending, false) },
        { "Name_Desc", ("Name", ListSortDirection.Descending, false) },
        { "Rarity_Asc", ("GlobalPercent", ListSortDirection.Ascending, true) },
        { "Rarity_Desc", ("GlobalPercent", ListSortDirection.Descending, true) }
      };
      if (sortMap.TryGetValue(tag, out var config)) {
        _AchievementView.SortDescriptions.Add(new SortDescription(config.property, config.direction));
        if (config.addNameSecondary) _AchievementView.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
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
          else if (item.Tag?.ToString() == "steamhunters") url = $"https://steamhunters.com/id/{steamId}/apps/{_SelectedGameId}/achievements";
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
    private void StartAchievementImageCaching(List<AchievementViewModel> achievements) {
      Task.Run(async () => {
        try {
          var cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "achievements");
          if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);
          using (var client = new WebClient()) {
            foreach (var ach in achievements) {
              string urlToLoad = ach.RealIconUrl;
              if (string.IsNullOrEmpty(urlToLoad) || urlToLoad.StartsWith("pack://")) {
                if (ach.IconUrl.StartsWith("pack://")) {
                  await Dispatcher.InvokeAsync(() => {
                    try {
                      ach.Icon = new BitmapImage(new Uri(ach.IconUrl));
                    } catch { }
                  });
                }
                continue;
              }
              var filename = System.IO.Path.GetFileName(new Uri(urlToLoad).LocalPath);
              var path = Path.Combine(cacheDir, filename);
              bool needsDownload = !File.Exists(path) || new FileInfo(path).Length == 0;
              if (needsDownload) {
                try {
                  var data = await client.DownloadDataTaskAsync(new Uri(urlToLoad));
                  if (data.Length > 0) File.WriteAllBytes(path, data);
                } catch { }
                await Task.Delay(20);
              }
              if (File.Exists(path) && new FileInfo(path).Length > 0) {
                await Dispatcher.InvokeAsync(() => {
                  try {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(path, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    ach.RealIcon = bitmap;
                    if (!ach.IsHiddenLocked || (RevealHiddenBtn != null && RevealHiddenBtn.IsChecked == true)) {
                      ach.Icon = bitmap;
                    } else if (ach.IsHiddenLocked && ach.Icon == null && ach.IconUrl.StartsWith("pack://")) {
                      try {
                        ach.Icon = new BitmapImage(new Uri(ach.IconUrl));
                      } catch { }
                    }
                  } catch { }
                }, DispatcherPriority.Background);
              } else {
                await Dispatcher.InvokeAsync(() => {
                  ach.IsBroken = true;
                }, DispatcherPriority.Background);
              }
            }
          }
        } catch { }
      });
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
              foreach (var bit in bits.Children) definitions.Add(new AchievementDefinition {
                Id = bit["name"].AsString(""),
                Name = GetLocalizedString(bit["display"]["name"], currentLanguage, bit["name"].AsString("")),
                Description = GetLocalizedString(bit["display"]["desc"], currentLanguage, ""),
                IconNormal = bit["display"]["icon"].AsString(""),
                IconLocked = bit["display"]["icon_gray"].AsString(""),
                Permission = bit["permission"].AsInteger(0),
                IsHidden = bit["display"]["hidden"].AsInteger(0)
              });
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
      try {
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
          if (FilterButtonsPanel != null) FilterButtonsPanel.Visibility = Visibility.Visible;
          if (FilterLockedBtn != null) FilterLockedBtn.Visibility = Visibility.Visible;
          if (FilterUnlockedBtn != null) FilterUnlockedBtn.Visibility = Visibility.Visible;
          if (StartTimerButton != null) StartTimerButton.Visibility = Visibility.Collapsed;
          if (RandomTimerButton != null) RandomTimerButton.Visibility = Visibility.Collapsed;
          if (EnableTimerButton != null) EnableTimerButton.Visibility = Visibility.Visible;
          if (SortFilter != null) SortFilter.Visibility = Visibility.Visible;
          if (FilterAllBtn != null) FilterAllBtn.IsChecked = true;
          if (FilterLockedBtn != null) FilterLockedBtn.IsChecked = false;
          if (FilterUnlockedBtn != null) FilterUnlockedBtn.IsChecked = false;
          if (AchievementProgressBar != null) AchievementProgressBar.Visibility = Visibility.Collapsed;
        }
        GameDetailsView.Visibility = Visibility.Collapsed;
        HomeView.Visibility = Visibility.Visible;
        if (WindowTitleText != null) WindowTitleText.Text = $"Super Lazy Achievement Manager v{_AppVersion}";
        Title = $"SLAM v{_AppVersion}";
        _CallbackTimer.Stop();
        try {
          _SteamClient?.Dispose();
          _SteamClient = null;
          SAM.API.Steam.Unload();
          _SteamClient = new Client();
          _SteamClient.Initialize(0);
        } catch (Exception ex) {
          DisplayAlert($"Failed to reinitialize Steam on back: {ex.Message}", true);
          SAM.Picker.Modern.App.LogCrash(ex, "Back_Click_SteamReinit");
          _SteamClient = null;
          _CallbackTimer.Start();
          return;
        }
        _CallbackTimer.Start();
        LoadData();
      } catch (Exception ex) {
        DisplayAlert($"Error returning to game list: {ex.Message}", true);
        SAM.Picker.Modern.App.LogCrash(ex, "Back_Click");
      }
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
    public static readonly DependencyProperty IsTimerModeProperty = DependencyProperty.Register("IsTimerMode", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));
    public bool IsTimerMode {
      get { return (bool)GetValue(IsTimerModeProperty); }
      set {
        SetValue(IsTimerModeProperty, value);
        UpdateTimerUIState();
      }
    }
    private void UpdateTimerUIState() {
      var visibility = IsTimerMode ? Visibility.Collapsed : Visibility.Visible;
      if (BulkActionsButton != null) BulkActionsButton.Visibility = visibility;
      if (AchievementSearchRow != null && IsTimerMode) AchievementSearchRow.Visibility = Visibility.Collapsed;
      if (RefreshButton != null) RefreshButton.Visibility = visibility;
      if (SaveButton != null) SaveButton.Visibility = visibility;
      if (FilterButtonsPanel != null) FilterButtonsPanel.Visibility = visibility;
      if (FilterAllBtn != null) FilterAllBtn.Visibility = visibility;
      bool hasHidden = _Achievements != null && _Achievements.Any(x => x.IsHiddenLocked);
      if (RevealHiddenBtn != null) RevealHiddenBtn.Visibility = hasHidden ? Visibility.Visible : Visibility.Collapsed;
      if (StartTimerButton != null) StartTimerButton.Visibility = IsTimerMode ? Visibility.Visible : Visibility.Collapsed;
      if (RandomTimerButton != null) RandomTimerButton.Visibility = IsTimerMode ? Visibility.Visible : Visibility.Collapsed;
      if (EnableTimerText != null) EnableTimerText.Text = IsTimerMode ? "Normal Mode" : "Timer Mode";
    }
    public static readonly DependencyProperty IsTimerRunningProperty = DependencyProperty.Register("IsTimerRunning", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));
    public bool IsTimerRunning {
      get { return (bool)GetValue(IsTimerRunningProperty); }
      set { SetValue(IsTimerRunningProperty, value); }
    }
    public static readonly DependencyProperty AreModificationsAllowedProperty = DependencyProperty.Register("AreModificationsAllowed", typeof(bool), typeof(MainWindow), new PropertyMetadata(true));
    public bool AreModificationsAllowed {
      get { return (bool)GetValue(AreModificationsAllowedProperty); }
      set { SetValue(AreModificationsAllowedProperty, value); }
    }
    private void EnableTimer_Click(object sender, RoutedEventArgs e) {
      IsTimerMode = !IsTimerMode;
      if (IsTimerMode) {
        if (_AchievementView != null) _AchievementView.Refresh();
        if (FilterAllBtn != null) FilterAllBtn.IsChecked = false;
        if (FilterLockedBtn != null) FilterLockedBtn.IsChecked = true;
        if (FilterUnlockedBtn != null) FilterUnlockedBtn.IsChecked = false;
        UpdateTimerMetadata();
        RefreshAchievementFilter();
        if (SharedStatusText != null) SharedStatusText.Text = "Drag and drop the achievements, set the delay in minutes, and 'Start Timer'.";
      } else {
        _IsTimerActive = false;
        _IsTimerPaused = false;
        UpdateTimerButtonState();
        if (SharedStatusText != null) SharedStatusText.Text = "Ready";
        if (FilterAllBtn != null) FilterAllBtn.IsChecked = true;
        if (FilterLockedBtn != null) FilterLockedBtn.IsChecked = false;
        if (FilterUnlockedBtn != null) FilterUnlockedBtn.IsChecked = false;
        RefreshAchievementFilter();
      }
    }
    private void RandomTimerButton_Click(object sender, RoutedEventArgs e) {
      if (RandomTimerPopup != null) RandomTimerPopup.IsOpen = true;
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
          if (!ach.IsAchieved) ach.TimerMinutes = random.Next(min, max + 1).ToString();
        }
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
      var achievements = _AchievementView.Cast<AchievementViewModel>().ToList();
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
      _IsTimerActive = active;
      _IsTimerPaused = false;
      IsTimerRunning = active;
      UpdateTimerButtonState();
      var visibility = active ? Visibility.Collapsed : Visibility.Visible;
      if (SortFilter != null) SortFilter.Visibility = visibility;
      if (EnableTimerButton != null) EnableTimerButton.Visibility = visibility;
      if (RandomTimerButton != null) RandomTimerButton.Visibility = active ? Visibility.Collapsed : (IsTimerMode ? Visibility.Visible : Visibility.Collapsed);
    }
    private void UpdateProgressBar() {
      if (AchievementProgressBar == null) return;
      int unlocked = _Achievements.Count(x => x.IsAchieved);
      int total = _Achievements.Count;
      AchievementProgressBar.Value = total > 0 ? (double)unlocked / total * 100 : 0;
    }
    private async Task<int> RunTimerSequence(List<AchievementViewModel> achievements) {
      string originalStatus = SharedStatusText.Text;
      int count = 0;
      foreach (var ach in achievements) {
        if (!_IsTimerActive) break;
        if (ach.IsAchieved) continue;
        if (!double.TryParse(ach.TimerMinutes, out double minutes) || minutes < 0) continue;
        if (minutes == 0) {
          if (!_IsTimerActive) break;
          ach.IsAchieved = true;
          count++;
          SharedStatusText.Text = $"Unlocked '{ach.Name}'!";
          if (_SteamClient.SteamUserStats.SetAchievement(ach.Id, true)) _SteamClient.SteamUserStats.StoreStats();
          _AchievementView.Refresh();
          UpdateTimerMetadata();
          UpdateProgressBar();
          await Task.Delay(500);
          continue;
        }
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
        ach.IsAchieved = true;
        count++;
        SharedStatusText.Text = $"Unlocked '{ach.Name}'!";
        if (_SteamClient.SteamUserStats.SetAchievement(ach.Id, true)) _SteamClient.SteamUserStats.StoreStats();
        _AchievementView.Refresh();
        UpdateTimerMetadata();
        UpdateProgressBar();
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
      var lcv = _AchievementView as ListCollectionView;
      bool isSorted = _AchievementView.SortDescriptions.Count > 0 || (lcv != null && lcv.CustomSort != null);
      if (isSorted) {
        var sorted = _AchievementView.Cast<AchievementViewModel>().ToList();
        _Achievements.Clear();
        foreach (var item in sorted) _Achievements.Add(item);
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
        } else if (!ach.IsAchieved && double.TryParse(ach.TimerMinutes, out minutes) && minutes == 0) {
          ach.ETA = $"{projection:t}";
        } else ach.ETA = "";
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
    private string _Name;
    public string Name {
      get => _Name;
      set {
        if (_Name != value) {
          _Name = value;
          OnPropertyChanged(nameof(Name));
        }
      }
    }
    private string _Description;
    public string Description {
      get => _Description;
      set {
        if (_Description != value) {
          _Description = value;
          OnPropertyChanged(nameof(Description));
        }
      }
    }
    public string OriginalName { get; set; }
    public string OriginalDescription { get; set; }
    public float GlobalPercent { get; set; }
    public string IconUrl { get; set; }
    public string RealIconUrl { get; set; }
    public System.Windows.Media.ImageSource RealIcon { get; set; }
    public bool IsHiddenLocked { get; set; }
    private bool _IsBroken;
    public bool IsBroken {
      get => _IsBroken;
      set {
        if (_IsBroken != value) {
          _IsBroken = value;
          OnPropertyChanged(nameof(IsBroken));
        }
      }
    }
    private System.Windows.Media.ImageSource _Icon;
    public System.Windows.Media.ImageSource Icon {
      get => _Icon;
      set {
        if (_Icon != value) {
          _Icon = value;
          OnPropertyChanged(nameof(Icon));
        }
      }
    }
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
    public bool IsHidden { get; set; }
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  }
  public class AchievementDateSorter : System.Collections.IComparer {
    private readonly bool _descending;
    public AchievementDateSorter(bool descending) { _descending = descending; }
    public int Compare(object x, object y) {
      var a = x as AchievementViewModel;
      var b = y as AchievementViewModel;
      if (a == null || b == null) return 0;
      if (a.IsAchieved && !b.IsAchieved) return -1;
      if (!a.IsAchieved && b.IsAchieved) return 1;
      if (a.IsAchieved) {
        if (!a.UnlockTime.HasValue && !b.UnlockTime.HasValue) return 0;
        if (!a.UnlockTime.HasValue) return 1;
        if (!b.UnlockTime.HasValue) return -1;
        return _descending ? b.UnlockTime.Value.CompareTo(a.UnlockTime.Value) : a.UnlockTime.Value.CompareTo(b.UnlockTime.Value);
      } else return b.GlobalPercent.CompareTo(a.GlobalPercent);
    }
  }
  public class RarityColorConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
      if (value is float percent) {
        string mode = parameter as string;
        if (mode == "Blur") return (percent < 1) ? 25.0 : 8.0;
        string hex = "#FF8000";
        if (percent >= 40) hex = "#CCCCCC";
        else if (percent >= 20) hex = "#1EFF00";
        else if (percent >= 5) hex = "#0070DD";
        else if (percent >= 1) hex = "#A335EE";
        Color color = (Color)ColorConverter.ConvertFromString(hex);
        if (mode == "Color") return color;
        return new SolidColorBrush(color);
      }
      if (parameter as string == "Blur") return 0.0;
      if (parameter as string == "Color") return Colors.Transparent;
      return new SolidColorBrush(Colors.Transparent);
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
  }
}
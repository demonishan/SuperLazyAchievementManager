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
using Newtonsoft.Json;
namespace SLAM.Reborn {
  public class AppConfig {
    public List<uint> FavoriteGames { get; set; } = new List<uint>();
  }
  public partial class MainWindow : Window {
    private void CopyGameName_ContextMenu_Click(object sender, RoutedEventArgs e) {
      if (SelectedGameName != null && !string.IsNullOrWhiteSpace(SelectedGameName.Text))
        Clipboard.SetText(SelectedGameName.Text);
    }
    private void CopyGameId_ContextMenu_Click(object sender, RoutedEventArgs e) {
      if (SelectedGameName != null && SelectedGameName.DataContext is GameViewModel game)
        Clipboard.SetText(game.Id.ToString());
    }
    private void CopyGameName_Click(object sender, RoutedEventArgs e) {
      if (sender is Button btn) {
        var grid = VisualTreeHelper.GetParent(btn);
        while (grid != null && !(grid is Grid))
          grid = VisualTreeHelper.GetParent(grid);
        if (grid is Grid g) {
          foreach (var child in LogicalTreeHelper.GetChildren(g)) {
            if (child is TextBlock tb && !string.IsNullOrWhiteSpace(tb.Text)) {
              Clipboard.SetText(tb.Text);
              break;
            }
          }
        }
      }
    }
    private void LoadConfiguration() {
      try {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        if (File.Exists(path)) {
          var json = File.ReadAllText(path);
          _Config = JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
          _FavoriteGameIds = _Config.FavoriteGames ?? new List<uint>();
        }
      } catch {
        _Config = new AppConfig();
        _FavoriteGameIds = new List<uint>();
      }
    }
    private void SaveConfiguration() {
      try {
        _Config.FavoriteGames = _FavoriteGameIds;
        var json = JsonConvert.SerializeObject(_Config, Formatting.Indented);
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        File.WriteAllText(path, json);
      } catch { }
    }
    private void ToggleFavorite(uint appId) {
      if (_FavoriteGameIds.Contains(appId)) _FavoriteGameIds.Remove(appId);
      else _FavoriteGameIds.Add(appId);
      SaveConfiguration();
      var vm = _FilteredGames.FirstOrDefault(x => x.Id == appId);
      if (vm != null) vm.IsFavorite = _FavoriteGameIds.Contains(appId);
      if (_CurrentFilterMode == GameFilterMode.Favorites) RefreshFilter();
    }
    private void FavoritesFilterToggle_Click(object sender, RoutedEventArgs e) {
      SetFilterMode(GameFilterMode.Favorites);
    }
    private void InstalledFilterToggle_Click(object sender, RoutedEventArgs e) {
      SetFilterMode(GameFilterMode.Installed);
    }
    private void ToggleFavorite_Click(object sender, RoutedEventArgs e) {
      if (sender is MenuItem item && item.DataContext is GameViewModel game) {
        ToggleFavorite(game.Id);
      }
    }
    private void ToggleFavorite_Btn_Click(object sender, RoutedEventArgs e) {
      if (sender is System.Windows.Controls.Primitives.ToggleButton btn && btn.DataContext is GameViewModel game) {
        ToggleFavorite(game.Id);
        e.Handled = true;
      }
    }
    private Client _SteamClient;
    private int _StartPlayTimeMinutes = 0;
    private DateTime _SessionStartTime = DateTime.MinValue;
    private List<GameInfo> _AllGames = new List<GameInfo>();
    private ObservableCollection<GameViewModel> _FilteredGames = new ObservableCollection<GameViewModel>();
    private bool _WantGames = true;
    private bool _WantMods = false;
    private bool _WantDemos = false;
    private bool _WantDlc = false;
    private enum GameFilterMode { Favorites, Installed, WithAchievements, WithoutAchievements }
    private GameFilterMode _CurrentFilterMode = GameFilterMode.Installed;
    private void SetFilterMode(GameFilterMode mode) {
      if (_CurrentFilterMode == mode) return;
      _CurrentFilterMode = mode;
      if (FilterFavoritesBtn != null) FilterFavoritesBtn.IsChecked = mode == GameFilterMode.Favorites;
      if (FilterInstalledBtn != null) FilterInstalledBtn.IsChecked = mode == GameFilterMode.Installed;
      if (FilterWithAchievementsBtn != null) FilterWithAchievementsBtn.IsChecked = mode == GameFilterMode.WithAchievements;
      if (FilterWithoutAchievementsBtn != null) FilterWithoutAchievementsBtn.IsChecked = mode == GameFilterMode.WithoutAchievements;
      RefreshFilter();
    }
    private DispatcherTimer _CallbackTimer;
    private DispatcherTimer _KeepAliveTimer;
    private ICollectionView _AchievementView;
    private string _AppVersion = "";
    private List<uint> _FavoriteGameIds = new List<uint>();
    private AppConfig _Config = new AppConfig();
    public MainWindow() {
      InitializeComponent();
      LoadConfiguration();
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
        if (WindowTitleText != null) WindowTitleText.Text = "Super Lazy Achievement Manager";
        if (WindowVersionText != null) WindowVersionText.Text = $"v{_AppVersion}";
        Title = "SLAM";
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
        catch (Exception ex) { SLAM.Reborn.App.LogCrash(ex, "CallbackTimer_RunCallbacks"); }
      };
      _CallbackTimer.Start();
      _KeepAliveTimer = new DispatcherTimer();
      _KeepAliveTimer.Interval = TimeSpan.FromMinutes(5);
      _KeepAliveTimer.Tick += (s, e) => {
        try {
          if (_SteamClient != null) {
            _SteamClient.SteamUserStats.StoreStats();
            Dispatcher.Invoke(() => UpdatePlayTimeDisplay());
          }
        } catch { }
      };
      AppDomain.CurrentDomain.UnhandledException += (s, e) => {
        if (e.ExceptionObject is Exception ex) SLAM.Reborn.App.LogCrash(ex, "AppDomain_UnhandledException");
      };
      TaskScheduler.UnobservedTaskException += (s, e) => {
        SLAM.Reborn.App.LogCrash(e.Exception, "TaskScheduler_UnobservedTaskException");
        e.SetObserved();
      };
      if (Application.Current != null) Application.Current.DispatcherUnhandledException += (s, e) => {
        SLAM.Reborn.App.LogCrash(e.Exception, "DispatcherUnhandledException");
        e.Handled = true;
      };
      LoadData();
    }
    private void DisplayAlert(string message, bool isError) {
      if (isError) {
        new WarningWindow(message, "Okay", null, "Error").ShowDialog();
      }
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
      _KeepAliveTimer?.Stop();
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
        var installPath = SAM.API.Steam.GetInstallPath();
        var allKnownGames = AppInfoReader.GetGames(installPath);
        var fetchedGames = new List<GameInfo>();
        foreach (var game in allKnownGames) {
          try {
            if (_SteamClient.SteamApps008.IsSubscribedApp(game.Id)) {
              game.IsInstalled = _SteamClient.SteamApps008.IsAppInstalled(game.Id);
              fetchedGames.Add(game);
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
          if (fetchedGames.Count == 0) DisplayAlert("No games found. Check if Steam is running or if appinfo.vdf is readable.", true);
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
      BitmapImage bitmap = null;
      bitmap = await ImageCacheHelper.GetImageAsync(game.ImageUrl, path);
      if (bitmap == null) {
        try {
          var json = await client.DownloadStringTaskAsync($"https://store.steampowered.com/api/appdetails?appids={game.Id}");
          var match = System.Text.RegularExpressions.Regex.Match(json, "\"header_image\"\\s*:\\s*\"(.*?)\"");
          if (match.Success) {
            var url = match.Groups[1].Value.Replace("\\/", "/");
            bitmap = await ImageCacheHelper.GetImageAsync(url, path);
          }
        } catch { }
      }
      if (bitmap != null) {
        await Dispatcher.InvokeAsync(() => {
          try {
            game.CachedIcon = bitmap;
            var vm = _FilteredGames.FirstOrDefault(x => x.Id == game.Id);
            if (vm != null) vm.Image = bitmap;
          } catch { }
        }, DispatcherPriority.Background);
      } else {
        await Dispatcher.InvokeAsync(() => {
          try {
            var fallback = new BitmapImage(new Uri("pack://application:,,,/SLAM;component/Resources/image-not-found.png"));
            game.CachedIcon = fallback;
            var vm = _FilteredGames.FirstOrDefault(x => x.Id == game.Id);
            if (vm != null) vm.Image = fallback;
          } catch { }
        }, DispatcherPriority.Background);
      }
    }
    private void RefreshFilter() {
      if (SearchBox == null) return;
      var searchText = SearchBox.Text.ToLower(CultureInfo.InvariantCulture);
      if (ClearSearchBtn != null) ClearSearchBtn.Visibility = string.IsNullOrEmpty(searchText) ? Visibility.Collapsed : Visibility.Visible;
      var filtered = _AllGames.Where(g => (string.IsNullOrEmpty(searchText) || g.Name.ToLower(CultureInfo.InvariantCulture).Contains(searchText)) && ((g.Type == "normal" && _WantGames) || (g.Type == "mod" && _WantMods) || (g.Type == "demo" && _WantDemos) || (g.Type == "dlc" && _WantDlc))).OrderBy(g => g.Name).ToList();
      switch (_CurrentFilterMode) {
        case GameFilterMode.Favorites:
          filtered = filtered.Where(g => _FavoriteGameIds.Contains(g.Id)).ToList();
          break;
        case GameFilterMode.Installed:
          filtered = filtered.Where(g => g.IsInstalled).ToList();
          break;
        case GameFilterMode.WithAchievements:
          filtered = filtered.Where(g => g.HasAchievements).ToList();
          break;
        case GameFilterMode.WithoutAchievements:
          filtered = filtered.Where(g => !g.HasAchievements).ToList();
          break;
      }
      _FilteredGames.Clear();
      foreach (var g in filtered)
        _FilteredGames.Add(new GameViewModel { Id = g.Id, Name = g.Name, Type = g.Type, Image = g.CachedIcon, IsFavorite = _FavoriteGameIds.Contains(g.Id), IsInstalled = g.IsInstalled });
    }
    private ObservableCollection<AchievementViewModel> _Achievements = new ObservableCollection<AchievementViewModel>();
    private uint _SelectedGameId;
    private SAM.API.UserStatsReceived _UserStatsReceivedCallback;
    private void Game_Click(object sender, MouseButtonEventArgs e) {
      try {
        if (sender is FrameworkElement element && element.DataContext is GameViewModel game) {
          if (game.Type == "dlc") {
            var warning = new WarningWindow($"Does \"{game.Name}\" even exist without the base game? Attempting to find out might anger Lord Gaben. Risk it?", "I Fear Nothing!", "Forgive Me!", "Uncharted Territory");
            warning.ShowDialog();
            if (!warning.IsConfirmed) return;
          }
          if (!game.IsInstalled) {
            var warning = new WarningWindow($"\"{game.Name}\" is not installed. Unlocking achievements for a game you can't even launch? That is peak laziness. I respect it, but Steam might not like it.", "Who Cares?", "My Bad!", "404: Game Not Found");
            warning.ShowDialog();
            if (!warning.IsConfirmed) return;
          }
          _SelectedGameId = game.Id;
          SelectedGameName.Text = game.Name;
          SelectedGameName.DataContext = game;
          if (WindowTitleText != null) WindowTitleText.Text = $"{game.Name} - Super Lazy Achievement Manager";
          if (WindowVersionText != null) WindowVersionText.Text = $"v{_AppVersion}";
          Title = $"{game.Name} - SLAM";
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
          SetGameBackgroundImage(game.Id);
          SharedStatusText.Text = $"Checking if you actually beat {game.Name}";
          SharedStatusText.Text = $"Checking if you actually beat {game.Name}";
          LoadGameData(true);
        }
      } catch (Exception ex) {
        DisplayAlert($"Error switching game: {ex.Message}", true);
        SLAM.Reborn.App.LogCrash(ex, "Game_Click");
        HomeView.Visibility = Visibility.Visible;
        GameDetailsView.Visibility = Visibility.Collapsed;
        IsTimerMode = false;
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
        if (EnableTimerButton != null) EnableTimerButton.Visibility = Visibility.Visible;
        if (LinksButton != null) LinksButton.Visibility = Visibility.Visible;
        if (RefreshButton != null) RefreshButton.Visibility = Visibility.Visible;
        if (SaveButton != null) SaveButton.Visibility = Visibility.Visible;
        if (BulkActionsButton != null) BulkActionsButton.Visibility = Visibility.Visible;
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
          RefreshAchievementFilter();
          ApplySort();
          ApplySortByTag("Rarity_Desc");
          _AchievementView.Refresh();
        } else {
          RefreshAchievementFilter();
          ApplySort();
        }
        _Achievements.Clear();
        if (NoAchievementsMessage != null) NoAchievementsMessage.Visibility = Visibility.Collapsed;
        if (AchievementList != null) AchievementList.Visibility = Visibility.Visible;
        LoadingOverlay.Visibility = Visibility.Visible;
        SharedStatusText.Text = "Switching Steam context...";
        _CallbackTimer.Stop();
        try {
          _SteamClient?.Dispose();
          _SteamClient = null;
          GC.Collect();
          GC.WaitForPendingFinalizers();
          SAM.API.Steam.Unload();
          _SteamClient = new Client();
          _SteamClient.Initialize(_SelectedGameId);
          try {
            string installPath = SAM.API.Steam.GetInstallPath();
            ulong steamId64 = _SteamClient.SteamUser.GetSteamId();
            uint accountId = (uint)(steamId64 & 0xFFFFFFFF);
            var stats = LocalConfigReader.GetAppStats(installPath, accountId, _SelectedGameId);
            _StartPlayTimeMinutes = stats.PlayTimeMinutes;
            _SessionStartTime = DateTime.Now;
          } catch { _StartPlayTimeMinutes = 0; _SessionStartTime = DateTime.Now; }
          _UserStatsReceivedCallback = _SteamClient.CreateAndRegisterCallback<SAM.API.UserStatsReceived>();
          _UserStatsReceivedCallback.OnRun += (p) => {
            Dispatcher.Invoke(() => {
              if (p.GameId != _SelectedGameId) {
                DisplayAlert($"Steam returned stats for the wrong game ({p.GameId} vs {_SelectedGameId}). Please restart SLAM.", true);
                LoadingOverlay.Visibility = Visibility.Collapsed;
                return;
              }
              if (p.Result == 1) {
                try { FetchAchievements(); }
                catch (Exception ex) {
                  DisplayAlert("Error fetching achievements: " + ex.Message, true);
                  SLAM.Reborn.App.LogCrash(ex, "LoadGameData_FetchAchievements");
                  LoadingOverlay.Visibility = Visibility.Collapsed;
                }
              }
              else {
                SharedStatusText.Text = $"Steam error {p.Result} (UserStatsReceived).";
                LoadingOverlay.Visibility = Visibility.Collapsed;
              }
            });
          };
        } catch (ClientInitializeException cie) {
          if (cie.Failure == ClientInitializeFailure.AppIdMismatch) DisplayAlert("Identity Crisis! I think I'm in the wrong game. Please restart to fix my brain.", true);
          else DisplayAlert("Failed to switch Steam context: " + cie.Message, true);
          SLAM.Reborn.App.LogCrash(cie, "LoadGameData_SteamSwitch_Init");
          LoadingOverlay.Visibility = Visibility.Collapsed;
          _CallbackTimer.Start();
          return;
        } catch (Exception ex) {
          DisplayAlert("Failed to switch Steam context: " + ex.Message, true);
          SLAM.Reborn.App.LogCrash(ex, "LoadGameData_SteamSwitch");
          LoadingOverlay.Visibility = Visibility.Collapsed;
          _CallbackTimer.Start();
          return;
        }
        _CallbackTimer.Start();
        var gameInfo = _AllGames.FirstOrDefault(g => g.Id == _SelectedGameId);
        if (gameInfo != null && !gameInfo.HasAchievements) {
          if (NoAchievementsMessage != null) NoAchievementsMessage.Visibility = Visibility.Visible;
          if (AchievementList != null) AchievementList.Visibility = Visibility.Collapsed;
          if (BulkActionsButton != null) BulkActionsButton.Visibility = Visibility.Collapsed;
          if (EnableTimerButton != null) EnableTimerButton.Visibility = Visibility.Collapsed;
          if (AchievementSearchRow != null) AchievementSearchRow.Visibility = Visibility.Collapsed;
          if (RefreshButton != null) RefreshButton.Visibility = Visibility.Collapsed;
          if (SaveButton != null) SaveButton.Visibility = Visibility.Collapsed;
          LoadingOverlay.Visibility = Visibility.Collapsed;
          SharedStatusText.Text = "No achievements here. You're just here for the card drops, aren't you? I won't tell.";
          return;
        }
        var steamId = _SteamClient.SteamUser.GetSteamId();
        _SteamClient.SteamUserStats.RequestUserStats(steamId);
        _SteamClient.SteamUserStats.RequestGlobalAchievementPercentages();
        SharedStatusText.Text = "Fetching stats... don't rush me.";
        _KeepAliveTimer.Start();
      } catch (Exception ex) {
        DisplayAlert($"Critical error loading game data: {ex.Message}", true);
        SLAM.Reborn.App.LogCrash(ex, "LoadGameData_Critical");
        LoadingOverlay.Visibility = Visibility.Collapsed;
        _CallbackTimer.Start();
      }
    }
    private void FetchAchievements() {
      var definitions = new List<AchievementDefinition>();
      if (!LoadUserGameStatsSchema(definitions)) SharedStatusText.Text = "Steam ghosted my request for the schema. Expect some missing info. Use your imagination or restart SLAM.";
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
            OriginalIsAchieved = isAchieved,
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
      if (_Achievements.Count == 0) {
        if (NoAchievementsMessage != null) NoAchievementsMessage.Visibility = Visibility.Visible;
        if (AchievementList != null) AchievementList.Visibility = Visibility.Collapsed;
      } else {
        if (NoAchievementsMessage != null) NoAchievementsMessage.Visibility = Visibility.Collapsed;
        if (AchievementList != null) AchievementList.Visibility = Visibility.Visible;
      }
      bool hasHiddenLocked = _Achievements.Any(x => x.IsHiddenLocked);
      if (RevealHiddenBtn != null) RevealHiddenBtn.Visibility = hasHiddenLocked ? Visibility.Visible : Visibility.Collapsed;
      SharedStatusText.Text = $"Loaded {_Achievements.Count} achievements.";
      int unlocked = _Achievements.Count(x => x.IsAchieved);
      int total = _Achievements.Count;
      int locked = total - unlocked;
      if (AchievementProgressBar != null) AchievementProgressBar.Visibility = Visibility.Visible;
      UpdateProgressBar();
      UpdatePlayTimeDisplay();
      if (anyProtected) {
        AreModificationsAllowed = false;
        UpdateProtectionState();
        UpdateStatsMessage();
        DisplayAlert("These achievements are protected! SLAM is lazy, not magical. Can't touch 'em.", true);
      } else {
        AreModificationsAllowed = true;
        UpdateProtectionState();
        UpdateStatsMessage();
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
    private void LockAll_Click(object sender, RoutedEventArgs e) {
      foreach (var ach in _Achievements) ach.IsAchieved = false;
    }
    private void StartAchievementImageCaching(List<AchievementViewModel> achievements) {
      Task.Run(async () => {
        try {
          var cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "achievements");
          if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);
          foreach (var ach in achievements) {
            string urlToLoad = ach.RealIconUrl;
            if (string.IsNullOrEmpty(urlToLoad) || urlToLoad.StartsWith("pack://")) {
              if (ach.IconUrl.StartsWith("pack://"))
                await Dispatcher.InvokeAsync(() => {
                  try {
                    ach.Icon = new BitmapImage(new Uri(ach.IconUrl));
                  } catch { }
                });
              continue;
            }
            var filename = System.IO.Path.GetFileName(new Uri(urlToLoad).LocalPath);
            var path = Path.Combine(cacheDir, filename);
            var bitmap = await ImageCacheHelper.GetImageAsync(urlToLoad, path);
            await Task.Delay(20);
            if (bitmap != null) {
              await Dispatcher.InvokeAsync(() => {
                try {
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
        SharedStatusText.Text = $"{_AllGames.Count} games detected! Your wallet sends its regards.";
        if (SharedStatusText.Parent is Border b) b.Background = new SolidColorBrush(Color.FromRgb(0, 122, 204));
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
        if (GameBackgroundImage != null) {
          GameBackgroundImage.Source = new BitmapImage(new Uri("pack://application:,,,/SLAM;component/Resources/bg.png"));
          GameBackgroundImage.Visibility = Visibility.Visible;
        }
        if (WindowTitleText != null) WindowTitleText.Text = "Super Lazy Achievement Manager";
        if (WindowVersionText != null) WindowVersionText.Text = $"v{_AppVersion}";
        Title = "SLAM";
        _CallbackTimer.Stop();
        _KeepAliveTimer.Stop();
        try {
          _SteamClient?.Dispose();
          _SteamClient = null;
          GC.Collect();
          GC.WaitForPendingFinalizers();
          SAM.API.Steam.Unload();
          _SteamClient = new Client();
          _SteamClient.Initialize(0);
        } catch (Exception ex) {
          DisplayAlert($"Failed to reinitialize Steam on back: {ex.Message}", true);
          SLAM.Reborn.App.LogCrash(ex, "Back_Click_SteamReinit");
          _SteamClient = null;
          _CallbackTimer.Start();
          return;
        }
        _CallbackTimer.Start();
        LoadData();
      } catch (Exception ex) {
        DisplayAlert($"Error returning to game list: {ex.Message}", true);
        SLAM.Reborn.App.LogCrash(ex, "Back_Click");
      }
    }
    private async void SetGameBackgroundImage(uint appId) {
      if (GameBackgroundImage == null) return;
      BitmapImage bitmap = null;
      try {
        string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "backgrounds");
        string bgPath = Path.Combine(cacheDir, $"{appId}.jpg");
        string bgUrl = null;
        using (var client = new WebClient()) {
          client.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
          string url = $"https://store.steampowered.com/api/appdetails?appids={appId}";
          string json = await client.DownloadStringTaskAsync(url);
          var data = Newtonsoft.Json.Linq.JObject.Parse(json);
          var gameData = data[appId.ToString()];
          if (gameData != null && (bool)gameData["success"]) {
            bgUrl = (string)gameData["data"]?["background"];
          }
        }
        if (!string.IsNullOrEmpty(bgUrl)) {
          bitmap = await ImageCacheHelper.GetImageAsync(bgUrl, bgPath);
        }
      } catch { }
      if (bitmap == null) {
        bitmap = new BitmapImage(new Uri("pack://application:,,,/SLAM;component/Resources/bg.png"));
      }
      GameBackgroundImage.Source = bitmap;
      GameBackgroundImage.Visibility = Visibility.Visible;
    }
    private void RefreshGame_Click(object sender, RoutedEventArgs e) => LoadGameData(false);
    private void Store_Click(object sender, RoutedEventArgs e) {
      if (_Achievements != null) {
        int unlockCount = _Achievements.Count(a => !a.OriginalIsAchieved && a.IsAchieved);
        if (unlockCount > 2) {
          var warning = new WarningWindow($"You're about to pop {unlockCount} achievements at once. That's very efficient, but also suspicious. Why not use the Timer Mode to look like a human?", "I am a machine!", "I am smart!", "The \"reckless\" set");
          warning.ShowDialog();
          if (!warning.IsConfirmed) return;
        }
      }
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
    private void GameFilterToggle_Click(object sender, RoutedEventArgs e) {
      if (sender is System.Windows.Controls.Primitives.ToggleButton btn) {
        if (btn.IsChecked == false) {
          btn.IsChecked = true;
          return;
        }
        if (btn == FilterGamesBtn) {
          if (FilterModsBtn != null) FilterModsBtn.IsChecked = false;
          if (FilterDemosBtn != null) FilterDemosBtn.IsChecked = false;
          if (FilterDlcBtn != null) FilterDlcBtn.IsChecked = false;
        } else if (btn == FilterModsBtn) {
          if (FilterGamesBtn != null) FilterGamesBtn.IsChecked = false;
          if (FilterDemosBtn != null) FilterDemosBtn.IsChecked = false;
          if (FilterDlcBtn != null) FilterDlcBtn.IsChecked = false;
        } else if (btn == FilterDemosBtn) {
          if (FilterGamesBtn != null) FilterGamesBtn.IsChecked = false;
          if (FilterModsBtn != null) FilterModsBtn.IsChecked = false;
          if (FilterDlcBtn != null) FilterDlcBtn.IsChecked = false;
        } else if (btn == FilterDlcBtn) {
          if (FilterGamesBtn != null) FilterGamesBtn.IsChecked = false;
          if (FilterModsBtn != null) FilterModsBtn.IsChecked = false;
          if (FilterDemosBtn != null) FilterDemosBtn.IsChecked = false;
        }
        _WantGames = FilterGamesBtn.IsChecked == true;
        _WantMods = FilterModsBtn.IsChecked == true;
        _WantDemos = FilterDemosBtn.IsChecked == true;
        _WantDlc = FilterDlcBtn.IsChecked == true;
        RefreshFilter();
      }
    }
    private void AchievementFilterToggle_Click(object sender, RoutedEventArgs e) {
      if (sender == FilterWithAchievementsBtn) SetFilterMode(GameFilterMode.WithAchievements);
      else if (sender == FilterWithoutAchievementsBtn) SetFilterMode(GameFilterMode.WithoutAchievements);
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
      if (AchievementSearchRow != null) AchievementSearchRow.Visibility = Visibility.Visible;
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
    private void UpdateStatsMessage() {
      int unlocked = _Achievements.Count(x => x.IsAchieved);
      int total = _Achievements.Count;
      int locked = total - unlocked;
      if (!AreModificationsAllowed) SharedStatusText.Text = "These achievements are protected! SLAM is lazy, not magical. Can't touch 'em.";
      else {
        if (unlocked == total && total > 0) SharedStatusText.Text = $"Unlocked all {total}. Please, touch some grass.";
        else if (unlocked == 0 && total > 0) SharedStatusText.Text = $"0 down, {total} to go. Do you even play this game?";
        else SharedStatusText.Text = $"{unlocked} out of {total} popped. Standing between me and my nap are the remaining {locked}.";
      }
    }
    private void UpdatePlayTimeDisplay() {
      if (SelectedGamePlayTime == null) return;
      SelectedGamePlayTime.Visibility = Visibility.Collapsed;
      SelectedGamePlayTime.Text = "";
      try {
        if (_SteamClient == null) return;
        int currentPlayTime = _StartPlayTimeMinutes;
        if (_SessionStartTime != DateTime.MinValue) {
          currentPlayTime += (int)(DateTime.Now - _SessionStartTime).TotalMinutes;
        }
        if (currentPlayTime > 0) {
          double hours = currentPlayTime / 60.0;
          string text = $"Played: {hours:0.0}h";
          text += " | Last Played: Today";
          SelectedGamePlayTime.Text = text;
          SelectedGamePlayTime.Visibility = Visibility.Visible;
        }
      } catch { }
    }
    private void EnableTimer_Click(object sender, RoutedEventArgs e) {
      IsTimerMode = !IsTimerMode;
      if (IsTimerMode) {
        if (SortFilter != null) SortFilter.SelectedIndex = 0;
        if (_AchievementView != null) {
          _AchievementView.SortDescriptions.Clear();
          var lcv = _AchievementView as ListCollectionView;
          if (lcv != null) lcv.CustomSort = null;
          _AchievementView.Refresh();
        }
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
        if (SharedStatusText != null) UpdateStatsMessage();
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
      } else DisplayAlert("Math is hard. Please use actual numbers so I don't have to think.", true);
    }
    private void SmartRandom_Click(object sender, RoutedEventArgs e) {
      if (int.TryParse(RandMinInput.Text, out int min) && int.TryParse(RandMaxInput.Text, out int max)) {
        if (min > max) {
          int temp = min;
          min = max;
          max = temp;
        }
        var random = new Random();
        var pending = (_AchievementView != null ? _AchievementView.Cast<AchievementViewModel>() : _Achievements).Where(a => !a.IsAchieved).ToList();
        int count = pending.Count;
        int spread = max - min;
        int jitterLimit = Math.Max(1, (int)(spread * 0.15));
        for (int i = 0; i < count; i++) {
          double progress = (double)i / (count > 1 ? count - 1 : 1);
          int target = (int)(min + spread * progress);
          int jitter = random.Next(-jitterLimit, jitterLimit + 1);
          int val = Math.Max(min, Math.Min(max, target + jitter));
          pending[i].TimerMinutes = val.ToString();
        }
        if (RandomTimerPopup != null) RandomTimerPopup.IsOpen = false;
      } else DisplayAlert("Math is hard. Please use actual numbers so I don't have to think.", true);
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
          if (processedCount == 0) DisplayAlert("I can't find any timers. I can't read your mind, you have to type the numbers.", false);
          else DisplayAlert($"Job's done! I unlocked {processedCount} achievements for you. You're welcome.", false);
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
          await Dispatcher.InvokeAsync(() => UpdateTimerMetadata(ach, totalSeconds / 60.0));
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
    private void AchievementList_DragOver(object sender, DragEventArgs e) {
      if (!IsTimerMode || IsTimerRunning || !e.Data.GetDataPresent("myFormat")) {
        e.Effects = DragDropEffects.None;
      } else {
        e.Effects = DragDropEffects.Move;
      }
      e.Handled = true;
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
    private void UpdateTimerMetadata(AchievementViewModel activeItem = null, double activeRemainingMinutes = -1) {
      if (_AchievementView == null) return;
      int i = 1;
      DateTime projection = DateTime.Now;
      foreach (AchievementViewModel ach in _AchievementView) {
        ach.Index = i++;
        if (ach.IsAchieved) {
          ach.ETA = "";
          continue;
        }
        double minutes = 0;
        bool isParsed = double.TryParse(ach.TimerMinutes, out minutes);
        if (activeItem != null && ach == activeItem && activeRemainingMinutes >= 0) {
          minutes = activeRemainingMinutes;
          isParsed = true;
        }
        if (isParsed && minutes > 0) {
          projection = projection.AddMinutes(minutes);
          ach.ETA = $"{projection:t}";
          projection = projection.AddSeconds(1);
        } else if (isParsed && minutes == 0) {
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
    public bool HasAchievements { get; set; }
    public bool IsInstalled { get; set; }
  }
  public class GameViewModel : INotifyPropertyChanged {
    public uint Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public bool IsInstalled { get; set; }
    private bool _IsFavorite;
    public bool IsFavorite {
      get => _IsFavorite;
      set {
        if (_IsFavorite != value) {
          _IsFavorite = value;
          OnPropertyChanged(nameof(IsFavorite));
        }
      }
    }
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
    public bool OriginalIsAchieved { get; set; }
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
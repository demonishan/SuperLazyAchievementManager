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
using System.Windows.Input;
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
           
            LoadData();
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

            foreach (var kv in pairs) {
                if (_SteamClient.SteamApps008.IsSubscribedApp(kv.Key)) {
                    var name = _SteamClient.SteamApps001.GetAppData(kv.Key, "name") ?? $"App {kv.Key}";
                    _AllGames.Add(new GameInfo {
                        Id = kv.Key,
                        Name = name,
                        Type = kv.Value,
                        ImageUrl = $"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{kv.Key}/capsule_231x87.jpg"
                    });
                }
            }
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

        private void Game_Click(object sender, MouseButtonEventArgs e) {
             if (sender is FrameworkElement fe && fe.DataContext is GameViewModel vm) {
                 try {
                     Process.Start("SAM.Game.exe", vm.Id.ToString(CultureInfo.InvariantCulture));
                 } catch (Exception ex) {
                     MessageBox.Show("Failed to launch SAM.Game.exe: " + ex.Message);
                 }
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
}
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace SLAM.Reborn {
  public class AppState : INotifyPropertyChanged {
    public List<GameInfo> AllGames { get; set; } = new List<GameInfo>();
    public ObservableCollection<GameViewModel> FilteredGames { get; set; } = new ObservableCollection<GameViewModel>();
    public ObservableCollection<AchievementViewModel> Achievements { get; set; } = new ObservableCollection<AchievementViewModel>();
    public List<uint> FavoriteGameIds { get; set; } = new List<uint>();
    private uint _selectedGameId = 0;
    public uint SelectedGameId {
      get => _selectedGameId;
      set { _selectedGameId = value; OnPropertyChanged(); }
    }
    public enum GameFilterMode { Favorites, Installed, WithAchievements, WithoutAchievements }
    private GameFilterMode _currentFilterMode = GameFilterMode.Installed;
    public GameFilterMode CurrentFilterMode {
      get => _currentFilterMode;
      set { _currentFilterMode = value; OnPropertyChanged(); }
    }
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}

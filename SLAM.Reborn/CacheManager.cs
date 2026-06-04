using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SLAM.Reborn {
  public static class CacheManager {
    public static Task RunCleanupAsync(AppState state) {
      return Task.Run(() => {
        try {
          // 1. Wipe the old flat achievements cache folder if it exists (one-time migration)
          var oldCacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "achievements");
          if (Directory.Exists(oldCacheDir)) {
            var files = Directory.GetFiles(oldCacheDir, "*.*", SearchOption.TopDirectoryOnly);
            foreach (var file in files) {
              try { File.Delete(file); } catch { }
            }
          }

          // 2. Iterate over games and clean up folders for uninstalled & non-favorite games
          foreach (var game in state.AllGames) {
            bool isInstalled = game.IsInstalled;
            bool isFavorite = state.FavoriteGameIds.Contains(game.Id);

            if (!isInstalled && !isFavorite) {
              // Delete the achievement icons folder for this game
              var achCacheDir = Path.Combine(oldCacheDir, game.Id.ToString());
              if (Directory.Exists(achCacheDir)) {
                try { Directory.Delete(achCacheDir, true); } catch { }
              }
              
              // Delete the game's high-res background image
              var bgCacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "backgrounds");
              var bgFile = Path.Combine(bgCacheDir, $"{game.Id}.jpg");
              if (File.Exists(bgFile)) {
                try { File.Delete(bgFile); } catch { }
              }
              
              // We intentionally KEEP the game's profile image (cache/games/{game.Id}.jpg) 
              // so the list populates fast.
            }
          }
        } catch (Exception ex) {
          System.Diagnostics.Debug.WriteLine($"Cache cleanup error: {ex.Message}");
        }
      });
    }
  }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace SLAM.Reborn {
  public static class ImageCacheService {
    public static async Task ProcessGameImageAsync(GameInfo game, WebClient client, Dispatcher dispatcher) {
      if (game.CachedIcon != null) return;
      var cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "games");
      if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);
      var path = Path.Combine(cacheDir, $"{game.Id}.jpg");
      BitmapImage bitmap = await ImageCacheHelper.GetImageAsync(game.ImageUrl, path);
      if (bitmap == null) {
        try {
          var json = await client.DownloadStringTaskAsync(string.Format(SteamConstants.AppDetailsApiUrl, game.Id));
          var match = System.Text.RegularExpressions.Regex.Match(json, "\"header_image\"\\s*:\\s*\"(.*?)\"");
          if (match.Success) {
            var url = match.Groups[1].Value.Replace("\\/", "/");
            bitmap = await ImageCacheHelper.GetImageAsync(url, path);
          }
        } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Steam API image fetch error: {ex.Message}"); }
      }
      if (bitmap != null) {
        await dispatcher.InvokeAsync(() => {
          try {
            game.CachedIcon = bitmap;
          } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Dispatcher image assignment error: {ex.Message}"); }
        }, DispatcherPriority.Background);
      } else {
        await dispatcher.InvokeAsync(() => {
          try {
            game.CachedIcon = new BitmapImage(new Uri("pack://application:,,,/SLAM;component/Resources/image-not-found.png"));
          } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Dispatcher fallback image error: {ex.Message}"); }
        }, DispatcherPriority.Background);
      }
    }
    public static async Task FetchAchievementIconsAsync(uint gameId, List<AchievementViewModel> achievements, SAM.API.Client steamClient, bool revealHidden, Dispatcher dispatcher) {
      var cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "achievements", gameId.ToString());
      if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);
      foreach (var ach in achievements) {
        string urlToLoad = ach.RealIconUrl;
        if (string.IsNullOrEmpty(urlToLoad) || urlToLoad.EndsWith("/")) {
          var fallbackPath = Path.Combine(cacheDir, $"{ach.Id}.png");
          var localBitmap = await ImageCacheHelper.GetImageAsync(fallbackPath, fallbackPath);
          if (localBitmap != null) {
            await dispatcher.InvokeAsync(() => {
              ach.RealIcon = localBitmap;
              ach.Icon = localBitmap;
              ach.IsBroken = false;
            });
            continue;
          }
          int handle = steamClient.SteamUserStats.GetAchievementIcon(ach.Id);
          var steamBmp = GetSteamImage(handle, steamClient);
          if (steamBmp != null) {
            SaveBitmapSourceToDisk(steamBmp, fallbackPath);
            await dispatcher.InvokeAsync(() => {
              ach.RealIcon = steamBmp;
              ach.Icon = steamBmp;
              ach.IsBroken = false;
            });
          } else {
            await dispatcher.InvokeAsync(() => {
              if (ach.IsHiddenLocked && ach.IconUrl.StartsWith("pack://")) {
                try { ach.Icon = new BitmapImage(new Uri(ach.IconUrl)); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Hidden icon internal load error: {ex.Message}"); }
              } else ach.IsBroken = true;
            }, DispatcherPriority.Background);
          }
          continue;
        }
        if (urlToLoad.StartsWith("pack://")) {
          if (ach.IconUrl.StartsWith("pack://")) {
            await dispatcher.InvokeAsync(() => {
              try { ach.Icon = new BitmapImage(new Uri(ach.IconUrl)); }
              catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Pack URI fetch error: {ex.Message}"); }
            });
          }
          continue;
        }
        var filename = System.IO.Path.GetFileName(new Uri(urlToLoad).LocalPath);
        var path = Path.Combine(cacheDir, filename);
        var bitmap = await ImageCacheHelper.GetImageAsync(urlToLoad, path);
        await Task.Delay(20);
        if (bitmap != null) {
          await dispatcher.InvokeAsync(() => {
            try {
              ach.RealIcon = bitmap;
              if (!ach.IsHiddenLocked || revealHidden) {
                ach.Icon = bitmap;
              } else if (ach.IsHiddenLocked && ach.Icon == null && ach.IconUrl.StartsWith("pack://")) {
                try { ach.Icon = new BitmapImage(new Uri(ach.IconUrl)); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Fallback pack URI fetch error: {ex.Message}"); }
              }
            } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Achievement icon assignment error: {ex.Message}"); }
          }, DispatcherPriority.Background);
        } else {
          var fallbackPath = Path.Combine(cacheDir, $"{ach.Id}.png");
          var localBitmap = await ImageCacheHelper.GetImageAsync(fallbackPath, fallbackPath);
          if (localBitmap != null) {
            await dispatcher.InvokeAsync(() => {
              ach.RealIcon = localBitmap;
              ach.Icon = localBitmap;
              ach.IsBroken = false;
            });
            continue;
          }
          int handle = steamClient.SteamUserStats.GetAchievementIcon(ach.Id);
          var steamBmp = GetSteamImage(handle, steamClient);
          if (steamBmp != null) {
            SaveBitmapSourceToDisk(steamBmp, fallbackPath);
            await dispatcher.InvokeAsync(() => {
              ach.RealIcon = steamBmp;
              ach.Icon = steamBmp;
              ach.IsBroken = false;
            });
          } else {
            await dispatcher.InvokeAsync(() => { ach.IsBroken = true; }, DispatcherPriority.Background);
          }
        }
      }
    }
    private static void SaveBitmapSourceToDisk(BitmapSource bitmap, string path) {
      try {
        if (!Directory.Exists(Path.GetDirectoryName(path))) Directory.CreateDirectory(Path.GetDirectoryName(path));
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var fs = new FileStream(path, FileMode.Create)) {
          encoder.Save(fs);
        }
      } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"SaveBitmap error: {ex.Message}"); }
    }
    private static BitmapSource GetSteamImage(int handle, SAM.API.Client steamClient) {
      if (handle <= 0) return null;
      if (steamClient.SteamUtils.GetImageSize(handle, out int width, out int height)) {
        int size = width * height * 4;
        byte[] data = new byte[size];
        if (steamClient.SteamUtils.GetImageRGBA(handle, data)) {
          for (int i = 0; i < size; i += 4) {
            byte r = data[i];
            data[i] = data[i + 2];
            data[i + 2] = r;
          }
          var bitmap = BitmapSource.Create(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, data, width * 4);
          bitmap.Freeze();
          return bitmap;
        }
      }
      return null;
    }
  }
}

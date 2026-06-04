using System;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
namespace SLAM.Reborn {
  public static class ImageCacheHelper {
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BitmapImage> _memoryCache = new System.Collections.Concurrent.ConcurrentDictionary<string, BitmapImage>();
    public static async Task<BitmapImage> GetImageAsync(string url, string cachePath) {
      if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(cachePath)) return null;
      if (_memoryCache.TryGetValue(cachePath, out var cachedImage)) return cachedImage;
      var dir = System.IO.Path.GetDirectoryName(cachePath);
      if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
      if (!System.IO.File.Exists(cachePath) || new System.IO.FileInfo(cachePath).Length == 0) {
        try {
          using (var client = new System.Net.WebClient()) {
            var data = await client.DownloadDataTaskAsync(url);
            if (data.Length > 0) System.IO.File.WriteAllBytes(cachePath, data);
          }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"ImageCacheHelper Download Error: {ex.Message}"); }
      }
      if (System.IO.File.Exists(cachePath) && new System.IO.FileInfo(cachePath).Length > 0) {
        try {
          BitmapImage bitmap = null;
          await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
            bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(cachePath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
          });
          _memoryCache[cachePath] = bitmap;
          return bitmap;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"ImageCacheHelper Load Error: {ex.Message}"); }
      }
      return null;
    }
  }
}

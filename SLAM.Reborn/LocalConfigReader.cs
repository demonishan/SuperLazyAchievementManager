using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace SLAM.Reborn {
  public static class LocalConfigReader {
    public struct GamePlayStats {
      public int PlayTimeMinutes;
      public long LastPlayedTime;
    }
    public static GamePlayStats GetAppStats(string steamInstallPath, uint accountId, uint appId) {
      var stats = new GamePlayStats();
      try {
        string userDataPath = Path.Combine(steamInstallPath, "userdata", accountId.ToString());
        if (!Directory.Exists(userDataPath)) return stats;
        string localConfigPath = Path.Combine(userDataPath, "config", "localconfig.vdf");
        if (!File.Exists(localConfigPath)) return stats;
        string[] lines = File.ReadAllLines(localConfigPath);
        return ParseStatsFromVdf(lines, appId.ToString());
      } catch { return stats; }
    }
    public static int GetPlayTimeMinutes(string steamInstallPath, uint accountId, uint appId) => GetAppStats(steamInstallPath, accountId, appId).PlayTimeMinutes;
    private static GamePlayStats ParseStatsFromVdf(string[] lines, string appId) {
      var stats = new GamePlayStats();
      var desiredPath = new List<string> { "userlocalconfigstore", "software", "valve", "steam", "apps", appId.ToLowerInvariant() };
      Stack<string> currentPath = new Stack<string>();
      string pendingKey = null;
      foreach (string rawLine in lines) {
        string line = rawLine.Trim();
        if (string.IsNullOrEmpty(line) || line.StartsWith("//")) continue;
        if (line == "{") {
          if (pendingKey != null) {
            currentPath.Push(pendingKey.ToLowerInvariant());
            pendingKey = null;
          } else currentPath.Push("<unknown>");
          continue;
        }
        if (line == "}") {
          if (currentPath.Count > 0) currentPath.Pop();
          continue;
        }
        var match = Regex.Match(line, "^\"(?<key>[^\"]+)\"(\\s*\"(?<value>[^\"]+)\")?$");
        if (match.Success) {
          string key = match.Groups["key"].Value;
          string value = match.Groups["value"].Success ? match.Groups["value"].Value : null;
          if (value == null) pendingKey = key;
          else {
            if (currentPath.Count == desiredPath.Count && currentPath.Reverse().SequenceEqual(desiredPath)) {
              if (key.Equals("PlayTime", StringComparison.OrdinalIgnoreCase)) int.TryParse(value, out stats.PlayTimeMinutes);
              else if (key.Equals("LastPlayed", StringComparison.OrdinalIgnoreCase)) long.TryParse(value, out stats.LastPlayedTime);
            }
            pendingKey = null;
          }
        }
      }
      return stats;
    }
  }
}

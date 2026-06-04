using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SAM.API;
namespace SLAM.Reborn {
  public class AchievementDefinition {
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string IconNormal { get; set; }
    public string IconLocked { get; set; }
    public int Permission { get; set; }
    public int IsHidden { get; set; }
  }
  public static class SteamDataService {
    public static List<AchievementDefinition> FetchAchievements(uint gameId, SAM.API.Client steamClient) {
      var definitions = new List<AchievementDefinition>();
      LoadUserGameStatsSchema(gameId, steamClient, definitions);
      if (definitions.Count == 0) {
        try {
          uint count = steamClient.SteamUserStats.GetNumAchievements();
          for (uint i = 0; i < count; i++) {
            string id = steamClient.SteamUserStats.GetAchievementName(i);
            if (!string.IsNullOrEmpty(id)) {
              definitions.Add(new AchievementDefinition {
                Id = id,
                Name = steamClient.SteamUserStats.GetAchievementDisplayAttribute(id, "name"),
                Description = steamClient.SteamUserStats.GetAchievementDisplayAttribute(id, "desc"),
                IsHidden = steamClient.SteamUserStats.GetAchievementDisplayAttribute(id, "hidden") == "1" ? 1 : 0,
                Permission = 0,
                IconNormal = "",
                IconLocked = ""
              });
            }
          }
        } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Get Game Config error: {ex.Message}"); }
      }
      return definitions;
    }
    private static bool LoadUserGameStatsSchema(uint gameId, SAM.API.Client steamClient, List<AchievementDefinition> definitions) {
      try {
        string installPath = SAM.API.Steam.GetInstallPath();
        string path = Path.Combine(installPath, "appcache", "stats", $"UserGameStatsSchema_{gameId}.bin");
        var kv = KeyValue.LoadAsBinary(path);
        if (kv == null) return false;
        var currentLanguage = steamClient.SteamApps008.GetCurrentGameLanguage() ?? "english";
        var stats = kv[gameId.ToString()]["stats"];
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
      } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Schema parsing error: {ex.Message}"); return false; }
    }
    private static string GetLocalizedString(KeyValue kv, string language, string defaultValue) {
      var name = kv[language].AsString("");
      if (!string.IsNullOrEmpty(name)) return name;
      if (language != "english") {
        name = kv["english"].AsString("");
        if (!string.IsNullOrEmpty(name)) return name;
      }
      return kv.AsString(defaultValue);
    }
  }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using SAM.API;
namespace SLAM.Reborn {
  public static class AppInfoReader {
    public static List<GameInfo> GetGames(string steamPath) {
      var games = new List<GameInfo>();
      var appInfoPath = Path.Combine(steamPath, "appcache", "appinfo.vdf");
      if (!File.Exists(appInfoPath)) return games;
      try {
        using (var fs = new FileStream(appInfoPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new BinaryReader(fs)) {
          var magic = reader.ReadUInt32();
          var version = magic & 0xFF;
          var magicHeader = magic >> 8;
          bool isNewFormat = (magicHeader == 0x075644);
          if (!isNewFormat) {
            if (magic != 0x27564446 && magic != 0x28564446 && magic != 0x29564446) throw new Exception($"Unknown magic: {magic:X}");
          }
          reader.ReadUInt32();
          string[] stringTable = null;
          if (isNewFormat && version >= 41) {
            var stringTableOffset = reader.ReadInt64();
            var currentPos = fs.Position;
            fs.Position = stringTableOffset;
            var stringCount = reader.ReadUInt32();
            stringTable = new string[stringCount];
            for (int i = 0; i < stringCount; i++) stringTable[i] = ReadNullTerminatedString(fs);
            fs.Position = currentPos;
          }
          while (fs.Position < fs.Length) {
            var appId = reader.ReadUInt32();
            if (appId == 0) break;
            var size = reader.ReadUInt32();
            var nextPos = fs.Position + size;
            if (nextPos > fs.Length) break;
            try {
              fs.Seek(40, SeekOrigin.Current);
              if (isNewFormat && version >= 40) fs.Seek(20, SeekOrigin.Current);
              var kv = new KeyValue();
              kv.ReadAsBinary(fs, stringTable);
              if (kv.Children != null && kv.Children.Count > 0) {
                var root = kv.Children[0];
                var common = root["common"];
                var type = common["type"].AsString("").ToLower();
                if (type == "game" || type == "dlc" || type == "demo" || type == "mod") {
                  bool hasAchievements = false;
                  if (root["achievements"].Valid) hasAchievements = true;
                  else if (root["common"]["achievements"].Valid) hasAchievements = true;
                  else if (root["extended"]["achievements"].Valid) hasAchievements = true;
                  if (!hasAchievements && common["community_visible_stats"].AsBoolean(false)) hasAchievements = true;
                  if (!hasAchievements && common["category"].Valid && common["category"].Children != null) {
                    foreach (var cat in common["category"].Children) {
                      if (cat.Name.Contains("22")) {
                        hasAchievements = true;
                        break;
                      }
                    }
                  }
                  var name = common["name"].AsString($"App {appId}");
                  string gameType = "normal";
                  if (type == "demo") gameType = "demo";
                  else if (type == "mod") gameType = "mod";
                  else if (type == "dlc") gameType = "dlc";
                  games.Add(new GameInfo { Id = appId, Name = name, Type = gameType, ImageUrl = $"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{appId}/header.jpg", HasAchievements = hasAchievements });
                }
              }
            } catch { }
            fs.Position = nextPos;
          }
        }
      } catch { }
      return games;
    }
    private static string ReadNullTerminatedString(Stream stream) {
      var bytes = new List<byte>();
      int b;
      while ((b = stream.ReadByte()) > 0) bytes.Add((byte)b);
      return Encoding.UTF8.GetString(bytes.ToArray());
    }
  }
}

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
namespace SAM.Picker.Modern {
  internal static class StreamHelpers {
    public static byte ReadValueU8(this Stream stream) {
      return (byte)stream.ReadByte();
    }
    public static int ReadValueS32(this Stream stream) {
      var data = new byte[4];
      int read = stream.Read(data, 0, 4);
      return BitConverter.ToInt32(data, 0);
    }
    public static uint ReadValueU32(this Stream stream) {
      var data = new byte[4];
      int read = stream.Read(data, 0, 4);
      return BitConverter.ToUInt32(data, 0);
    }
    public static ulong ReadValueU64(this Stream stream) {
      var data = new byte[8];
      int read = stream.Read(data, 0, 8);
      return BitConverter.ToUInt64(data, 0);
    }
    public static float ReadValueF32(this Stream stream) {
      var data = new byte[4];
      int read = stream.Read(data, 0, 4);
      return BitConverter.ToSingle(data, 0);
    }
    internal static string ReadStringInternalDynamic(this Stream stream, Encoding encoding, char end) {
      int characterSize = encoding.GetByteCount("e");
      string characterEnd = end.ToString(CultureInfo.InvariantCulture);
      int i = 0;
      var data = new byte[128 * characterSize];
      while (true) {
        if (i + characterSize > data.Length) {
          Array.Resize(ref data, data.Length + (128 * characterSize));
        }
        int read = stream.Read(data, i, characterSize);
        if (read != characterSize) break;
        if (encoding.GetString(data, i, characterSize) == characterEnd) {
          break;
        }
        i += characterSize;
      }
      if (i == 0) return "";
      return encoding.GetString(data, 0, i);
    }
    public static string ReadStringUnicode(this Stream stream) {
      return stream.ReadStringInternalDynamic(Encoding.UTF8, '\0');
    }
  }
  internal enum KeyValueType : byte {
    None = 0,
    String = 1,
    Int32 = 2,
    Float32 = 3,
    Pointer = 4,
    WideString = 5,
    Color = 6,
    UInt64 = 7,
    End = 8,
  }
  internal class KeyValue {
    private static readonly KeyValue _Invalid = new();
    public string Name = "<root>";
    public KeyValueType Type = KeyValueType.None;
    public object Value;
    public bool Valid;
    public System.Collections.Generic.List<KeyValue> Children = null;
    public KeyValue this[string key] {
      get {
        if (this.Children == null) return _Invalid;
        var child = System.Linq.Enumerable.SingleOrDefault(this.Children,
            c => string.Compare(c.Name, key, StringComparison.InvariantCultureIgnoreCase) == 0);
        return child ?? _Invalid;
      }
    }
    public string AsString(string defaultValue) {
      return (this.Valid && this.Value != null) ? this.Value.ToString() : defaultValue;
    }
    public int AsInteger(int defaultValue) {
      if (!this.Valid) return defaultValue;
      if (this.Type == KeyValueType.Int32) return (int)this.Value;
      if (this.Type == KeyValueType.String) return int.TryParse((string)this.Value, out int v) ? v : defaultValue;
      return defaultValue;
    }
    public float AsFloat(float defaultValue) {
      if (!this.Valid) return defaultValue;
      if (this.Type == KeyValueType.Float32) return (float)this.Value;
      return defaultValue;
    }
    public bool AsBoolean(bool defaultValue) {
      if (!this.Valid) return defaultValue;
      if (this.Type == KeyValueType.Int32) return (int)this.Value != 0;
      return defaultValue;
    }
    public static KeyValue LoadAsBinary(string path) {
      if (!File.Exists(path)) return null;
      try {
        using (var input = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
          KeyValue kv = new();
          return kv.ReadAsBinary(input) ? kv : null;
        }
      } catch { return null; }
    }
    public bool ReadAsBinary(Stream input) {
      this.Children = new System.Collections.Generic.List<KeyValue>();
      try {
        while (true) {
          var type = (KeyValueType)input.ReadValueU8();
          if (type == KeyValueType.End) break;
          KeyValue current = new() { Type = type, Name = input.ReadStringUnicode() };
          switch (type) {
            case KeyValueType.None: current.ReadAsBinary(input); break;
            case KeyValueType.String: current.Valid = true; current.Value = input.ReadStringUnicode(); break;
            case KeyValueType.Int32: current.Valid = true; current.Value = input.ReadValueS32(); break;
            case KeyValueType.UInt64: current.Valid = true; current.Value = input.ReadValueU64(); break;
            case KeyValueType.Float32: current.Valid = true; current.Value = input.ReadValueF32(); break;
            default: throw new FormatException();
          }
          this.Children.Add(current);
        }
        this.Valid = true;
        return true;
      } catch { return false; }
    }
  }
}

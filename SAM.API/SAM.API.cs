using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using SAM.API.Interfaces;
namespace SAM.API {
  public enum ClientInitializeFailure : byte {
    Unknown = 0,
    GetInstallPath,
    Load,
    CreateSteamClient,
    CreateSteamPipe,
    ConnectToGlobalUser,
    AppIdMismatch,
  }
  public enum CallHandle : ulong {
    Invalid = 0,
  }
  public class ClientInitializeException : Exception {
    public readonly ClientInitializeFailure Failure;
    public ClientInitializeException(ClientInitializeFailure failure) { Failure = failure; }
    public ClientInitializeException(ClientInitializeFailure failure, string message) : base(message) { Failure = failure; }
    public ClientInitializeException(ClientInitializeFailure failure, string message, Exception innerException) : base(message, innerException) { Failure = failure; }
  }
  public static class Types {
    public enum AccountType : int { Invalid = 0, Individual = 1, Multiset = 2, GameServer = 3, AnonGameServer = 4, Pending = 5, ContentServer = 6, Clan = 7, Chat = 8, P2PSuperSeeder = 9, }
    public enum ItemRequestResult : int { InvalidValue = -1, OK = 0, Denied = 1, ServerError = 2, Timeout = 3, Invalid = 4, NoMatch = 5, UnknownError = 6, NotLoggedOn = 7, }
    public enum UserStatType { Invalid = 0, Integer = 1, Float = 2, AverageRate = 3, Achievements = 4, GroupAchievements = 5, }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AppDataChanged {
      public uint Id;
      public bool Result;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CallbackMessage {
      public int User;
      public int Id;
      public IntPtr ParamPointer;
      public int ParamSize;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UserItemsReceived {
      public ulong GameId;
      public int Unknown;
      public int ItemCount;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UserStatsReceived {
      public ulong GameId;
      public int Result;
      public ulong SteamIdUser;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UserStatsStored {
      public ulong GameId;
      public int Result;
    }
  }
  public interface ICallback {
    int Id { get; }
    bool IsServer { get; }
    void Run(IntPtr param);
  }
  public abstract class Callback : ICallback {
    public delegate void CallbackFunction(IntPtr param);
    public event CallbackFunction OnRun;
    public abstract int Id { get; }
    public abstract bool IsServer { get; }
    public void Run(IntPtr param) {
      OnRun?.Invoke(param);
    }
  }
  public abstract class Callback<TParameter> : ICallback where TParameter : struct {
    public delegate void CallbackFunction(TParameter arg);
    public event CallbackFunction OnRun;
    public abstract int Id { get; }
    public abstract bool IsServer { get; }
    public void Run(IntPtr pvParam) {
      var data = Marshal.PtrToStructure<TParameter>(pvParam);
      OnRun?.Invoke(data);
    }
  }
  public class AppDataChanged : Callback<Types.AppDataChanged> {
    public override int Id => 1001;
    public override bool IsServer => false;
  }
  public class CallbackMessage : Callback<Types.CallbackMessage> {
    public override int Id => 1002;
    public override bool IsServer => false;
  }
  public class UserStatsReceived : Callback<Types.UserStatsReceived> {
    public override int Id => 1101;
    public override bool IsServer => false;
  }
  public class UserStatsStored : Callback<Types.UserStatsStored> {
    public override int Id => 1102;
    public override bool IsServer => false;
  }
  public class UserItemsReceived : Callback<Types.UserItemsReceived> {
    public override int Id => 1201;
    public override bool IsServer => false;
  }
  public interface INativeWrapper {
    void SetupFunctions(IntPtr objectAddress);
  }
  [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
  internal struct NativeClass {
    public IntPtr VirtualTable;
  }
  internal class NativeStrings {
    public sealed class StringHandle : SafeHandleZeroOrMinusOneIsInvalid {
      internal StringHandle(IntPtr preexistingHandle, bool ownsHandle) : base(ownsHandle) => SetHandle(preexistingHandle);
      public IntPtr Handle => handle;
      protected override bool ReleaseHandle() {
        if (handle == IntPtr.Zero) return false;
        Marshal.FreeHGlobal(handle);
        handle = IntPtr.Zero;
        return true;
      }
    }
    public static unsafe StringHandle StringToStringHandle(string value) {
      if (value == null) return new StringHandle(IntPtr.Zero, true);
      var bytes = Encoding.UTF8.GetBytes(value);
      var length = bytes.Length;
      var p = Marshal.AllocHGlobal(length + 1);
      Marshal.Copy(bytes, 0, p, bytes.Length);
      ((byte*)p)[length] = 0;
      return new StringHandle(p, true);
    }
    public static unsafe string PointerToString(sbyte* bytes) {
      if (bytes == null) return null;
      int running = 0;
      var b = bytes;
      if (*b == 0) return string.Empty;
      while ((*b++) != 0) running++;
      return new string(bytes, 0, running, Encoding.UTF8);
    }
    public static unsafe string PointerToString(byte* bytes) => PointerToString((sbyte*)bytes);
    public static unsafe string PointerToString(IntPtr nativeData) => PointerToString((sbyte*)nativeData.ToPointer());
    public static unsafe string PointerToString(sbyte* bytes, int length) {
      if (bytes == null) return null;
      int running = 0;
      var b = bytes;
      if (length == 0 || *b == 0) return string.Empty;
      while ((*b++) != 0 && running < length) running++;
      return new string(bytes, 0, running, Encoding.UTF8);
    }
    public static unsafe string PointerToString(byte* bytes, int length) => PointerToString((sbyte*)bytes, length);
    public static unsafe string PointerToString(IntPtr nativeData, int length) => PointerToString((sbyte*)nativeData.ToPointer(), length);
  }
  public abstract class NativeWrapper<TNativeFunctions> : INativeWrapper {
    protected IntPtr ObjectAddress;
    protected TNativeFunctions Functions;
    private readonly Dictionary<IntPtr, Delegate> _FunctionCache = new();
    public override string ToString() => $"Steam Interface<{typeof(TNativeFunctions)}> #{ObjectAddress.ToInt32():X8}";
    public void SetupFunctions(IntPtr objectAddress) {
      ObjectAddress = objectAddress;
      var iface = (NativeClass)Marshal.PtrToStructure(ObjectAddress, typeof(NativeClass));
      Functions = (TNativeFunctions)Marshal.PtrToStructure(iface.VirtualTable, typeof(TNativeFunctions));
    }
    protected Delegate GetDelegate<TDelegate>(IntPtr pointer) {
      if (_FunctionCache.TryGetValue(pointer, out var function) == false) {
        function = Marshal.GetDelegateForFunctionPointer(pointer, typeof(TDelegate));
        _FunctionCache[pointer] = function;
      }
      return function;
    }
    protected TDelegate GetFunction<TDelegate>(IntPtr pointer) where TDelegate : class => (TDelegate)(object)GetDelegate<TDelegate>(pointer);
    protected void Call<TDelegate>(IntPtr pointer, params object[] args) => GetDelegate<TDelegate>(pointer).DynamicInvoke(args);
    protected TReturn Call<TReturn, TDelegate>(IntPtr pointer, params object[] args) => (TReturn)GetDelegate<TDelegate>(pointer).DynamicInvoke(args);
  }
  public static class Steam {
    private struct Native {
      [DllImport("kernel32.dll", SetLastError = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
      internal static extern IntPtr GetProcAddress(IntPtr module, string name);
      [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
      internal static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
      [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
      [return: MarshalAs(UnmanagedType.Bool)]
      internal static extern bool SetDllDirectory(string path);
      [DllImport("kernel32.dll", SetLastError = true)]
      [return: MarshalAs(UnmanagedType.Bool)]
      internal static extern bool FreeLibrary(IntPtr module);
      internal const uint LoadWithAlteredSearchPath = 8;
    }
    private static IntPtr _Handle = IntPtr.Zero;
    private static NativeCreateInterface _CallCreateInterface;
    private static NativeSteamGetCallback _CallSteamBGetCallback;
    private static NativeSteamFreeLastCallback _CallSteamFreeLastCallback;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate IntPtr NativeCreateInterface(string version, IntPtr returnCode);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool NativeSteamGetCallback(int pipe, out Types.CallbackMessage message, out int call);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool NativeSteamFreeLastCallback(int pipe);
    public static bool Load() {
      if (_Handle != IntPtr.Zero) return true;
      string path = GetInstallPath();
      if (path == null) return false;
      Native.SetDllDirectory(path + ";" + Path.Combine(path, "bin"));
      path = Path.Combine(path, "steamclient.dll");
      IntPtr module = Native.LoadLibraryEx(path, IntPtr.Zero, Native.LoadWithAlteredSearchPath);
      if (module == IntPtr.Zero) return false;
      _CallCreateInterface = GetExportFunction<NativeCreateInterface>(module, "CreateInterface");
      if (_CallCreateInterface == null) return false;
      _CallSteamBGetCallback = GetExportFunction<NativeSteamGetCallback>(module, "Steam_BGetCallback");
      if (_CallSteamBGetCallback == null) return false;
      _CallSteamFreeLastCallback = GetExportFunction<NativeSteamFreeLastCallback>(module, "Steam_FreeLastCallback");
      if (_CallSteamFreeLastCallback == null) return false;
      _Handle = module;
      return true;
    }
    public static void Unload() {
      if (_Handle == IntPtr.Zero) return;
      Native.FreeLibrary(_Handle);
      _Handle = IntPtr.Zero;
      _CallCreateInterface = null;
      _CallSteamBGetCallback = null;
      _CallSteamFreeLastCallback = null;
    }
    public static string GetInstallPath() =>
          (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\Software\Valve\Steam", "InstallPath", null) ??
          (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\Software\Wow6432Node\Valve\Steam", "InstallPath", null);
    public static TClass CreateInterface<TClass>(string version) where TClass : INativeWrapper, new() {
      IntPtr address = _CallCreateInterface(version, IntPtr.Zero);
      if (address == IntPtr.Zero) return default;
      TClass instance = new();
      instance.SetupFunctions(address);
      return instance;
    }
    public static bool GetCallback(int pipe, out Types.CallbackMessage message, out int call) =>
      _CallSteamBGetCallback(pipe, out message, out call);
    public static bool FreeLastCallback(int pipe) => _CallSteamFreeLastCallback(pipe);
    private static Delegate GetExportDelegate<TDelegate>(IntPtr module, string name) {
      IntPtr address = Native.GetProcAddress(module, name);
      return address == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer(address, typeof(TDelegate));
    }
    private static TDelegate GetExportFunction<TDelegate>(IntPtr module, string name) where TDelegate : class =>
      (TDelegate)(object)GetExportDelegate<TDelegate>(module, name);
  }
  public static class Wrappers {
    public class SteamApps001 : NativeWrapper<ISteamApps001> {
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      private delegate int NativeGetAppData(IntPtr self, uint appId, IntPtr key, IntPtr value, int valueLength);
      public string GetAppData(uint appId, string key) {
        using (var nativeHandle = NativeStrings.StringToStringHandle(key)) {
          const int valueLength = 1024;
          var valuePointer = Marshal.AllocHGlobal(valueLength);
          int result = Call<int, NativeGetAppData>(Functions.GetAppData, ObjectAddress, appId, nativeHandle.Handle, valuePointer, valueLength);
          var value = result == 0 ? null : NativeStrings.PointerToString(valuePointer, valueLength);
          Marshal.FreeHGlobal(valuePointer);
          return value;
        }
      }
    }
    public class SteamApps008 : NativeWrapper<ISteamApps008> {
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      [return: MarshalAs(UnmanagedType.I1)]
      private delegate bool NativeIsSubscribedApp(IntPtr self, uint gameId);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      private delegate IntPtr NativeGetCurrentGameLanguage(IntPtr self);
      public bool IsSubscribedApp(uint gameId) => Call<bool, NativeIsSubscribedApp>(Functions.IsSubscribedApp, ObjectAddress, gameId);
      public string GetCurrentGameLanguage() => NativeStrings.PointerToString(Call<IntPtr, NativeGetCurrentGameLanguage>(Functions.GetCurrentGameLanguage, ObjectAddress));
    }
    public class SteamClient018 : NativeWrapper<ISteamClient018> {
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      private delegate int NativeCreateSteamPipe(IntPtr self);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      [return: MarshalAs(UnmanagedType.I1)]
      private delegate bool NativeReleaseSteamPipe(IntPtr self, int pipe);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      private delegate int NativeCreateLocalUser(IntPtr self, ref int pipe, Types.AccountType type);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      private delegate int NativeConnectToGlobalUser(IntPtr self, int pipe);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      private delegate void NativeReleaseUser(IntPtr self, int pipe, int user);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      private delegate void NativeSetLocalIPBinding(IntPtr self, uint host, ushort port);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      private delegate IntPtr NativeGetISteamUser(IntPtr self, int user, int pipe, IntPtr version);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      private delegate IntPtr NativeGetISteamUserStats(IntPtr self, int user, int pipe, IntPtr version);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      private delegate IntPtr NativeGetISteamUtils(IntPtr self, int pipe, IntPtr version);
      private delegate IntPtr NativeGetISteamApps(int user, int pipe, IntPtr version);
      public int CreateSteamPipe() => Call<int, NativeCreateSteamPipe>(Functions.CreateSteamPipe, ObjectAddress);
      public bool ReleaseSteamPipe(int pipe) => Call<bool, NativeReleaseSteamPipe>(Functions.ReleaseSteamPipe, ObjectAddress, pipe);
      public int CreateLocalUser(ref int pipe, Types.AccountType type) => GetFunction<NativeCreateLocalUser>(Functions.CreateLocalUser)(ObjectAddress, ref pipe, type);
      public int ConnectToGlobalUser(int pipe) => Call<int, NativeConnectToGlobalUser>(Functions.ConnectToGlobalUser, ObjectAddress, pipe);
      public void ReleaseUser(int pipe, int user) => Call<NativeReleaseUser>(Functions.ReleaseUser, ObjectAddress, pipe, user);
      public void SetLocalIPBinding(uint host, ushort port) => Call<NativeSetLocalIPBinding>(Functions.SetLocalIPBinding, ObjectAddress, host, port);
      public SteamUser012 GetSteamUser012(int user, int pipe) => GetISteamUser<SteamUser012>(user, pipe, "SteamUser012");
      private TClass GetISteamUser<TClass>(int user, int pipe, string version) where TClass : INativeWrapper, new() {
        using (var nativeVersion = NativeStrings.StringToStringHandle(version)) {
          IntPtr address = Call<IntPtr, NativeGetISteamUser>(Functions.GetISteamUser, ObjectAddress, user, pipe, nativeVersion.Handle);
          TClass result = new();
          result.SetupFunctions(address);
          return result;
        }
      }
      public SteamUserStats013 GetSteamUserStats013(int user, int pipe) => GetISteamUserStats<SteamUserStats013>(user, pipe, "STEAMUSERSTATS_INTERFACE_VERSION013");
      private TClass GetISteamUserStats<TClass>(int user, int pipe, string version) where TClass : INativeWrapper, new() {
        using (var nativeVersion = NativeStrings.StringToStringHandle(version)) {
          IntPtr address = Call<IntPtr, NativeGetISteamUserStats>(Functions.GetISteamUserStats, ObjectAddress, user, pipe, nativeVersion.Handle);
          TClass result = new();
          result.SetupFunctions(address);
          return result;
        }
      }
      public SteamUtils005 GetSteamUtils004(int pipe) => GetISteamUtils<SteamUtils005>(pipe, "SteamUtils005");
      private TClass GetISteamUtils<TClass>(int pipe, string version) where TClass : INativeWrapper, new() {
        using (var nativeVersion = NativeStrings.StringToStringHandle(version)) {
          IntPtr address = Call<IntPtr, NativeGetISteamUtils>(Functions.GetISteamUtils, ObjectAddress, pipe, nativeVersion.Handle);
          TClass result = new();
          result.SetupFunctions(address);
          return result;
        }
      }
      public SteamApps001 GetSteamApps001(int user, int pipe) => GetISteamApps<SteamApps001>(user, pipe, "STEAMAPPS_INTERFACE_VERSION001");
      public SteamApps008 GetSteamApps008(int user, int pipe) => GetISteamApps<SteamApps008>(user, pipe, "STEAMAPPS_INTERFACE_VERSION008");
      private TClass GetISteamApps<TClass>(int user, int pipe, string version) where TClass : INativeWrapper, new() {
        using (var nativeVersion = NativeStrings.StringToStringHandle(version)) {
          IntPtr address = Call<IntPtr, NativeGetISteamApps>(Functions.GetISteamApps, user, pipe, nativeVersion.Handle);
          TClass result = new();
          result.SetupFunctions(address);
          return result;
        }
      }
    }
    public class SteamUser012 : NativeWrapper<ISteamUser012> {
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      [return: MarshalAs(UnmanagedType.I1)]
      private delegate bool NativeLoggedOn(IntPtr self);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      private delegate void NativeGetSteamId(IntPtr self, out ulong steamId);
      public bool IsLoggedIn() => Call<bool, NativeLoggedOn>(Functions.LoggedOn, ObjectAddress);
      public ulong GetSteamId() {
        GetFunction<NativeGetSteamId>(Functions.GetSteamID)(ObjectAddress, out ulong steamId);
        return steamId;
      }
    }
    public class SteamUserStats013 : NativeWrapper<ISteamUserStats013> {
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      [return: MarshalAs(UnmanagedType.I1)]
      private delegate bool NativeGetStatInt(IntPtr self, IntPtr name, out int data);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      [return: MarshalAs(UnmanagedType.I1)]
      private delegate bool NativeGetStatFloat(IntPtr self, IntPtr name, out float data);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      [return: MarshalAs(UnmanagedType.I1)]
      private delegate bool NativeSetStatInt(IntPtr self, IntPtr name, int data);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      [return: MarshalAs(UnmanagedType.I1)]
      private delegate bool NativeSetStatFloat(IntPtr self, IntPtr name, float data);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      [return: MarshalAs(UnmanagedType.I1)]
      private delegate bool NativeGetAchievement(IntPtr self, IntPtr name, [MarshalAs(UnmanagedType.I1)] out bool isAchieved);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      [return: MarshalAs(UnmanagedType.I1)]
      private delegate bool NativeSetAchievement(IntPtr self, IntPtr name);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      [return: MarshalAs(UnmanagedType.I1)]
      private delegate bool NativeClearAchievement(IntPtr self, IntPtr name);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      [return: MarshalAs(UnmanagedType.I1)]
      private delegate bool NativeGetAchievementAndUnlockTime(IntPtr self, IntPtr name, [MarshalAs(UnmanagedType.I1)] out bool isAchieved, out uint unlockTime);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      [return: MarshalAs(UnmanagedType.I1)]
      private delegate bool NativeStoreStats(IntPtr self);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      private delegate int NativeGetAchievementIcon(IntPtr self, IntPtr name);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      private delegate IntPtr NativeGetAchievementDisplayAttribute(IntPtr self, IntPtr name, IntPtr key);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      private delegate CallHandle NativeRequestUserStats(IntPtr self, ulong steamIdUser);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      private delegate CallHandle NativeRequestGlobalAchievementPercentages(IntPtr self);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      [return: MarshalAs(UnmanagedType.I1)]
      private delegate bool NativeGetAchievementAchievedPercent(IntPtr self, IntPtr name, out float percent);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      [return: MarshalAs(UnmanagedType.I1)]
      private delegate bool NativeResetAllStats(IntPtr self, [MarshalAs(UnmanagedType.I1)] bool achievementsToo);
      public bool GetStatValue(string name, out int value) {
        using (var nativeName = NativeStrings.StringToStringHandle(name)) return GetFunction<NativeGetStatInt>(Functions.GetStatInteger)(ObjectAddress, nativeName.Handle, out value);
      }
      public bool GetStatValue(string name, out float value) {
        using (var nativeName = NativeStrings.StringToStringHandle(name)) return GetFunction<NativeGetStatFloat>(Functions.GetStatFloat)(ObjectAddress, nativeName.Handle, out value);
      }
      public bool SetStatValue(string name, int value) {
        using (var nativeName = NativeStrings.StringToStringHandle(name)) return Call<bool, NativeSetStatInt>(Functions.SetStatInteger, ObjectAddress, nativeName.Handle, value);
      }
      public bool SetStatValue(string name, float value) {
        using (var nativeName = NativeStrings.StringToStringHandle(name)) return Call<bool, NativeSetStatFloat>(Functions.SetStatFloat, ObjectAddress, nativeName.Handle, value);
      }
      public bool GetAchievement(string name, out bool isAchieved) {
        using (var nativeName = NativeStrings.StringToStringHandle(name)) return GetFunction<NativeGetAchievement>(Functions.GetAchievement)(ObjectAddress, nativeName.Handle, out isAchieved);
      }
      public bool SetAchievement(string name, bool state) {
        using (var nativeName = NativeStrings.StringToStringHandle(name)) {
          if (state) return Call<bool, NativeSetAchievement>(Functions.SetAchievement, ObjectAddress, nativeName.Handle);
          return Call<bool, NativeClearAchievement>(Functions.ClearAchievement, ObjectAddress, nativeName.Handle);
        }
      }
      public bool GetAchievementAndUnlockTime(string name, out bool isAchieved, out uint unlockTime) {
        using (var nativeName = NativeStrings.StringToStringHandle(name)) return GetFunction<NativeGetAchievementAndUnlockTime>(Functions.GetAchievementAndUnlockTime)(ObjectAddress, nativeName.Handle, out isAchieved, out unlockTime);
      }
      public bool StoreStats() => Call<bool, NativeStoreStats>(Functions.StoreStats, ObjectAddress);
      public int GetAchievementIcon(string name) {
        using (var nativeName = NativeStrings.StringToStringHandle(name)) return GetFunction<NativeGetAchievementIcon>(Functions.GetAchievementIcon)(ObjectAddress, nativeName.Handle);
      }
      public string GetAchievementDisplayAttribute(string name, string key) {
        using (var nativeName = NativeStrings.StringToStringHandle(name))
        using (var nativeKey = NativeStrings.StringToStringHandle(key)) {
          return NativeStrings.PointerToString(Call<IntPtr, NativeGetAchievementDisplayAttribute>(Functions.GetAchievementDisplayAttribute, ObjectAddress, nativeName.Handle, nativeKey.Handle));
        }
      }
      public CallHandle RequestUserStats(ulong steamIdUser) => Call<CallHandle, NativeRequestUserStats>(Functions.RequestUserStats, ObjectAddress, steamIdUser);
      public CallHandle RequestGlobalAchievementPercentages() => Call<CallHandle, NativeRequestGlobalAchievementPercentages>(Functions.RequestGlobalAchievementPercentages, ObjectAddress);
      public bool GetAchievementAchievedPercent(string name, out float percent) {
        using (var nativeName = NativeStrings.StringToStringHandle(name)) return GetFunction<NativeGetAchievementAchievedPercent>(Functions.GetAchievementAchievedPercent)(ObjectAddress, nativeName.Handle, out percent);
      }
      public bool ResetAllStats(bool achievementsToo) => Call<bool, NativeResetAllStats>(Functions.ResetAllStats, ObjectAddress, achievementsToo);
    }
    public class SteamUtils005 : NativeWrapper<ISteamUtils005> {
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      private delegate int NativeGetConnectedUniverse(IntPtr self);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      private delegate IntPtr NativeGetIPCountry(IntPtr self);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      [return: MarshalAs(UnmanagedType.I1)]
      private delegate bool NativeGetImageSize(IntPtr self, int index, out int width, out int height);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      [return: MarshalAs(UnmanagedType.I1)]
      private delegate bool NativeGetImageRGBA(IntPtr self, int index, byte[] buffer, int length);
      [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
      private delegate uint NativeGetAppId(IntPtr self);
      public int GetConnectedUniverse() => Call<int, NativeGetConnectedUniverse>(Functions.GetConnectedUniverse, ObjectAddress);
      public string GetIPCountry() => NativeStrings.PointerToString(Call<IntPtr, NativeGetIPCountry>(Functions.GetIPCountry, ObjectAddress));
      public bool GetImageSize(int index, out int width, out int height) => GetFunction<NativeGetImageSize>(Functions.GetImageSize)(ObjectAddress, index, out width, out height);
      public bool GetImageRGBA(int index, byte[] data) {
        if (data == null) throw new ArgumentNullException(nameof(data));
        return GetFunction<NativeGetImageRGBA>(Functions.GetImageRGBA)(ObjectAddress, index, data, data.Length);
      }
      public uint GetAppId() => Call<uint, NativeGetAppId>(Functions.GetAppID, ObjectAddress);
    }
  }
  public class Client : IDisposable {
    public Wrappers.SteamClient018 SteamClient;
    public Wrappers.SteamUser012 SteamUser;
    public Wrappers.SteamUserStats013 SteamUserStats;
    public Wrappers.SteamUtils005 SteamUtils;
    public Wrappers.SteamApps001 SteamApps001;
    public Wrappers.SteamApps008 SteamApps008;
    private bool _IsDisposed = false;
    private int _Pipe;
    private int _User;
    private readonly List<ICallback> _Callbacks = new();
    private bool _RunningCallbacks;
    ~Client() => Dispose(false);
    public void Initialize(long appId) {
      if (string.IsNullOrEmpty(Steam.GetInstallPath())) throw new ClientInitializeException(ClientInitializeFailure.GetInstallPath, "failed to get Steam install path");
      if (appId != 0) Environment.SetEnvironmentVariable("SteamAppId", appId.ToString(CultureInfo.InvariantCulture));
      if (!Steam.Load()) throw new ClientInitializeException(ClientInitializeFailure.Load, "failed to load SteamClient");
      SteamClient = Steam.CreateInterface<Wrappers.SteamClient018>("SteamClient018");
      if (SteamClient == null) throw new ClientInitializeException(ClientInitializeFailure.CreateSteamClient, "failed to create ISteamClient018");
      _Pipe = SteamClient.CreateSteamPipe();
      if (_Pipe == 0) throw new ClientInitializeException(ClientInitializeFailure.CreateSteamPipe, "failed to create pipe");
      _User = SteamClient.ConnectToGlobalUser(_Pipe);
      if (_User == 0) throw new ClientInitializeException(ClientInitializeFailure.ConnectToGlobalUser, "failed to connect to global user");
      SteamUtils = SteamClient.GetSteamUtils004(_Pipe);
      if (appId > 0 && SteamUtils.GetAppId() != (uint)appId) throw new ClientInitializeException(ClientInitializeFailure.AppIdMismatch, "appID mismatch");
      SteamUser = SteamClient.GetSteamUser012(_User, _Pipe);
      SteamUserStats = SteamClient.GetSteamUserStats013(_User, _Pipe);
      SteamApps001 = SteamClient.GetSteamApps001(_User, _Pipe);
      SteamApps008 = SteamClient.GetSteamApps008(_User, _Pipe);
    }
    public void RunCallbacks(bool server) {
      if (_RunningCallbacks) return;
      _RunningCallbacks = true;
      Types.CallbackMessage message;
      while (Steam.GetCallback(_Pipe, out message, out _)) {
        var callbackId = message.Id;
        foreach (ICallback callback in _Callbacks.Where(candidate => candidate.Id == callbackId && candidate.IsServer == server)) callback.Run(message.ParamPointer);
        Steam.FreeLastCallback(_Pipe);
      }
      _RunningCallbacks = false;
    }
    public TCallback CreateAndRegisterCallback<TCallback>() where TCallback : ICallback, new() {
      TCallback callback = new();
      _Callbacks.Add(callback);
      return callback;
    }
    public void Dispose() {
      Dispose(true);
      GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing) {
      if (_IsDisposed) return;
      if (SteamClient != null && _Pipe > 0) {
        if (_User > 0) {
          SteamClient.ReleaseUser(_Pipe, _User);
          _User = 0;
        }
        SteamClient.ReleaseSteamPipe(_Pipe);
        _Pipe = 0;
      }
      _IsDisposed = true;
    }
  }
  public static class StreamHelpers {
    public static byte ReadValueU8(this Stream stream) => (byte)stream.ReadByte();
    public static int ReadValueS32(this Stream stream) { var data = new byte[4]; stream.Read(data, 0, 4); return BitConverter.ToInt32(data, 0); }
    public static uint ReadValueU32(this Stream stream) { var data = new byte[4]; stream.Read(data, 0, 4); return BitConverter.ToUInt32(data, 0); }
    public static ulong ReadValueU64(this Stream stream) { var data = new byte[8]; stream.Read(data, 0, 8); return BitConverter.ToUInt64(data, 0); }
    public static float ReadValueF32(this Stream stream) { var data = new byte[4]; stream.Read(data, 0, 4); return BitConverter.ToSingle(data, 0); }
    public static string ReadStringInternalDynamic(this Stream stream, Encoding encoding, char end) {
      int characterSize = encoding.GetByteCount("e");
      string characterEnd = end.ToString(CultureInfo.InvariantCulture);
      int i = 0;
      var data = new byte[128 * characterSize];
      while (true) {
        if (i + characterSize > data.Length) Array.Resize(ref data, data.Length + (128 * characterSize));
        int read = stream.Read(data, i, characterSize);
        if (read != characterSize) break;
        if (encoding.GetString(data, i, characterSize) == characterEnd) break;
        i += characterSize;
      }
      return i == 0 ? "" : encoding.GetString(data, 0, i);
    }
    public static string ReadStringUnicode(this Stream stream) => stream.ReadStringInternalDynamic(Encoding.UTF8, '\0');
  }
  public enum KeyValueType : byte { None = 0, String = 1, Int32 = 2, Float32 = 3, Pointer = 4, WideString = 5, Color = 6, UInt64 = 7, End = 8 }
  public class KeyValue {
    private static readonly KeyValue _Invalid = new();
    public string Name = "<root>"; public KeyValueType Type = KeyValueType.None; public object Value; public bool Valid; public List<KeyValue> Children = null;
    public KeyValue this[string key] {
      get {
        if (Children == null) return _Invalid;
        return Children.SingleOrDefault(c => string.Compare(c.Name, key, StringComparison.InvariantCultureIgnoreCase) == 0) ?? _Invalid;
      }
    }
    public string AsString(string defaultValue) => (Valid && Value != null) ? Value.ToString() : defaultValue;
    public int AsInteger(int defaultValue) {
      if (!Valid) return defaultValue;
      if (Type == KeyValueType.Int32) return (int)Value;
      if (Type == KeyValueType.String) return int.TryParse((string)Value, out int v) ? v : defaultValue;
      return defaultValue;
    }
    public float AsFloat(float defaultValue) {
      if (!Valid) return defaultValue;
      if (Type == KeyValueType.Float32) return (float)Value;
      return defaultValue;
    }
    public bool AsBoolean(bool defaultValue) {
      if (!Valid) return defaultValue;
      if (Type == KeyValueType.Int32) return (int)Value != 0;
      return defaultValue;
    }
    public static KeyValue LoadAsBinary(string path) {
      if (!File.Exists(path)) return null;
      try { using (var input = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { KeyValue kv = new(); kv.ReadAsBinary(input); return kv; } } catch { return null; }
    }
    public void ReadAsBinary(Stream input, string[] stringTable = null) {
      Children = new List<KeyValue>();
      while (true) {
        var type = (KeyValueType)input.ReadValueU8();
        if (type == KeyValueType.End) break;
        string name;
        if (stringTable != null) {
          var index = input.ReadValueS32();
          name = (index >= 0 && index < stringTable.Length) ? stringTable[index] : index.ToString();
        } else name = input.ReadStringUnicode();
        KeyValue current = new() { Type = type, Name = name };
        switch (type) {
          case KeyValueType.None: current.ReadAsBinary(input, stringTable); break;
          case KeyValueType.String: current.Valid = true; current.Value = input.ReadStringUnicode(); break;
          case KeyValueType.Int32: current.Valid = true; current.Value = input.ReadValueS32(); break;
          case KeyValueType.UInt64: current.Valid = true; current.Value = input.ReadValueU64(); break;
          case KeyValueType.Float32: current.Valid = true; current.Value = input.ReadValueF32(); break;
          default: throw new FormatException($"Unknown KV type: {type} at position {input.Position}");
        }
        Children.Add(current);
      }
      Valid = true;
    }
  }
}
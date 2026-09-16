using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LyricsChatbox;

// Only per-session ISimpleAudioVolume; never obtain IAudioEndpointVolume (system master).
public sealed record MusicVolume(int ProcessId, string SessionId, float Level);
public sealed class AppleMusicPlaybackVolume : IPlaybackVolume
{
    public MusicVolume? Read() => AppleMusicVolume.Read();
    public bool Set(MusicVolume expected, float level) => AppleMusicVolume.Set(expected, level);
}
public sealed class SpotifyPlaybackVolume : IPlaybackVolume
{
    public MusicVolume? Read() => SpotifyMusicVolume.Read();
    public bool Set(MusicVolume expected, float level) => SpotifyMusicVolume.Set(expected, level);
}
public static class SpotifyMusicVolume
{
    public static MusicVolume? Read() => AppleMusicVolume.Access(null, null, IsSpotify);
    public static bool Set(MusicVolume expected, float level) => float.IsFinite(level) && level is >= 0 and <= 1 &&
        AppleMusicVolume.Access(expected, level, IsSpotify) is not null;
    public static MusicVolume? SelectSession(IEnumerable<(MusicVolume State, bool Active)> sessions, MusicVolume? expected = null) =>
        AppleMusicVolume.SelectSession(sessions, expected);
    private static bool IsSpotify(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return AppleMusicVolume.PackageFamily(process.Handle) == "SpotifyAB.SpotifyMusic_zpdnekdrzrea0";
        }
        catch { return false; }
    }
}
public static class AppleMusicVolume
{
    public static MusicVolume? Read() => Access(null, null, IsAppleMusic);
    public static bool Set(MusicVolume expected, float level) => float.IsFinite(level) && level is >= 0 and <= 1 && Access(expected, level, IsAppleMusic) is not null;
    public static MusicVolume? SelectSession(IEnumerable<(MusicVolume State, bool Active)> sessions, MusicVolume? expected = null)
    {
        var candidates = sessions.ToArray();
        var active = candidates.Where(candidate => candidate.Active).ToArray();
        if (active.Length > 0) candidates = active;
        if (candidates.Length != 1) return null;
        var selected = candidates[0].State;
        return expected is null || (selected.ProcessId == expected.ProcessId && selected.SessionId == expected.SessionId)
            ? selected : null;
    }
    internal static MusicVolume? Access(MusicVolume? expected, float? level, Func<int, bool> ownsProcess)
    {
        var owned = new List<object>();
        T Own<T>(T value) where T : class { owned.Add(value); return value; }
        try
        {
            var enumerator = Own((IDeviceEnumerator)new DeviceEnumerator());
            Check(enumerator.EnumAudioEndpoints(0, 1, out var collection)); Own(collection);
            Check(collection.GetCount(out var devices));
            if (devices > 32) return null;
            var matches = new List<(ISessionControl2 Control, IVolume Volume, MusicVolume State, bool Active)>();
            for (uint d = 0; d < devices; d++)
            {
                Check(collection.Item(d, out var device)); Own(device);
                var iid = typeof(ISessionManager).GUID;
                Check(device.Activate(ref iid, 23, IntPtr.Zero, out var managerObject)); Own(managerObject);
                var manager = (ISessionManager)managerObject;
                Check(manager.GetSessionEnumerator(out var sessions)); Own(sessions);
                Check(sessions.GetCount(out var count)); if (count > 128) return null;
                for (var i = 0; i < count; i++)
                {
                    Check(sessions.GetSession(i, out var controlObject)); Own(controlObject);
                    var control = (ISessionControl2)controlObject;
                    var processResult = control.GetProcessId(out var pid);
                    // Apple's active renderer uses a cross-process session. This success code still
                    // identifies its creating process; require the installed Apple package below.
                    if (processResult is not (0 or 0x0889000D) || pid == 0 || !ownsProcess((int)pid)) continue;
                    if (control.GetState(out var state) != 0 || state == 2) continue;
                    Check(control.GetSessionInstanceIdentifier(out var id));
                    var volume = (IVolume)controlObject;
                    Check(volume.GetMasterVolume(out var current));
                    matches.Add((control, volume, new((int)pid, id, current), state == 1));
                }
            }
            // Ambiguous audio sessions fail closed instead of moving another endpoint/session.
            var selected = SelectSession(matches.Select(match => (match.State, match.Active)), expected);
            if (selected is null) return null;
            var match = matches.Single(match => ReferenceEquals(match.State, selected));
            if (level.HasValue)
            {
                if (!ownsProcess(match.State.ProcessId)) return null;
                var context = Guid.Empty; Check(match.Volume.SetMasterVolume(level.Value, ref context));
                Check(match.Volume.GetMasterVolume(out var confirmed));
                return Math.Abs(confirmed - level.Value) < 0.001f ? match.State with { Level = confirmed } : null;
            }
            return match.State;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
        finally { for (var i = owned.Count - 1; i >= 0; i--) if (Marshal.IsComObject(owned[i])) Marshal.ReleaseComObject(owned[i]); }
    }
    private static bool IsAppleMusic(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (process.ProcessName is not ("AppleMusic" or "AMPLibraryAgent")) return false;
            return PackageFamily(process.Handle) == "AppleInc.AppleMusicWin_nzyj5cx40ttqa";
        }
        catch { return false; }
    }
    internal static string? PackageFamily(IntPtr handle)
    {
        uint length = 0;
        if (GetPackageFamilyName(handle, ref length, null) != 122 || length > 256) return null;
        var name = new System.Text.StringBuilder((int)length);
        return GetPackageFamilyName(handle, ref length, name) == 0 ? name.ToString() : null;
    }
    private static void Check(int result) => Marshal.ThrowExceptionForHR(result);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetPackageFamilyName(IntPtr process, ref uint length, System.Text.StringBuilder? name);
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] private class DeviceEnumerator { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDeviceEnumerator { [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out IDeviceCollection devices); }
    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDeviceCollection { [PreserveSig] int GetCount(out uint count); [PreserveSig] int Item(uint index, out IDevice device); }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDevice { [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object result); }
    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISessionManager
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr guid, uint flags, out IntPtr control);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr guid, uint flags, out IntPtr volume);
        [PreserveSig] int GetSessionEnumerator(out ISessionEnumerator sessions);
    }
    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, [MarshalAs(UnmanagedType.IUnknown)] out object control);
    }
    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISessionControl2
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid context);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid context);
        [PreserveSig] int GetGroupingParam(out Guid group);
        [PreserveSig] int SetGroupingParam(ref Guid group, ref Guid context);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr events);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr events);
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetProcessId(out uint pid);
    }
    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IVolume
    {
        [PreserveSig] int SetMasterVolume(float level, ref Guid context);
        [PreserveSig] int GetMasterVolume(out float level);
    }
}



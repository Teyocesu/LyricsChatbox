// Read-only Windows Core Audio session enumeration. No volume interface or setter is acquired.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

internal static class SpotifyAudioSessions
{
    public static string ReadOnlySummary()
    {
        var owned = new List<object>();
        T Own<T>(T value) where T : class { owned.Add(value); return value; }
        try
        {
            var enumerator = Own((IDeviceEnumerator)new DeviceEnumerator());
            Check(enumerator.EnumAudioEndpoints(0, 1, out var collection)); Own(collection);
            Check(collection.GetCount(out var devices));
            if (devices > 32) return "ambiguous: endpoint bound exceeded";
            var found = new List<string>();
            for (uint d = 0; d < devices; d++)
            {
                Check(collection.GetItem(d, out var device)); Own(device);
                var iid = typeof(ISessionManager).GUID;
                Check(device.Activate(ref iid, 23, IntPtr.Zero, out var managerObject)); Own(managerObject);
                var manager = (ISessionManager)managerObject;
                Check(manager.GetSessionEnumerator(out var sessions)); Own(sessions);
                Check(sessions.GetCount(out var count));
                if (count > 128) return "ambiguous: audio-session bound exceeded";
                for (var i = 0; i < count; i++)
                {
                    Check(sessions.GetSession(i, out var controlObject)); Own(controlObject);
                    var control = (ISessionControl2)controlObject;
                    var pidResult = control.GetProcessId(out var pid);
                    if (pidResult is not (0 or 0x0889000D) || pid == 0 || !IsInstalledSpotify((int)pid)) continue;
                    if (control.GetState(out var state) != 0 || state == 2) continue;
                    Check(control.GetSessionInstanceIdentifier(out var id));
                    found.Add($"endpoint {d}; pid {pid}; state {(state == 1 ? "active" : "inactive")}; audio-session {id}");
                }
            }
            var active = found.Where(s => s.Contains("state active", StringComparison.Ordinal)).ToArray();
            var relevant = active.Length > 0 ? active : found.ToArray();
            return relevant.Length switch
            {
                0 => "no: zero local Spotify audio sessions found",
                1 => "yes: one local Spotify audio session; " + relevant[0],
                _ => "ambiguous: " + relevant.Length + " Spotify audio sessions; " + string.Join(" | ", relevant.Take(8))
            };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { return "ambiguous: Core Audio read error " + ex.GetType().Name + ": " + ex.Message; }
        finally
        {
            for (var i = owned.Count - 1; i >= 0; i--)
                if (Marshal.IsComObject(owned[i])) Marshal.ReleaseComObject(owned[i]);
        }
    }
    private static bool IsInstalledSpotify(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            if (p.ProcessName != "Spotify") return false;
            uint length = 0;
            if (GetPackageFamilyName(p.Handle, ref length, null) != 122 || length > 256) return false;
            var name = new StringBuilder((int)length);
            return GetPackageFamilyName(p.Handle, ref length, name) == 0 &&
                name.ToString() == "SpotifyAB.SpotifyMusic_zpdnekdrzrea0";
        }
        catch { return false; }
    }
    private static void Check(int hr) => Marshal.ThrowExceptionForHR(hr);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetPackageFamilyName(IntPtr process, ref uint length, StringBuilder? name);
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class DeviceEnumerator { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDeviceEnumerator
    { [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out IDeviceCollection devices); }
    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDeviceCollection
    { [PreserveSig] int GetCount(out uint count); [PreserveSig] int GetItem(uint index, out IDevice device); }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDevice
    { [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object result); }
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
}

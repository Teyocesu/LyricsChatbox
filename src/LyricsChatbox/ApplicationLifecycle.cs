using System.Security;
using System.Security.Principal;
using Microsoft.Win32;

namespace LyricsChatbox;

public sealed class SingleInstance : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle restore;
    private RegisteredWaitHandle? listener;
    public bool IsOwner { get; }
    public static string Name => @"Local\LyricsChatbox." + WindowsIdentity.GetCurrent().User!.Value;

    public SingleInstance(string? name = null)
    {
        name ??= Name;
        mutex = new(false, name);
        try { IsOwner = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { IsOwner = true; }
        restore = new(false, EventResetMode.AutoReset, name + ".Restore");
        if (!IsOwner) restore.Set();
    }
    public void Listen(Action action)
    {
        if (!IsOwner || listener is not null) return;
        listener = ThreadPool.RegisterWaitForSingleObject(restore, (_, _) => action(), null, Timeout.Infinite, false);
    }
    public void Dispose()
    {
        listener?.Unregister(null);
        restore.Dispose();
        if (IsOwner) mutex.ReleaseMutex(); // The application's owning dispatcher also performs shutdown.
        mutex.Dispose();
    }
}

public enum WindowAction { Normal, Hide, Exit }
public static class LifecyclePolicy
{
    public static WindowAction Close(AppSettings settings, bool exitRequested) =>
        !exitRequested && settings.CloseToTray ? WindowAction.Hide : WindowAction.Exit;
    public static WindowAction Minimize(AppSettings settings) => settings.MinimizeToTray ? WindowAction.Hide : WindowAction.Normal;
}

public interface IStartupStore
{
    string? Read();
    void Write(string? command);
}

public sealed class WindowsStartupStore : IStartupStore
{
    public const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "LyricsChatbox";
    public string? Read() { using var key = Registry.CurrentUser.OpenSubKey(Key); return key?.GetValue(ValueName) as string; }
    public void Write(string? command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key, true);
        if (command is null) key.DeleteValue(ValueName, false);
        else key.SetValue(ValueName, command, RegistryValueKind.String);
    }
}

public sealed class StartupRegistration(IStartupStore store)
{
    public static string Command(string executable)
    {
        if (!System.IO.Path.IsPathFullyQualified(executable) || !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            executable.IndexOfAny(['"', '\r', '\n']) >= 0) throw new ArgumentException("Invalid startup executable");
        return "\"" + executable + "\"";
    }
    public bool Set(bool enabled, string executable)
    {
        try
        {
            var desired = enabled ? Command(executable) : null;
            store.Write(desired);
            return store.Read() == desired;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or System.IO.IOException or ArgumentException)
        { return false; }
    }
}

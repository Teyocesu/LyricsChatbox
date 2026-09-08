using System.Windows;
using System.IO;
using System.Text.Json;

namespace LyricsChatbox;

public partial class App : Application
{
    private SingleInstance? instance;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new();
        if (!instance.IsOwner) { Shutdown(); return; }
        if (e.Args.Contains("--osc-test") || e.Args.Contains("--osc-burst"))
        {
            using var output = new ChatboxOutput();
            output.Configure("127.0.0.1", 9000);
            var scheduler = new ChatboxScheduler();
            var sent = new List<object>();
            void SendCurrent()
            {
                if (scheduler.Take(MonotonicClock.Now) is not { } message) return;
                output.Send(message.Text);
                sent.Add(new { utc = DateTimeOffset.UtcNow, text = message.Text, status = output.Status });
            }
            foreach (var line in e.Args.Contains("--osc-burst") ? Array.Empty<string>() : new[] { "LyricsChatbox test 1: hello", "LyricsChatbox test 2: 日本語 テスト", "LyricsChatbox test 3: 🎵 👩‍🚀 é", "LyricsChatbox test 4: " + new string('A', 180) })
            {
                scheduler.Set(1, line, true);
                SendCurrent();
                await Task.Delay(6000);
            }
            scheduler.Set(1, "Rapid test starting", true);
            SendCurrent();
            // Queue the complete burst synchronously so task delays cannot turn it into separate valid intervals.
            foreach (var line in new[] { "Latest A", "Latest B", "Latest C" })
                scheduler.Set(1, line, true);
            await Task.Delay(1600);
            SendCurrent();
            await Task.Delay(6000);
            scheduler.Set(1, "", true);
            SendCurrent();
            await Task.Delay(6000);
            // Only synthetic diagnostic messages, never normal listening history.
            try
            {
                Directory.CreateDirectory(LocalData.DefaultRoot);
                File.WriteAllText(Path.Combine(LocalData.DefaultRoot, "osc-test.json"), JsonSerializer.Serialize(sent));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            Shutdown(); return;
        }
        var window = new MainWindow();
        MainWindow = window;
        instance.Listen(() => _ = Dispatcher.InvokeAsync(window.RestoreWindow));
        SessionEnding += (_, _) => window.RequestExit();
        window.Show();
        window.ApplyInitialWindowState();
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}

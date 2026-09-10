using System.Net.Http;
using System.Windows;

namespace LyricsChatbox;

public partial class MainWindow
{
    private readonly HttpClient discoveryHttp = new(new HttpClientHandler { UseProxy = false, UseCookies = false, AllowAutoRedirect = false });
    private readonly OscDestinationSelection destinationSelection = new();
    private CancellationTokenSource? discoveryCancellation;
    private OscDiscovery? discovery;
    private bool discovering;
    private double nextDiscovery;
    private OscDestination? configuredDestination;

    private void InitializeDiscovery()
    {
        discovery = new(discoveryHttp, OscDiscovery.BrowseAsync);
        destinationSelection.SetMode(OscDestinationSelection.InitiallyAutomatic(settings));
        AutoOscBox.IsChecked = destinationSelection.Automatic;
        DiscoverButton.IsEnabled = destinationSelection.Automatic;
        configuredDestination = new(settings.Host, settings.Port);
        DiscoveryStatus.Text = destinationSelection.Automatic ? "Looking for local VRChat…" : "Manual destination";
        OscDestinationText.Text = settings.Host + ":" + settings.Port;
    }
    private void DiscoveryModeChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        var automatic = AutoOscBox.IsChecked == true;
        settings = settings with { AutoDiscoverOsc = automatic };
        DiscoverButton.IsEnabled = automatic && !discovering;
        discoveryCancellation?.Cancel(); destinationSelection.SetMode(automatic); nextDiscovery = 0;
        DiscoveryStatus.Text = automatic ? "Looking for local VRChat…" : "Manual destination";
        ConfigureEffectiveDestination(); Save(); Tick();
    }
    private void DiscoveryReceiverChanged()
    {
        discoveryCancellation?.Cancel(); destinationSelection.Invalidate(); nextDiscovery = 0;
        ConfigureEffectiveDestination();
    }
    private void TickDiscovery(double now)
    {
        if (destinationSelection.Automatic && !discovering && now >= nextDiscovery) StartDiscovery();
    }
    private void DiscoverNow(object sender, RoutedEventArgs e)
    {
        if (destinationSelection.Automatic && !discovering) StartDiscovery();
    }
    private async void StartDiscovery()
    {
        if (closing || discovery is null) return;
        discovering = true; DiscoverButton.IsEnabled = false; nextDiscovery = MonotonicClock.Now + 30;
        discoveryCancellation?.Dispose(); discoveryCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var token = discoveryCancellation.Token; var revision = destinationSelection.Revision;
        if (destinationSelection.Discovered is null) DiscoveryStatus.Text = "Looking for local VRChat…";
        try
        {
            var task = Task.Run(() => discovery.DiscoverAsync(token), token);
            pending.Add(task);
            OscDiscoveryResult result;
            try { result = await task; }
            finally { pending.Remove(task); }
            if (closing || token.IsCancellationRequested || !destinationSelection.Complete(revision, result)) return;
            DiscoveryStatus.Text = result.Status; ConfigureEffectiveDestination();
        }
        catch (OperationCanceledException) { }
        finally { discovering = false; if (!closing) DiscoverButton.IsEnabled = destinationSelection.Automatic; }
    }
    private void ConfigureEffectiveDestination()
    {
        var destination = destinationSelection.Effective(settings);
        OscDestinationText.Text = destination.Host + ":" + destination.Port;
        if (destination == configuredDestination) return;
        try
        {
            output.SendTyping(false); output.Configure(destination.Host, destination.Port);
            configuredDestination = destination; scheduler.ReceiverChanged(); typing.Reset();
        }
        catch (Exception ex) when (ex is System.Net.Sockets.SocketException or ArgumentException)
        { DiscoveryStatus.Text = "OSC destination unavailable · check manual settings"; }
    }
}

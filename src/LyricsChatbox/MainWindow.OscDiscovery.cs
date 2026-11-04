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
    private bool destinationUnavailable;
    private bool oscTestQueued;

    private string? OscTestBlockReason(DateTimeOffset nowUtc) =>
        !engine.Enabled ? "Turn Output on to send a test."
        : IsOutputPaused(nowUtc) ? "Output is paused. Resume it to send a test."
        : nativeChatbox.Holding ? "Output is held while the VRChat Chatbox is open. Try again after it closes."
        : null;

    // Only a click queues a test. It uses the normal scheduler, so pacing, eligibility and the destination all apply.
    private void SendOscTest(object sender, RoutedEventArgs e)
    {
        if (OscTestBlockReason(DateTimeOffset.UtcNow) is { } blocked) { OscTestStatus.Text = blocked; return; }
        if (oscTestQueued) { OscTestStatus.Text = "A test is already waiting to be sent."; return; }
        if (!scheduler.RequestTest(FloatingCompatibility.Effective(settings.Compact))) { OscTestStatus.Text = "Output is not ready yet. Try again."; return; }
        oscTestQueued = true;
        OscTestStatus.Text = "Sending test…";
        Tick();
    }

    private void CancelBlockedOscTest(DateTimeOffset nowUtc)
    {
        if (!oscTestQueued || OscTestBlockReason(nowUtc) is not { } blocked) return;
        scheduler.CancelTest(); oscTestQueued = false;
        OscTestStatus.Text = "Test not sent. " + blocked;
    }

    private void ReportOscTest(bool sent)
    {
        oscTestQueued = false;
        OscTestStatus.Text = sent
            ? "Test sent from this PC at " + DateTime.Now.ToString("T") + ". Check VRChat to see whether it appeared."
            : "Test could not be sent from this PC. Check the destination.";
    }

    private void ApplyOscRouteView()
    {
        // Describe the socket's actual destination, not one recomputed from settings.
        var destination = configuredDestination ?? destinationSelection.Effective(settings);
        var address = destination.Host + ":" + destination.Port;
        var route = destinationUnavailable ? "Destination unavailable · check Advanced destination"
            : destinationSelection.Automatic && destinationSelection.Discovered is not null ? "Found VRChat with OSCQuery · sending to " + address
            : destinationSelection.Automatic ? "VRChat not detected · sending to the configured address " + address
            : "Manual destination · sending to " + address;
        var last = output.LastSentUtc is { } sent
            ? "Last local send " + sent.ToLocalTime().ToString("T") + " · VRChat does not confirm delivery"
            : "Nothing sent from this PC yet";
        if (OscRouteText.Text != route) OscRouteText.Text = route;
        if (OscLastSendText.Text != last) OscLastSendText.Text = last;
    }

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
    private void InvalidateDiscoveryForLifecycle() => DiscoveryReceiverChanged();
    private void TickDiscovery(double now)
    {
        if (!discovering && destinationSelection.ShouldRetry(now, nextDiscovery)) StartDiscovery();
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
            nextDiscovery = result.Destination is null ? MonotonicClock.Now + 30 : double.PositiveInfinity;
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
            output.Configure(destination.Host, destination.Port);
            configuredDestination = destination; scheduler.ReceiverChanged(); destinationUnavailable = false;
        }
        catch (Exception ex) when (ex is System.Net.Sockets.SocketException or ArgumentException)
        {
            destinationUnavailable = true;
            if (destinationSelection.Discovered is not null)
            {
                destinationSelection.Invalidate();
                nextDiscovery = 0;
            }
            DiscoveryStatus.Text = "OSC destination unavailable · check manual settings";
        }
    }
}

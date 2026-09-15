# Spotify Desktop GSMTC observation spike

Research only. This does not enable Spotify in LyricsChatbox. Run from the repository root on Windows 10/11 with Spotify Desktop open. The probe never reads/writes LyricsChatbox LocalData, resolves lyrics, sends OSC, contacts Spotify's Web API, or initiates a seek.

```powershell
dotnet build spikes/SpotifyPlaybackSpike/SpotifyPlaybackSpike.csproj -c Release
dotnet run --project spikes/SpotifyPlaybackSpike -c Release --no-build -- --self-test
dotnet run --project spikes/SpotifyPlaybackSpike -c Release --no-build -- --duration 90 --interactive
```

Before the 90-second run, sign in and play an ordinary song. Let it play for roughly 60 seconds. While the probe keeps running, perform pause/resume, Next, Previous, and forward/backward scrubs manually in Spotify. After each action type a short marker such as `pause`, `resume`, `next`, `previous`, `scrub-forward`, or `scrub-backward` followed by Enter in the probe console. Use another separate longer run to close/reopen Spotify and compare simultaneous Apple Music/Spotify sessions. Ctrl+C ends a run and writes the report. Unmarked changes still appear in polling and GSMTC events; markers merely identify user actions.

The default is strictly observation-only. An optional single transport command requires the **exact SourceAppUserModelId already observed in a previous report** and a unique matching session:

```powershell
dotnet run --project spikes/SpotifyPlaybackSpike -c Release --no-build -- --duration 15 --source '<observed exact ID>' --exercise pause
```

Commands: `play`, `pause`, `next`, `previous`, `shuffle`, `repeat`. No seek command exists. Capability, GSMTC return value and subsequent raw observations are recorded separately; a true return is not physical proof.

Reports are bounded JSON and text in ignored `artifacts/spikes/spotify/<UTC timestamp>/`. The per-run `session-N` is a diagnostic object identity, not a Windows-provided persistent session GUID. Raw timeline samples include observation UTC/monotonic time, Position and LastUpdatedTime. Statistics describe the longest contiguous same-recording playing segment (which may contain a scrub); inspect raw samples, events and markers to judge scrubs, transitions and pauses. Do not commit reports containing listening history. Artwork is only bounded-decoded and summarized, never saved. Volume/audio-session mapping and Free advertisements are not inferred when unobserved.

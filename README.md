# LyricsChatbox

A small Windows app that follows native Apple Music playback, finds synchronized lyrics, and sends the current line to the VRChat OSC Chatbox. Built with C# / .NET 10 / WPF. No accounts, telemetry, Apple credentials or backend.

## Run

Use the local Windows x64 distribution under `artifacts/win-x64` when supplied, and launch `LyricsChatbox.exe`. The self-contained build does not require a separate .NET installation. Windows 10 version 2004 or newer is required; development validation uses Windows 11.

1. Open Apple Music for Windows and play a song.
2. In VRChat, enable **OSC** from the Action Menu. Make your own Chatbox visible to check output. Stop other apps that send Chatbox messages to avoid competing output.
3. Open LyricsChatbox and select **Send lyrics to VRChat**. The first launch starts with sending off; this setting is remembered.

Default OSC destination: `127.0.0.1:9000`. **Apply** accepts an IP address (IPv4/IPv6) or `localhost`, and a port from 1 to 65535. UDP has no delivery acknowledgement; “sent” does not prove that VRChat displayed a message. No inbound server is needed.

## Synchronization and lyrics

The app explicitly selects Apple Music's Windows media session. Windows position anchors remain authoritative; interpolation only bridges short gaps and stops when observations become stale. Pause, seek, restart and track changes are re-evaluated automatically. Results from an old track are discarded.

Lyrics are resolved in this order: an imported local LRC for the recording, a valid local cache, [LRCLIB direct lookup and conservative search](https://lrclib.net/docs). Track title, artist, album and duration are sent over HTTPS to LRCLIB. Plain lyrics are never treated as synchronized. Incompatible or ambiguous recordings fail closed; unavailable lyrics and instrumental tracks are normal states. The app respects provider cooldowns and retries temporary failures without blocking playback monitoring.

**Use local LRC…** associates a UTF-8 `.lrc` file with the currently detected recording. Common minute/second timestamps, fractional seconds, repeated timestamp tags and LRC offset metadata are supported. The app clears its current line after a 10-second hold or an explicit blank LRC line. This can end an unusually long sung line early.

The **Lyric offset** control is optional and ranges from −5 to +5 seconds in 0.1-second steps. Positive means *later*: +1.0 s delays the displayed lyrics by one second. The playback clock automatically corrects its own anchors; it does not analyze audio or automatically repair inaccurate timestamps supplied by a lyrics file.

## Local data and privacy

Settings, imported lyrics and a simple successful-lookup cache live in `%LocalAppData%\LyricsChatbox`. **Open data folder** opens that directory. Imported files use a hash of recording metadata; choose them through the app instead of guessing filenames. Cache entries expire after 30 days. Broken cache/settings files are ignored; writes are atomic where supported by the filesystem. Normal listening does not persist lyric history or diagnostics.

**Retry lyrics** checks local files/cache again and repeats lookup if needed. It cannot bypass LRCLIB's rate limit. A successful cached recording is reused during normal replay.

## Limitations and validation

- Apple Music's observed Windows timeline uses whole seconds. Subsecond precision is limited by the source; a lyrics file can also have imperfect timing.
- This Apple Music version puts `artist — album` in its artist field and leaves the album field empty. LyricsChatbox handles an unambiguous occurrence of that format and preserves raw observations internally.
- Recording matching is intentionally cautious. Different feature credits or missing version information can result in no match. Local LRC import is the explicit fallback.
- Only the newest line is kept when output is faster than the 1.05-second send interval. Text is limited to 144 UTF-16 units without splitting Unicode grapheme clusters, with at most nine explicit lines. VRChat's own word wrapping can further limit display.
- Disabling stops new output immediately. An already displayed VRChat message is owned by the client and may remain until cleared or expired.
- VRChat process restarts reset output deduplication for the current line. Arbitrary remote receiver restarts cannot be detected through UDP.
- No system-audio capture, automatic transcription/alignment, other music players or Apple UI scraping.

See `PLAN.md` for the exact automated and physical validation evidence. A passing build or UDP test is not proof of complete headset/end-to-end validation.

## Build and test

Install the .NET 10 SDK (the repository pins the 10.0.400 feature band) on Windows:

```powershell
dotnet restore LyricsChatbox.slnx
dotnet build LyricsChatbox.slnx -c Release --no-restore
dotnet test tests/LyricsChatbox.Tests -c Release --no-build
dotnet run --project src/LyricsChatbox
dotnet publish src/LyricsChatbox -c Release -r win-x64 --self-contained true -o artifacts/win-x64
```

The regression suite covers timeline boundaries, offsets/gaps, pause/resume/seeks, stale lookup epochs, conservative matching, Unicode, OSC coalescing/UDP decoding, provider failures/cancellation/rate limiting, and local persistence corruption. It does not access Apple Music, LRCLIB or VRChat during normal test execution.

For explicit physical diagnostics:

```powershell
dotnet run --project spikes/AppleMusicPlaybackSpike -- 60
.\artifacts\win-x64\LyricsChatbox.exe --osc-test
.\artifacts\win-x64\LyricsChatbox.exe --osc-burst
```

The playback spike reads metadata/timing/events and compares the production clock to Windows. Its optional `--exercise` flag changes playback through public GSMTC controls; on the tested Apple Music version, seek calls reported success without actually seeking. Use native player controls for real seek validation.

OSC diagnostics send synthetic visible messages to localhost port 9000. The full sequence tests ASCII/Japanese/emoji/long text, then a coalesced A/B/C burst and clear; the short sequence tests only the burst and clear. Only these canned diagnostic sends are recorded in `%LocalAppData%\LyricsChatbox\osc-test.json` for checking what was emitted.

No MagicChatBox implementation was copied. The fixed OSC message encoder is independently implemented from the [OSC message format](https://opensoundcontrol.stanford.edu/spec-1_0.html) and [VRChat's Chatbox contract](https://docs.vrchat.com/docs/osc-as-input-controller).

# LyricsChatbox

A small Windows app that follows native Apple Music playback, finds synchronized lyrics, and sends the current line to the VRChat OSC Chatbox. Built with C# / .NET 10 / WPF. No accounts, telemetry, Apple credentials or backend.

## Run

Download the Windows x64 ZIP from [GitHub Releases](https://github.com/Teyocesu/LyricsChatbox/releases), extract it, and launch `win-x64/LyricsChatbox.exe`. The self-contained build does not require a separate .NET installation. Windows 10 version 2004 or newer is required; development validation uses Windows 11. This branch describes the v0.3 release candidate, awaiting final physical acceptance. The latest public stable release remains v0.2.0.

1. Open Apple Music for Windows and play a song.
2. In VRChat, enable **OSC** from the Action Menu. Make your own Chatbox visible to check output. Stop other apps that send Chatbox messages to avoid competing output.
3. Open LyricsChatbox and enable **Output** in the sidebar. The first launch starts with sending off; this setting is remembered. **Lyrics Only** is the default, with no forced song title or prefix.

The **Settings → Advanced destination** section contains the OSC destination. Default: `127.0.0.1:9000`. **Apply** accepts an IP address (IPv4/IPv6) or `localhost`, and a port from 1 to 65535. UDP has no delivery acknowledgement; “sent” does not prove that VRChat displayed a message. No inbound server is needed.

## Synchronization and lyrics

The app explicitly selects Apple Music's Windows media session. Windows position anchors remain authoritative; interpolation only bridges short gaps and stops when observations become stale. Pause, seek, restart and track changes are re-evaluated automatically. Results from an old track are discarded.

Lyrics are resolved in this order: an imported local LRC for the recording, a valid local cache, [LRCLIB direct lookup and conservative search](https://lrclib.net/docs), then NetEase as a best-effort fallback. Track title, artist, album and duration are sent over HTTPS to LRCLIB. If no usable confident result is available, title and artist are sent to NetEase; returned metadata must pass the same strict title/artist/version/duration checks. Plain lyrics are never treated as synchronized. Incompatible or ambiguous recordings fail closed; unavailable lyrics and instrumental tracks are normal states. The app respects provider cooldowns and retries temporary failures without blocking playback monitoring.

**Settings → Import local LRC…** associates a UTF-8 `.lrc` file with the currently detected recording. Common minute/second timestamps, fractional seconds, repeated timestamp tags and LRC offset metadata are supported. The app clears its current line after a 10-second hold or an explicit blank LRC line. This can end an unusually long sung line early.

The **Lyric offset** control is optional and ranges from −5 to +5 seconds in 0.1-second steps. Positive means *later*: +1.0 s delays the displayed lyrics by one second. The playback clock automatically corrects its own anchors; it does not analyze audio or automatically repair inaccurate timestamps supplied by a lyrics file.

## Display and manual chat

The **Display** page offers **Lyrics Only**, **Song + Lyrics**, **Status / Time** and **Custom** presets. Custom templates support `{lyrics}`, `{title}`, `{artist}`, `{album}`, `{time}`, `{message}`, `{elapsed}` and `{duration}`. The clock uses local 24-hour time; playback times use minutes and seconds (hours when needed). `{message}` is your saved custom status. Missing fields and unknown tokens are omitted; text inserted through a token is never interpreted as another template. Templates and status text accept up to 512 characters.

The **Manual** page takes priority while composing. With live edit off, a draft stays local until **Send**. With live edit on, the newest draft updates at the existing 1.05-second send cadence; old keystrokes never queue. **Send** or **Ctrl+Enter** holds the final message for eight seconds after emission, then resumes the current automatic display. **Resume Automatic** returns immediately; **Clear** sends an empty message and holds the empty state for eight seconds. Drafts are not saved. Optional **Typing indicator** sends VRChat's typing state while actively editing, and clears on three seconds of inactivity, focus loss, Send, Clear, resume, output disable or shutdown (best effort over UDP).

**Compact / Floating Chatbox** is opt-in and applies to automatic and manual output. It appends U+0003 followed by U+001F to non-empty payloads, using current VRChat rendering behavior to make the opaque background narrow while text appears to float. This is not an official background-width API and may change after VRChat updates. Toggling it resends the current text at the normal cadence. Empty clears remain empty. The preview hides the suffix, while its character counter includes those two UTF-16 units: compact mode has up to 142 visible units. Both modes preserve whole Unicode grapheme clusters during truncation.

## Message formatting and ASCII

Custom/Status messages and Manual drafts have separate **Left**, **Center** and **Right** choices. Center and Right insert ordinary spaces over an approximate 24–48-column block. VRChat's font and wrapping can render this differently; OSC does not provide an exact alignment or rich-text styling command. The layout preview uses a monospaced font to make the transmitted spaces visible, not to promise the same font in VRChat.

**Insert ASCII** inserts an editable Cat, AFK sign, Message divider or Music template at the current selection. Custom templates can still contain the eight display tokens. Intentional indentation and internal blank lines are preserved; ordinary Lyrics Only / Song + Lyrics formatting stays unchanged. Spaces count toward the 144-unit payload limit, and compact mode still reserves two units. The final formatter preserves complete graphemes and at most nine explicit lines; the manual counter marks trimmed content. Whitespace-only messages still clear the Chatbox.

## Secondary lyrics source

A primary hit, local LRC, cache hit or instrumental result never calls the fallback. LRCLIB keeps its direct lookup, broad search and optional album-narrowed search at the 20-result cap. It has a five-second total deadline; NetEase has a separate six-second total deadline, including pacing and at most three candidate lyric downloads. The UI displays “Searching another source…” during fallback. Transient errors retry later, independent provider cooldowns respect Retry-After, and old-track progress/results are discarded.

The NetEase adapter uses community-documented HTTPS REST endpoints, with no login, cookies, shared secret, encrypted endpoint emulation or HTML scraping. It is **not an official supported developer integration**. It can stop working, be blocked by region or change without notice. Public endpoint access does not establish a lyric redistribution license; no permission or service guarantee is asserted. Full current service-terms review remains unverified because the official terms page was blocked by browser site-safety during this evaluation. No provider lyric bodies are shipped in this repository or release archive. Local LRC remains available.

The evaluation compared Musixmatch, NetEase, QQ Music and Kugou. The final uncached chain confirmed 12 incremental timed results over confirmed LRCLIB misses/ambiguity and kept 11 LRCLIB-hit controls primary-only. Measured median total fallback was 2.48 seconds; small-sample nearest-rank p95 was 3.99 seconds. These are measurements from one network, not availability promises or proof of audio alignment. Exact metadata, counts, timing and limitations are in PLAN.md.

## Desktop layout

**Home** shows Apple Music state, song, current lyric, source and quick preset/floating/timing controls. **Display** provides visual presets and shows the token editor only for Custom. **Manual** has a local draft, payload counter and eight-second return countdown. **Settings** keeps import/data/network controls out of the main playback view. The sidebar output switch and final formatted preview remain visible. The preview labels floating mode and omits its control suffix; it is not a replica of VRChat's renderer. The dark native WPF theme uses Windows fonts/icons and adds no UI framework dependency.

## Local data and privacy

Settings, imported lyrics and a simple successful-lookup cache live in `%LocalAppData%\LyricsChatbox`. **Open data folder** opens that directory. Imported files use a hash of recording metadata; choose them through the app instead of guessing filenames. Cache entries expire after 30 days and retain their source. Existing v0.2 cache entries are read as LRCLIB; existing preferences migrate unchanged. Broken cache/settings files are ignored; writes are atomic where supported by the filesystem. Normal listening does not persist lyric history or diagnostics.

**Retry lyrics** checks local files/cache again and repeats lookup if needed. It cannot bypass either source's rate limit. A successful cached recording is reused during normal replay.

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
.\scripts\Publish.ps1
```

The publish script produces `artifacts/v0.3.0-rc.1/win-x64`, a ZIP and SHA256 file, including documentation and third-party license texts. The regression suite covers timeline boundaries, offsets/gaps, pause/resume/seeks, stale lookup epochs, conservative matching, provider failures/cancellation/rate limiting, local persistence corruption and migration, composer tokens, manual ownership, typing lifecycle, latest-draft coalescing and compact Unicode budgets. It does not access Apple Music, LRCLIB or VRChat during normal test execution.

For explicit physical diagnostics:

```powershell
dotnet run --project spikes/AppleMusicPlaybackSpike -- 60
.\artifacts\v0.3.0-rc.1\win-x64\LyricsChatbox.exe --osc-test
.\artifacts\v0.3.0-rc.1\win-x64\LyricsChatbox.exe --osc-burst
```

The playback spike reads metadata/timing/events and compares the production clock to Windows. Its optional `--exercise` flag changes playback through public GSMTC controls; on the tested Apple Music version, seek calls reported success without actually seeking. Use native player controls for real seek validation.

OSC diagnostics send synthetic visible messages to localhost port 9000. The full sequence tests ASCII/Japanese/emoji/long text, then a coalesced A/B/C burst and clear; the short sequence tests only the burst and clear. Only these canned diagnostic sends are recorded in `%LocalAppData%\LyricsChatbox\osc-test.json` for checking what was emitted.

No MagicChatBox implementation was copied. The fixed OSC message encoder is independently implemented from the [OSC message format](https://opensoundcontrol.stanford.edu/spec-1_0.html) and [VRChat's Chatbox contract](https://docs.vrchat.com/docs/osc-as-input-controller).

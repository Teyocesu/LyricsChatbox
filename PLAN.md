# Execution state

Updated 2026-09-06. Local MVP implementation, release packaging and available validation are complete, with explicit remaining manual acceptance gates. The earlier testing pause was explicitly lifted by the user.

Environment: Windows 11 build 26200, .NET SDK 10.0.400 / runtime 10.0.11 x64, Apple Music 1.1540.23042.0. Local branch `codex/mvp`; origin is the user-supplied GitHub repository. No public operations authorized.

| Phase | State | Evidence / remaining gate |
|---|---|---|
| 1 Apple Music GSMTC | VALIDATED | Native normal playback, pause/wait/resume, seeks, next/previous, same-song restart and player loss/reappearance observed. Corrected clock confirmed by user. |
| 2 Lyrics + matching | VALIDATED | Representative live LRCLIB searches plus production direct lookups; deterministic conservative matching and 11 provider/persistence tests. |
| 3 VRChat OSC | DONE WITH MANUAL GATE | Real loopback UDP decode passed. User confirmed ASCII/Japanese/emoji/long/clear. Controlled rapid sequence emits only start/C/clear; final headset confirmation and client restart remain unverified. |
| 4 Deterministic core | VALIDATED | Core/boundary regression suite covers anchors, integer heartbeats, seeks, offsets, epochs, matching, Unicode and latest-state scheduling. |
| 5 Integration | DONE WITH MANUAL GATE | Apple Music -> LRCLIB/cache -> WPF -> real Chatbox exercised. After pacing correction, user confirmed the result. Exhaustive headset transition/failure matrix remains unverified. |
| 6 WPF + persistence | VALIDATED | Actual window inspected and used, clean close/reopen, cache reuse, enabled/zero-offset settings persisted. Corrupt/oversized data and invalid configuration covered automatically. Full manual offset-control exercise remains unverified. |
| 7 Release gate | DONE WITH MANUAL GATE | Restore/build Release: zero warnings/errors. Full suite 43 passed. Self-contained win-x64 publish succeeded and executable launched. Licenses reviewed and bundled. Source/diff/scope/secrets review completed. Local ZIP produced; remaining gates are physical only. |

## Playback evidence
- Exact AUMID, raw metadata split, integer positions, event cadence and paused/resumed timestamp behavior are recorded in SPEC. GSMTC seek controls returned true without seeking; that was not counted as a successful transition.
- `artifacts/apple-resumed-validation.local.jsonl` (ignored): native pause about 6.2s, resume, forward seek 117->141, backward 145->26 and track changes. Production WPF was active through the initial transitions.
- Initial smoothing carried an artificial lead: 248 snapshots, mean excess 0.481s and maximum 1.083s over Position+age. Reanchoring each heartbeat removed that accumulation but caused the user's reported lyric flicker.
- Final bounded-integer clock: `artifacts/apple-clock-bounded.local.jsonl`, 135 snapshots, zero uncommanded backward estimates and zero estimates reaching an unconfirmed next second. User's final check: "Sin titileo y bien sincronizado", offset 0.0s.
- User chose clock correction/testing before any proposed audio alignment expansion. No audio capture or automatic per-song LRC offset was added.

## Provider and output evidence
- Live LRCLIB cases: ordinary, feature credit, 2019 mix/remaster, deluxe, clean (no result), live, remix, Japanese, instrumental and nonexistent recording. Search returned conflicting durations/content; strict matching fails closed. No rate-limit pressure test or provider lyrics committed.
- Real app resolved My Dawg, Said Sum and subsequent tracks; final distribution loaded Soldado y Profeta [Remix] from cache after restart.
- User's first synthetic VRChat check passed all text/clear cases but briefly showed B before C. Diagnostic timing was made deterministic by submitting A/B/C synchronously. Repeated synthetic send logs show start/C/empty only; this proves emission, not headset display. Other Chatbox senders were to be stopped by user.
- Normal listening does not persist lyric history. The diagnostic log in LocalAppData contains only canned OSC test messages.

## Release evidence and remaining physical checks
- `dotnet restore LyricsChatbox.slnx`: passed.
- `dotnet build LyricsChatbox.slnx -c Release --no-restore`: passed, 0 warnings/errors.
- `dotnet test tests/LyricsChatbox.Tests -c Release --no-build`: 43 passed, 0 failed/skipped.
- `dotnet publish src/LyricsChatbox -c Release -r win-x64 --self-contained true -o artifacts/win-x64`: passed. Launched distributed executable and observed current metadata, cached lyric, offset zero, then enabled sending.
- Reviews: bounded/cancellable network, epoch rejection, dispatcher ownership, invalid metadata/cache/JSON, conservative matching, Unicode/grapheme formatting, single desired OSC state and dependency licenses. See THIRD_PARTY_NOTICES.md.
- Unverified physically: rapid track-switch stress, VRChat loss/reappearance, final synthetic rapid-C display, exhaustive headset end-to-end transitions, manual offset changes, no-lyrics/provider-failure UX and full launch-order matrix. Automated coverage is not substituted for these items.
- Internal 1.05s output interval and 10s line hold remain conservative constants pending complete physical validation; exact client counting/rate boundary and long instrumental-gap policy were not exhaustively measured.

- Enabled end-to-end user check found a 1–2s track-change delay, occasionally longer. Existing 1.5s pacing explains part of the latency; observed I Don't Know LRC starts at 31.03s. Reduced pacing to 1.05s after verifying current official five-per-five-second budget and installed client 2026.3.2p1. New transition/rate-window regression passed; full suite now 43 passed. Latest package relaunched; user answered "esta increible" to the focused track-change/lyric-following headset recheck. This confirms the reported improvement, not every unobserved physical acceptance case.

- Native lifecycle check: app showed No Apple Music session and empty current lyric after closing Apple Music, then detected Backlight and loaded LRCLIB after reopening. Ignored apple-lifecycle.local.jsonl records no-session from 15.69s through 42.70s during this deliberate interruption.
- Native restart/previous check: ignored apple-restart-previous.local.jsonl records Backlight 37->0, 7->0, then The Mystic at position 0. Production WPF displayed the new track and advancing lyric. Both capture processes completed.
- Distribution: artifacts/win-x64/LyricsChatbox.exe and artifacts/LyricsChatbox-0.1.0-win-x64.zip. Self-contained Windows x64 binaries, README, validation state and license texts included. Archive structure inspected. No credentials, normal lyric history, local settings or lyric cache included.
- Git: implementation preserved as one coherent local MVP commit on codex/mvp. No tags, pushes or public releases.

# Execution state

Historical MVP evidence (2026-09-06). The current v0.2 execution and publication state is recorded below. The earlier testing pause was explicitly lifted by the user.

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

# v0.2.0 execution (2026-09-07)

The new user goal explicitly authorizes publishing v0.1.0 and normal Git/GitHub work for v0.2.0. v0.2.0 final release requires critical physical checks including Compact/Floating confirmation.

- v0.1.0 independently verified: clean source at 0bfdc91347f6c9642f70bc4f6057d15ff638540c, Release build 0 warnings/errors, 43 tests passed; regenerated Windows x64 package with licenses and no personal data/cache.
- Published main at that SHA, annotated tag v0.1.0, public release https://github.com/Teyocesu/LyricsChatbox/releases/tag/v0.1.0. ZIP asset uploaded (71,807,050 bytes), GitHub digest matches local SHA256 8e7f6c6043d5bb101c56eb0ea1b544d37a13747172405bd44fea61a70d15f817. Checksum also attached. No project license invented.
- Created codex/v0.2.0 directly from v0.1.0.

| Phase | State | Next evidence |
|---|---|---|
| 0 Audit/publish | VALIDATED | Public release/source/tag/asset verified |
| 1 LRCLIB coverage | IMPLEMENTED / TESTED | Invalid timed direct result falls through; usable search candidates; one album-narrowed request only at 20-result cap, preserving broad ambiguity |
| 2 Secondary provider | BLOCKED BY TERMS | Official Musixmatch requirements conflict with approved no-tracking/clean display behavior; no scraper or fake integration |
| 3 Composer | IMPLEMENTED / TESTED | Eight tokens, four presets, safe missing fields, single-pass replacement and settings migration |
| 4 Manual/typing/live edit | VALIDATED | User confirmed repeated live/manual/long-text sequence, typing clears and automatic return; ownership/lifecycle regressions and real UDP typing packets passed |
| 5 Compact/Floating | VISUALLY CONFIRMED | User confirmed normal -> compact -> normal, Unicode/clock, real lyrics/composer and manual/long combinations |
| 6 UI/settings | IMPLEMENTED / INSPECTED | Three WPF tabs, contrast fixed, preview and actual payload counter; settings survived Release restart |
| 7 Release gate | RELEASED / VERIFIED | v0.2.0 public release, annotated tag and Windows ZIP/checksum verified; Release build 0 warnings/errors, 61 tests and critical physical checks passed |

## v0.2 evidence
- Live LRCLIB metadata-only probes covered Sweet Child O' Mine, Smack That (feature), Run It Up (features) and Said Sum. Structured feature metadata already returns plausible records; no feature/version/duration scoring was relaxed. Search can hit 20 results with conflicting durations. Probe is ignored under artifacts; provider lyrics are not committed.
- A concrete resolver defect prevented fallback when a direct result contained nonempty text without parseable timestamps. New regression protects fallback to a usable synchronized candidate. Album narrowing is bounded to one extra request; combining broad/narrowed candidates preserves conflict rejection. HTTP failure, cooldown, positive cache and epoch behavior remain covered.
- Musixmatch official docs checked 2026-09-07: https://docs.musixmatch.com/getting-started , https://docs.musixmatch.com/implementation-guidelines , https://docs.musixmatch.com/content-restrictions , https://docs.musixmatch.com/lyrics-views-tracking . Supported timed-lyrics APIs exist, but required usage tracking, copyright/branding/backlinks and territorial restrictions require an appropriate agreement before this no-telemetry desktop/VRChat integration. No key or applicable agreement is configured. The user explicitly allowed leaving this provider blocked; the current resolver boundary is retained without speculative provider classes.
- Real v0.2 WPF sent `Floating test · 日本語 🎵` with local time in normal, compact, then normal mode. User answered "si" to confirmation of narrow opaque element, floating text, restored ordinary background and intact Japanese/emoji. This proves that visual combination only.
- Release WPF restarted after contrast correction; tab labels and content are readable. Manual Send displayed the canned Unicode message and returned from Manual to Automatic after the eight-second hold, observed in app. Headset manual/typing/live-edit/long-text and current-song return are still pending.
- Latest full command: `dotnet test tests/LyricsChatbox.Tests -c Release --no-build`: 61 passed, 0 failed/skipped. Latest Release build: 0 warnings/errors. No v0.2 tag or release yet.
- Native v0.2 capture completed normally: `artifacts/apple-v02-validation.local.jsonl` (ignored). Hello Cotto pause lasted about 9.6s at position 169; resume was observed; dragging the native slider produced backward 201 -> 53 and forward 61 -> 133. Next changed to Malbec at zero. WPF observed Hello Cotto from cache, Malbec from LRCLIB, advancing lyrics and offset zero. Simple clicks on the thin progress bar did not seek and were not counted. This capture is production-clock/native-session evidence, not headset confirmation.
- Actual WPF manual/live test sent Japanese/emoji, Live 1 then Live 2/FINAL, then a long compact draft capped at 144 total UTF-16 units. App returned to current lyrics after Send. Actual composer then showed Malbec/artist + current lyric, compact off, then Lyrics Only. Headset answers for these later combinations remain pending.
- Focused audit strengthened the album-search regression: a broad-page conflicting synchronized record must remain ambiguous even when the narrowed page contains only the preferred record. Existing real UDP integration now receives both typing boolean states and verifies the exact OSC address/type tags. Three affected cases passed in Release.
- User answered "si todo bien" to the real-song sequence: compact lyrics -> Malbec/title/artist + current lyric -> ordinary background -> Lyrics Only, including pause/resume, seeks and track change. These combinations are now visually confirmed. Separate manual/typing/live-edit/long compact confirmation remains pending.
- Final Release build and complete suite after test strengthening passed: 0 warnings/errors and 61 tests. Initial v0.2 package built successfully (410 entries), with all five license texts matching source copies; no settings, cache, LRC files, credentials or diagnostic captures are bundled. `System.Transactions.Local.dll` is a runtime assembly, not personal local data. Final artifact is rebuilt after committing so its embedded source revision identifies the feature commit.
- Diff reviewed against v0.1.0: unchanged playback clock/engine and matching policy; dispatcher-owned manual state; epoch guard retained before scheduling and async completion; one paced final desired payload; safe Unicode and compact budgets; settings optional defaults; bounded sequential provider fallback. Source scan found no credentials/keys. README now describes implemented behavior and blocked Musixmatch accurately. Final release is still withheld for remaining physical gates (manual matrix, lookup-in-flight track change and representative no-lyrics runtime state).

## v0.2 final acceptance (supersedes pending checks above)
- User did not see the first manual sequence, so it was not counted. Repeated the real WPF sequence while music played: Live 1 with Japanese/emoji -> Live 2/FINAL -> same text compact -> long compact draft -> Send -> current automatic lyrics. User answered **"Todo funcionó"** to explicit checks for no competing lyrics, no stale drafts, intact long text, return to lyrics and typing indicator clearing. Combined with earlier real-song and normal/compact/normal confirmations, the v0.2 critical headset matrix is complete.
- Self-contained package at feature commit 29de2902a7542053cde0b4bf3b81243abcac7da8 launched successfully; enabled/offset/preset/compact persisted. Hijo de la Noche showed **Ambiguous lyrics match** with empty lyric/preview, a real safely rejected no-accepted-lyrics state. It did not substitute a guessed recording. Exact empty-result and transient-provider states are automated, not separately claimed as headset-tested outages.
- Explicit native/LRCLIB race capture: `dotnet run --project artifacts/LookupRace -c Release` (ignored diagnostic; no extra product code). Uses real GSMTC metadata/Next, real LRCLIB and the production resolver/engine with a fresh isolated cache. Hijo de la Noche request was pending at 0.117s; actual Quavo #Mododiablo metadata was observed at 0.226s while still pending; old request finished at 1.223s and `Complete(oldEpoch, result)` returned false, preserving the current track. The WPF distribution independently followed the same native transition and loaded Quavo from LRCLIB. Log: `artifacts/lookup-race-v02.local.jsonl`, no lyric bodies logged. No synthetic HTTP response or claimed successful-but-unobserved seek was used.
- Release requirement review: all v0.2 implementation, migration, bounded-provider, ownership, typing, Unicode, compact, cadence, epoch, documentation, license and critical visual gates are satisfied. Musixmatch remains the expressly permitted blocked provider; no credentials needed for implemented functionality. No architecture/privacy/scope deviations. Historical v0.1 exhaustive headset stress/restart/remote-receiver cases remain unmeasured and are not claimed as new v0.2 evidence.
- Feature commit 29de2902a7542053cde0b4bf3b81243abcac7da8 was pushed normally to codex/v0.2.0. Final documentation commit is to be fast-forwarded to main, tagged and packaged under the user's conditional release authorization. No force push, history rewrite or project license selection.

## v0.2 publication verified (2026-09-07)
- Released commit: **437dd958dd89056e69779ec6f81d1312d4a00859**. main advanced by fast-forward from the frozen v0.1 commit; codex/v0.2.0 and annotated v0.2.0 resolve to the released commit. v0.1.0 still resolves to 0bfdc91347f6c9642f70bc4f6057d15ff638540c. This final publication-evidence documentation commit follows the release on main; application code is unchanged.
- Public non-draft, non-prerelease: https://github.com/Teyocesu/LyricsChatbox/releases/tag/v0.2.0 . Uploaded ZIP is 71,820,920 bytes, 410 entries. GitHub's asset SHA256 matches local: **c5f6c4642bd881af2293b5658f034b6f792f1d6202aa2154fa861d4188599556**. The SHA256 text file is also uploaded. GitHub CLI finished successfully; no pending upload process remains.
- Artifact: `artifacts/v0.2.0/LyricsChatbox-0.2.0-win-x64.zip`; executable under `artifacts/v0.2.0/win-x64/LyricsChatbox.exe`. Embedded version is `0.2.0+437dd958dd89056e69779ec6f81d1312d4a00859`. Final executable launched and showed the safe waiting state when Apple Music was no longer open. Package contains matching license texts/documentation, no settings/cache/lyrics/credentials or diagnostics. Release notes accurately state the optional-provider limitation and validation scope.
- Both requested releases are now publicly available. No remaining v0.2 critical gate. Musixmatch remains unavailable under current approved scope, as explicitly allowed by the goal; historical exhaustive stress/outage cases are not newly claimed as validated.

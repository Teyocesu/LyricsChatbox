# Current handoff — v0.6.1 maintenance branch ready for release preparation

v0.6.0 is published stable on `main`. The v0.6.1 maintenance branch `codex/v0.6.1` holds: profile-aware Lyric Context (unified template budgeting, metadata-preserving degradation, Custom `{lyrics}` support, honest disabled state), master Output Off hard no-transmission boundary (Send/Clear/Live arming gated), corrected README/SPEC Spotify-era wording, and the 22-item context regression matrix. Full suite green, physical QA passed on a dev build with loopback capture, user environment restored byte-identical. No Medium/High blockers. Product stays at 0.6.0 — no version bump, packaging, tag, or release. Next: separate v0.6.1 release-preparation task. Stop before version/tag/merge/publish.

# Previous handoff — v0.6.0-rc.2 locally prepared, awaiting publication review

QA6-01 (Automatic stuck while Spotify unsettled) and QA6-02 (ambiguity guidance hidden by compact layout) are corrected, covered by new automated tests, physically regressed on a dev build across the full Automatic matrix, and versioned to 0.6.0-rc.2. `main`/`origin/main` remain `4df40710e673a7c6f585b6eb04f6b866f44aa909`; rc.1 tag/release untouched; no rc.2 tag/release yet. Next: review, then publish rc.2; second-machine Spotify stays external. Stop before stable work.

# Previous handoff — v0.6.0-rc.1 final correction committed, artifacts rebuilding next

The RC updater now lets an installed `0.6.0-rc.1` graduate to same-numeric stable `0.6.0` while staying stable-only (no downgrades, prereleases still ignored); focused tests plus the full 336/336 suite pass. `main`/`origin/main` remain `4df40710e673a7c6f585b6eb04f6b866f44aa909`, v0.5.4 remains public, and no tag/release exists. Next: push this final source commit, rebuild ZIP/installer from the clean tree, verify the embedded source revision, and stop before publication.

# Previous handoff — v0.6.0-rc.1 locally prepared, awaiting publication review

v0.6.0-rc.1 is versioned (`0.6.0-rc.1` informational, `0.6.0.0` numeric), built, and validated locally on `codex/v0.6.0`; `main`/`origin/main` remain `4df40710e673a7c6f585b6eb04f6b866f44aa909` and v0.5.4 remains public. Portable ZIP (81,452,028 bytes, `d74ed9a8…ed625ee`) and Inno 7.1.0 installer (58,196,256 bytes, `f3ec4b2a…91b77e2`) were verified against the 487-file policy-checked staging; both are unsigned as documented and exist locally only. Fresh install, v0.5.4 upgrade with full user-data preservation, uninstall, and reinstall all pass; packaged-app checks covered Spotify playback/pause/next/natural-transition/loss/recovery, Automatic selection, pause persistence, and window restore. Final locked restore, warning-free Release build, 324/324 tests, package policy, and `git diff --check` pass. The user environment was restored to stable v0.5.4 with byte-identical LocalData. Remaining: evidence review, publication authorization, and external second-machine/Apple/both-playing/visual QA. Stop before tag, prerelease, merge, or stable work.

# Previous handoff — v0.6.0 second post-audit correction pack completed

AUD6R-01 and Windows PowerShell package-policy compatibility are corrected on `codex/v0.6.0`; `main`/`origin/main` remain `4df40710e673a7c6f585b6eb04f6b866f44aa909`. No RC, version, tag, release, installer build/install or public-copy work was started.

Spotify accepted observations now carry the immutable source revision under which the gate proved them. An invalidation before publication drops the old result; an invalidation after the last current check can only leave an old-revision callback, never restamp it as current. Snapshot/status and artwork use the same token, with a revision recheck before artwork and no external callback under the transition lock. Deterministic media/session tests reproduce the former `null(N+1) -> old snapshot(N+1)` failure, prove it impossible after the fix, and show the allowed `null(N+1) -> old snapshot(N)` callback is rejected by the production host/coordinator path. Old track, lyrics epoch, artwork and UntilNextTrack cannot re-enter/change. The full previous Spotify settlement matrix remains green.

`PackagePolicy.ps1` no longer depends on .NET-only `Path.GetRelativePath` or `Convert.ToHexString`. Its Windows-only compatibility helper uses canonical full paths plus a separator-bounded OrdinalIgnoreCase child prefix and rejects exact-root, sibling, outside, similar-prefix and canonicalized escape cases before returning normalized `/` paths. Expanded tests also cover nested/space/case paths and duplicate ZIP entries. The package matrix passes under both Windows PowerShell 5.1 and PowerShell 7; the existing 487-file Phase 4 staging/ZIP/checksum/installer-source verification passes under Windows PowerShell 5.1 with unchanged SHA-256.

Race tests passed 4/4, multiplayer/Spotify settlement 48/48 and the relevant subsystem 170/170. Final locked restore passed, the Release solution built with zero warnings/errors, all 324 tests passed with no skips, both PowerShell package tests passed, the real Phase 4 verification path passed under Windows PowerShell 5.1, and `git diff --check` passed. No physical Spotify run was needed because the concurrency path is deterministic. Remaining gates are final independent confirmation, second-machine Spotify QA, explicit RC authorization and the later authorized RC installer/physical matrix. Stop before all RC, installer, second-machine, version/tag/release work.

# Previous handoff — v0.6.0 post-audit correction pack completed

AUD6-01 through AUD6-03 are corrected on `codex/v0.6.0`; `main`/`origin/main` remain `4df40710e673a7c6f585b6eb04f6b866f44aa909`, and no RC, tag, version, release or public-copy work was started.

Spotify settlement now preserves the last accepted recording across reconnect, rejects stale-session observations, compares exact identity-affecting state, keeps metadata-first and timeline-first combinations invalid, and requires an exact candidate across distinct fresh authoritative anchors while Playing. Cold/different Paused candidates remain fail-closed; only an exact prior identity can re-establish while Paused through stable observations. Physical production-adapter validation from Spotify closed covered corrected startup, more than one anchor, Next, Previous, forward/backward scrubs and close/reopen of the same recording. The paused rounded reconnect candidate was not accepted; after Play, only the original corrected 324.733-second identity settled. No alternate transient identity was accepted. Natural end-of-track transition was not performed in this bounded pass. No app/OSC output path ran, and Spotify was restored to closed.

Malformed `PlaybackSource` JSON is now isolated by a property converter: missing/null/unknown/number/boolean/object/array default only that property to AppleMusic, canonical strings round-trip, unrelated settings survive, and invalid whole JSON still falls back safely. Persisted Timed pause restore now accepts only a positive remaining interval through 35 minutes UTC; farther future, year-9999 and expired values clear, while 5/15/30-minute creation and indefinite modes are unchanged.

Focused correction tests passed 90/90. Final locked restore passed, the Release solution built with zero warnings/errors, all 320 tests passed with no skips, package policy passed and `git diff --check` passed. Remaining pre-RC gates are independent diff confirmation, second-machine Spotify QA and explicit RC authorization. Do not merge, tag, release or begin RC from this handoff.

# Previous handoff — v0.6.0 Phase 4 packaging hardening completed

Phase 4 is implemented on `codex/v0.6.0` in `354034bfc13e269ed6ae5253c8a689e7c8000161`. Packaging now cleans only a validated direct child of `artifacts/`, publishes runtime output plus an explicit public-doc/license allowlist, and verifies staging, ZIP parity, checksums, public copy, user-data exclusions and the Inno installer source boundary. `PLAN.md`, `HANDOFF.md`, `AGENTS.md` and `SPEC.md` cannot enter the portable or installer input tree. The README's internal validation references were removed; no Spotify/v0.6 marketing or release-note work was done.

The isolated validation package at `artifacts/phase4-validation` has 487 staging files, 487 ZIP entries, seven licenses and zero forbidden paths. ZIP SHA256 is `e0703a0fdfbe6490b45cb7970d9283b36a68626fdfea2a4ce033687259788830`; the ZIP matches staging byte-for-byte. The executable reports `0.5.4+354034bfc13e269ed6ae5253c8a689e7c8000161`, is `NotSigned` as documented, and launched/closed cleanly under an isolated `LOCALAPPDATA`. No Inno compiler or installer introspection tool is installed; installer source/payload routing passed, but no compiled-installer result is claimed.

The focused package-policy checks, locked restore, Release build (0 warnings/errors), full 284-test suite (no skips) and `git diff --check` pass. AUD-01 is closed for both portable output and the installer input boundary. v0.5.4, `main`, tags and public releases remain unchanged. Do not merge, tag, publish v0.6.0 or start the final independent/second-machine gates from this handoff. See the Phase 4 section in `PLAN.md` for the complete audit evidence.

# Previous handoff — v0.6.0 Phase 3 daily-use state implemented

Phase 3 is implemented on `codex/v0.6.0`: Output Pause has 5/15/30-minute, until-next-track and until-resumed modes distinct from master Enabled; all text/typing paths share one output eligibility gate, entry clears once best-effort, Manual ownership/drafts and remaining hold survive, and resume has no replay. Until-next-track persists only a TrackIdentity hash and expires only on a different coherent accepted recording. Playback, multi-player timing/arbitration, lyrics and artwork are unchanged.

Atomic `runtime-state.json` now holds only bounded normal geometry, desired Normal/Maximized state, stable section ID and pause state. A pure multi-monitor policy handles negative coordinates, missing displays, partial/off-screen/oversized/corrupt bounds and minimum sizes. Start Minimized applies before Show, never replaces the desired visible state, and tray/single-instance Open restores the saved section/state. UI and diagnostics distinguish Active, Paused and Off without persisting songs, lyrics, drafts or listening history.

Focused pause tests passed 16/16, runtime/lifecycle tests 21/21, and the relevant subsystem 134/134. Final locked restore passed, the Release solution built with zero warnings/errors, the complete suite passed 284/284 with no skips, and `git diff --check` passed. Physical development-app checks covered Normal/non-Home restart, corrected Maximized RestoreBounds, Start Minimized hidden-to-tray plus second-instance restore, timed and indefinite pause restarts, and real Spotify UntilNextTrack surviving a scrub then expiring after Next. Native pointer/UI automation exposed no Windows app surface, so direct mouse/menu checks and physical monitor reconfiguration are not claimed. Output remained disabled; the user's original settings/runtime absence were restored, Spotify was paused, stable v0.5.4 was relaunched, and temporary artifacts were removed. See PLAN.md for full evidence. AUD-01, final audit, second-machine gate, RC and release remain deferred.

# Previous handoff — v0.6.0 Phase 2 production multi-player implemented

Phase 2 is complete on `codex/v0.6.0`; v0.5.4 remains public and `main`/`origin/main` remain `4df40710e673a7c6f585b6eb04f6b866f44aa909`. Production now has exact-ID `SpotifyPlayback`, Apple Music/Spotify/Automatic persisted selection, deterministic fail-closed `PlaybackSourceCoordinator`, Spotify's 5.25-second bounded clock and observation-driven transition settling, GSMTC artwork/transports, conservative package-owned Core Audio volume, source-aware UI and sanitized diagnostics. TrackIdentity keys and Apple's timing/normalization remain unchanged. No seek, Spotify API/OAuth, arbitrary-player framework or public release copy was added.

Autonomous production-path checks on the physically verified Microsoft Store Spotify client covered metadata, artwork, synced-lyric progression, pause/fresh resume, Next invalidation, Previous acceptance with normal player semantics, external scrubs, unique-session volume verification, source switches, close/reopen recovery and Automatic choosing Spotify while Apple Music was idle. Output stayed disabled during playback testing; afterwards Spotify was paused, the user's AppleMusic/enabled preference was restored and stable v0.5.4 was relaunched. Apple Music again had no initialized playable item, so physical Apple Playing and both-Playing remain honest RC gaps; tests cover their deterministic behavior. A second Windows/Spotify machine must verify its exact distribution identifier, metadata, timing, lyrics and transitions before RC. Raw listening reports remain ignored/uncommitted.

Focused Phase 2 tests passed 22/22, the playback/core/lifecycle/artwork/volume/presentation subsystem passed 73/73, locked restore passed, the Release solution built with zero warnings/errors, the complete suite passed 252/252 with no skips, and `git diff --check` passed. See PLAN.md for the implementation and physical matrix. Output Pause Modes, Better Startup State, Daily-use UX, AUD-01 and RC/release remain deferred. Do not merge, tag, release or advertise Spotify publicly from this branch.

# Previous handoff — v0.6.0 Phase 1 in validation

The user approved Phase 1 only. On `codex/v0.6.0`, SPEC.md and AGENTS.md now formalize Windows-only Apple Music + Spotify Desktop with explicit/Automatic semantics and fail-closed source switching; v0.5.4 remains the public release and `main` is unchanged. The production app still selects only Apple Music, but routes it through `IPlaybackSource`/`PlaybackSourceHost`; `PlaybackTimingPolicy` retains Apple's 2-second freshness and integer anchor ceiling while documenting a provisional 5.25-second fractional Spotify policy. SynchronizationEngine keys epochs by source as well as session/track. TrackIdentity keys are unchanged. The exact observed Spotify ID is `SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify`; a second-machine check is deferred.

Five focused new boundary tests and 33 subsystem tests passed. Final locked restore, warning-free Release solution build, 230/230 full tests with no skips, and `git diff --check` passed. Physical Apple Music observation found only an Opened/idle session with empty metadata and zero timeline, so no live playable-item regression is claimed. Phase 2 must implement Spotify Desktop, selection/Automatic, transition settling and physical dual-player checks; later AUD-01/daily-use work is separate. Do not merge, tag, release, bump version or call Spotify publicly supported after Phase 1.

## Previous handoff — v0.6.0 Phase 0 physical validation complete

Research-only development is on `codex/v0.6.0`; v0.5.4 remains stable, and `main` remains at `4df40710e673a7c6f585b6eb04f6b866f44aa909`. The isolated spike code commit is `d2e42a31fbf76f310b269280dc4603f961015ae0`; `--auto-validate` physically exercised Spotify through documented GSMTC, while ordinary mode remains observation-only. The strict same-recording 62.124-second baseline had 269 samples, 15 Positions, median/p95 authoritative change intervals 4.611/4.617 seconds and max playing LastUpdatedTime age 4.512 seconds. The exact stable source is `SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify`. Pause/resume, Next, test-only forward/backward jumps, natural transition, graceful close/reopen and Apple Music idle coexistence were physically observed. PLAN.md contains the bounded clock and transition evidence; ignored JSON/text reports are local-only and uncommitted. Unrelated browser media text in an early report was redacted.

Phase 0 finding: **Spotify Desktop GSMTC is technically viable** for synchronized lyrics, but its approximately 4.5-second fractional anchors differ markedly from Apple Music's timing. Phase 1 must specify a Spotify-specific bounded clock/freshness policy and immediately invalidate stale timing when Position/duration reset before new metadata. The current production 2-second horizon must not be silently reused, and the production clock, TrackIdentity and Apple Music behavior remain unchanged. No public Spotify claim or release exists. Next step is explicit user review/approval; only then update SPEC.md, AGENTS.md scope and architecture before production multi-player implementation. Do not begin Phase 1 automatically.

## Previous stable release — v0.5.4 published

Stable release: https://github.com/Teyocesu/LyricsChatbox/releases/tag/v0.5.4

Application/tag source: `6f45d8ff7062f203e30cfa771123fdef6ec2eb8b`. Windows CI `35006554119` passed for that exact application commit, including locked publication verification. Final local locked restore, warning-free Release build and 225/225 tests passed; 108 focused maintenance tests passed.

P4-01 through P4-06 shipped as a focused cleanup patch. Installer cleanup is direct-file, app-managed-name-only and limited to LocalData `downloads`, with active-installer preservation and best-effort failure handling. Parsed automatic cache entries older than 30 days are removed only at the exact evaluated cache path; local LRC, manual matches, corrections and unrelated files are not touched. Correction reset, manual forget and ignore resume delete their recording files and fail closed when deletion fails, including safe legacy-key claiming. LRCLIB, NetEase, update and installer requests share the deterministic runtime product User-Agent derived from assembly version `0.5.4`. Production seek support is removed; the playback spike retains its native seek evidence. README OSC diagnostics now use version-independent `dotnet run` commands.

The ZIP is 84,104,645 bytes with SHA256 `da495c8dfa4d421154ac0b220249bd4eb988eaaf28cb6a4b8fc5aa5ec024d88c`; the installer is 58,202,300 bytes with SHA256 `45cd1d4db52cbf2defd8697620eba2b8800203ed41669df1308c05f2f928aa49`. GitHub reports matching binary digests for the four uploaded assets. The ZIP's 488 files match publish output, seven license copies match their sources, no user-data/test paths are packaged, and the executable reports `0.5.4+6f45d8ff7062f203e30cfa771123fdef6ec2eb8b`. Both binaries are unsigned. The public release body is the reviewed end-user copy.

All filesystem tests used unique temporary roots; no real user LocalData was touched. The documented OSC `dotnet run` invocation is supported by `App.OnStartup`; runtime execution was blocked by the already-running installed single-instance process and did not change user data.

After publication, `codex/v0.5.4` was verified fully preserved by `main`, deleted locally and remotely, and stale worktree metadata was pruned. The documentation-only publication record follows the immutable application tag. No v0.5.5 work has started.

## Previous stable release

Stable release: https://github.com/Teyocesu/LyricsChatbox/releases/tag/v0.5.3

Application/tag source: `c49696ca7669dec7832b69882a66647dd39ccf22`. Windows CI 35002827288 on the maintenance branch and 35003203697 on `main` succeeded for that exact commit. Final local locked restore, warning-free Release build and 217/217 tests passed; 31 focused maintenance tests passed. Native WPF Apple Music, sleep/resume, OSC, volume and window-state checks were not observable in this runtime and are not claimed.

P3-01 through P3-06 shipped: unchanged presentation values no longer cause repeated WPF assignments; identical null Apple Music status/revision events are suppressed without delaying real observations; suspend and resume use separate playback behavior; successful OSCQuery discovery stops periodic retry until invalidated or explicitly requested; final volume input consumes the pending debounce; and restoration retains normal/maximized window state. Timing-sensitive 50 ms work and the 1.05 s OSC schedule remain in place.

The portable ZIP SHA256 is `23119b756b4d7813057020db5af0dfd60b8e8e0026e08e95b49a1ba2978815c5` (84,101,933 bytes); the installer SHA256 is `949ecdc48ec417d1b438755b30cd14500b15b6602194d1405205f6c2c9dfa5c1` (58,209,107 bytes). Both checksum assets are uploaded; GitHub reports matching digests. The ZIP's 488 files match publish output, all seven license copies match, and no user-data/test paths were found. The public release body matches the reviewed user notes. Both application and installer are unsigned.

All development branches were verified reachable from `main` or immutable tags, then removed locally and remotely. The original worktree's metadata-only mark had identical content to HEAD and became clean when its index was refreshed. Three clean stale worktrees were removed; only the canonical `LyricsChatbox` worktree on `main` remains. GitHub automatic deletion of merged head branches is enabled. The publication-record commit is documentation-only after the v0.5.3 tag. No v0.5.4 work has started.

## Previous stable release

### v0.5.2 published

Stable release: https://github.com/Teyocesu/LyricsChatbox/releases/tag/v0.5.2

Application/tag source: `b86ea34a6e6fe0d0f62a12e77fae226ffcee792f`. Windows CI runs 34999373205 on `codex/v0.5.2` and 34999799897 on `main` both succeeded for that exact commit, including locked publication. Final local validation passed locked restore, a Release build with zero warnings/errors and 209/209 tests.

U2-01 through U2-08 shipped. Adaptive composition is distinct from the explicit three-line mode; context and Compact help are attached correctly; recovery state and Home actions are outcome-aware without duplication; null Apple Music states retain user-facing detail; empty previews use a non-payload placeholder; durations share hour-aware formatting; and updater Markdown becomes bounded plain text.

Published assets:

- `LyricsChatbox-0.5.2-win-x64.zip`: 84,093,306 bytes; SHA256 `6c013518cd26d49e452a0aafc7999d8260aa1b6f07b4407354ac345c87d482a2`.
- `LyricsChatbox-Setup-0.5.2.exe`: 58,208,097 bytes; SHA256 `460e643337adf9103247b662fcdbcc6570af8b418a4058f0db415f5a74a37b0c`.
- Both SHA256 files are uploaded; all GitHub asset digests match local values.

The ZIP contains 488 files matching the publish directory, matching license copies and no user-data files. The fetched release body exactly matches the reviewed public notes. Application and installer are unsigned as documented.

Native targeted UI validation could not be observed because the available computer-use runtime exposed browser surfaces only. The source executable did launch; VRChat output was disabled for the attempt, the process was closed, and the original settings file was restored. Do not claim the requested native state matrix as performed.

The v0.5.1 public release body was rewritten for users before v0.5.2 work; its title, tag, state, URL and assets remained unchanged. No v0.5.3 work has started.

## Previous stable release

Stable release: https://github.com/Teyocesu/LyricsChatbox/releases/tag/v0.5.1

Application/tag source: `fd2beab239abba57479a28a25959f8ea8284cb05`. Windows CI runs 34995871836 on `codex/v0.5.1` and 34996365603 on `main` both succeeded for that exact commit, including locked publication. Final local validation passed locked restore, a Release build with zero warnings/errors and 189/189 tests.

All six maintenance fixes shipped. Track identity now hashes the round-trip duration under identity version 2. When current-key state is absent, a v0.5 whole-second-key file is atomically renamed to the new key; the first claimant owns it, so a formerly colliding recording cannot receive a duplicate. Cache records are revalidated, JSON keys normalize where practical, and legacy manual association/cache pairs upgrade to one atomic version-2 bundle. New writes use only the current key.

OSC dedupe advances only after local UDP `SendTo` succeeds; attempts remain spaced by 1.05 seconds and failures retry only the latest desired state. Manual matches commit as one atomically replaced bundle. Use globally clears the active recording override. Usable Local LRC remains authoritative and blocks remote selection with a removal explanation. Relevant successful retry paths clear stale global errors.

Published assets:

- `LyricsChatbox-0.5.1-win-x64.zip`: 84,086,145 bytes; SHA256 `9c8e7ae9264b38cb1e9f0b9cf7ab8040a00e51ec73e08cc8dd683d7cd088825e`.
- `LyricsChatbox-Setup-0.5.1.exe`: 58,191,056 bytes; SHA256 `ecbd58f8b55ed49e8a2256486c90924487f9ea906c9de7a6ada0a5183cabb2c8`.
- Both SHA256 files are uploaded; GitHub asset digests match local values.

The ZIP contains 488 files matching the publish directory, matching license copies and no user-data/diagnostic files. Application and installer are unsigned as documented. v0.5.0 and older releases are unchanged. No unresolved v0.5.1 gate remains, and no v0.5.2 work has started.

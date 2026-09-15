# Current handoff — v0.6.0 Phase 0 spike preparation

Research-only development is on `codex/v0.6.0`; v0.5.4 remains stable, and `main` remains at `4df40710e673a7c6f585b6eb04f6b866f44aa909`. The isolated `spikes/SpotifyPlaybackSpike` GSMTC probe is observation-only by default. Run `dotnet run --project spikes/SpotifyPlaybackSpike -c Release -- --duration 90 --interactive` from the repository root after the user opens/signs into Spotify Desktop and plays an ordinary song. The probe writes ignored JSON/text under `artifacts/spikes/spotify/<UTC timestamp>`; retain the report locally for assessment, do not commit listening data. Type labels in the console to mark manually performed pause/resume/next/previous/scrubs/close/reopen. `--self-test` verifies spike statistics and exercise opt-in bounds.

The only native evidence so far is a five-second Spotify-closed run with zero GSMTC sessions and no errors. Spotify Windows package is installed but not running; actual SourceAppUserModelId, metadata, anchors and all viability requirements are unproven. Stop at user-assisted physical testing; do not start Phase 1 or claim Spotify support. PLAN.md holds baseline and current evidence.

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

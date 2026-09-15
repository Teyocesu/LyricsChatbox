# Current handoff — v0.5.2 published

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

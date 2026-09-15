# Current handoff — v0.5.2 in validation

Branch `codex/v0.5.2` starts from post-v0.5.1 main commit `c73f55ce55ea3108b06a26035062ad5137e72e07`. U2-01 through U2-08 are implemented, versioned as 0.5.2, and focused presentation/update tests pass. The v0.5.1 public release body was rewritten for users; title, tag, state, URL and assets were verified unchanged.

Native targeted UI validation could not be observed because the available computer-use runtime exposed browser surfaces only. The source executable did launch; VRChat output was disabled for the attempt, the process was closed, and the original settings file was restored. Do not claim the requested native state matrix as performed.

Final locked restore passed, the Release build completed with zero warnings/errors, and the complete suite passed 209/209 with no skips. CI, packaging, artifact consistency/digests and publication are pending. The intended public notes are in `docs/RELEASE-NOTES-v0.5.2.md`.

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

# Current handoff

Read SPEC.md and PLAN.md. Both requested releases are published. v0.1.0 is frozen at 0bfdc91347f6c9642f70bc4f6057d15ff638540c; v0.2.0 at 437dd958dd89056e69779ec6f81d1312d4a00859. Each has a public GitHub release, version tag, Windows x64 ZIP and verified checksum. main contains v0.2 plus this publication-evidence update; codex/v0.2.0 preserves its release point.

Composer, manual ownership/typing/live edit, compact formatting, settings migration and bounded LRCLIB improvements are implemented. Release build has zero warnings/errors; 61 tests pass. The user confirmed the focused headset matrix, including normal/compact/normal, Unicode/long text, real songs/composer, live manual ownership, typing clear and automatic return. Native pause/resume/seeks/cache/LRCLIB and an actual in-flight lookup/track change passed. See PLAN for exact evidence. Musixmatch is blocked by official terms incompatible with approved scope; no scraper/key/tracking was added.

The goal's conditional publication gate was satisfied before v0.2 main/tag/release. See https://github.com/Teyocesu/LyricsChatbox/releases/tag/v0.2.0 . Use scripts/Publish.ps1 for versioned Windows x64 packaging. Do not repeat confirmed checks without a relevant change. Audio alignment remains out of scope. No subagents authorized.

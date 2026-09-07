# Current handoff

Read SPEC.md and PLAN.md. v0.1.0 is published at commit 0bfdc91347f6c9642f70bc4f6057d15ff638540c with public main/tag/release and verified Windows x64 ZIP. The current goal is v0.2.0 on codex/v0.2.0, branched directly from that release.

Composer, manual ownership/typing/live edit, compact formatting, settings migration and bounded LRCLIB improvements are implemented. Release build has zero warnings/errors; 61 tests pass. The user confirmed the focused headset matrix, including normal/compact/normal, Unicode/long text, real songs/composer, live manual ownership, typing clear and automatic return. Native pause/resume/seeks/cache/LRCLIB and an actual in-flight lookup/track change passed. See PLAN for exact evidence. Musixmatch is blocked by official terms incompatible with approved scope; no scraper/key/tracking was added.

Normal feature commits/push are authorized. Final v0.2 main/tag/release is authorized only after critical physical gates. Do not publish that final release early or repeat confirmed checks without a relevant change. Use scripts/Publish.ps1 for the versioned Windows x64 ZIP and checksum. Audio alignment remains out of scope. No subagents authorized.

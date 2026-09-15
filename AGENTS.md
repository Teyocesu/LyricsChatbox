# Agent rules

- Read SPEC.md and PLAN.md before meaningful changes; SPEC is canonical behavior, PLAN is mutable evidence and execution state.
- Do not silently change architecture, providers, privacy, behavior, or acceptance criteria.
- Keep Windows-only scope: Apple Music and Spotify Desktop are the only explicitly supported playback sources. Use the smallest implementation satisfying SPEC; player quirks belong behind the playback boundary, not an arbitrary-player or plugin framework.
- Old-track async work must never affect the active epoch. Network and OSC are unreliable boundaries. Ambiguous matching fails closed.
- Do not copy MagicChatBox implementation. Never commit credentials or secrets.
- Tests protect actual invariants, boundaries, regressions and failures. Use focused checks, then subsystem checks, then full release validation.
- Write GitHub Releases, README user sections, updater notes and in-app text for users. Keep prompts, workflows, models, agents, internal IDs, scope gates, test evidence, branches and diagnostic checklists in PLAN.md, HANDOFF.md or CI unless users need them.
- After a verified release, remove merged or obsolete development branches and stale worktrees locally and remotely; preserve tags/releases and unique uncommitted work, leaving only `main`.
- Keep successful output concise. Update PLAN.md when state materially changes.

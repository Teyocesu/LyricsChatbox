# Agent rules

- Read SPEC.md and PLAN.md before meaningful changes; SPEC is canonical behavior, PLAN is mutable evidence and execution state.
- Do not silently change architecture, providers, privacy, behavior, or acceptance criteria.
- Keep Windows-only, Apple Music-first scope and the smallest implementation satisfying SPEC. No speculative frameworks.
- Old-track async work must never affect the active epoch. Network and OSC are unreliable boundaries. Ambiguous matching fails closed.
- Do not copy MagicChatBox implementation. Never commit credentials or secrets.
- Tests protect actual invariants, boundaries, regressions and failures. Use focused checks, then subsystem checks, then full release validation.
- Keep successful output concise. Update PLAN.md when state materially changes.

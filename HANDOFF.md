# Current handoff — v0.7.0 specification ready for review

- Branch: `codex/v0.7.0`, based exactly on stable `33838408f29b853c8945a0595fc4400420d41d27` (`v0.6.2`). Main remains untouched.
- Goal: review/approve the v0.7.0 four-area design in `SPEC.md`, then begin Phase 0 data models, migrations and pure logic from `PLAN.md`.
- Decisions: simplify Display around Profile/Composition/Lyric Context/Presentation/Preview; one local reusable Decorations picker; per-profile sequential rotation changes only `{message}` and never sends; About owns identity/maintenance/legal; `RefreshOutputPauseView()` remains authoritative.
- Safety: existing composer/formatter/1.05-second latest-state scheduler and Off/Pause/Manual no-backlog boundaries stay in control. Profile rotation is additive with legacy Message fallback; Favorites/My items use isolated bounded atomic `decorations.json` state.
- Known risks: WPF focus/caret behavior needs physical QA; rollback through v0.6.2 can discard unknown rotation JSON if profiles are saved; third-party catalog content requires item-level provenance; the canonical VRChat profile URL is still unknown.
- Next: Phase 0 only after product review. No production code or tests changed yet.
- Product version remains `0.6.2`; there is no `v0.7.0` tag, package or release.

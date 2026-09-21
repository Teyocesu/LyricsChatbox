# Current handoff — v0.7.0 Phase 3.2 implemented; physical picker QA pending

- Branch: `codex/v0.7.0`; Phase 3.2 base `deac40d0fda5691af9bdfe405be2f91190e81d1f`; stable `main`/`origin/main`/`v0.6.2` remain `33838408f29b853c8945a0595fc4400420d41d27`.
- Implemented: Decorations menu naming; Decorations modal title; Favorites-first navigation with explicit Popular default; Suggested deleted; My items moved to the footer; native WPF top reset for result-set changes; clearer fit toggle/footer and truthful unchanged-output state.
- Catalog: version 1, 327 entries/27 Popular items, maximum 384 entries and 1 MiB. Counts are Symbol 72, TextArt 39, Kaomoji 45, Divider 39, Frame 30, Heart 36, Music 36 and Status 30. No duplicate IDs/names/content or external dataset/license change.
- Verification: focused decoration/picker tests `32/32`; locked restore PASS; Release build 0 warnings/0 errors; full tests `451/451`, 0 skips; package policy and `git diff --check` PASS. Multi-axis review found no blocker; Ponytail FULL/complete-diff review removed one unnecessary guard wrapper and found no remaining removable architecture.
- Pending: leave the native Release build open with Output Off for product-owner QA of naming, navigation/default, My items, every scroll-reset trigger, fit copy/budget semantics, catalog variety/search and responsiveness. Do not claim final Phase 3 acceptance.
- Phase 4 remains untouched: token chips, Display redesign, rotating-message UI and contextual Lyric Context/Status UI are deferred. Product version stays `0.6.2`; do not tag, release or merge main.

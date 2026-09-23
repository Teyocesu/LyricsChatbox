# Handoff — LyricsChatbox v0.8.0 Phase 1

- Branch: `codex/v0.8.0`, Phase 1 committed on top of `e04f349b92e986ce80d6ad322d3b40de5c635d1b`. `main`/`origin/main` untouched at `b663555fa604ec86062a6348876f1c27fc31a041`. Product version still `0.7.0`.
- Phase 1 status: shell/sidebar/Output foundation implemented. 240-DIP continuous sidebar, single nav rhythm (Home/Display/Manual/Settings/About), left-marker selected state, bottom-anchored Output with real destination line and static decorative motif, short-height policy. About body and all other pages intentionally still old.
- QA state: locked restore, Release build 0/0, 509/509 tests, package-policy PASS, diff-check clean; native Release QA done (nominal, Settings nav, Output OFF/ON, 820 × 650, About select + restart-restore) with captures in local temp; user app state reset (Home, 1280 × 940, output off). Ponytail FULL pass applied (one using-directive cleanup).
- Next exact action: product-owner physical review of Phase 1 against `docs/visual/v0.8.0-about-reference.png` (sidebar/Output only). Do NOT start About Phase 2 until accepted.

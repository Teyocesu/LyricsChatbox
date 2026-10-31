# Handoff — LyricsChatbox v0.8.0

- Branch `codex/v0.8.0`; product version remains `0.7.0`. Phases 1, 2.3, 2.4 and 3 are closed and frozen; `main`, tags and releases are untouched. `SPEC.md` is normative and `PLAN.md` holds evidence.
- Floating stays release-wide unavailable: saved `Compact` is preserved, and effective output is 144 units with no suffix. Do not claim Floating appearance restoration.
- Home is one full-bleed visual tree: two columns from 840 DIP of usable width, one tabbed section below it, and height scales only the hero. Do not reintroduce reparenting, card wrappers or a Split mode.
- Last gates: locked restore, Release build (0/0), full 545-test suite, package policy, diff check.
- Final release gate: live playback on Home, candidate search results, tab deselection and focus-to-tab retest, non-100% DPI, screen reader and keyboard order, VR headset view, automatic lyrics with Lyric Context, saved-false tray menu.
- Next: Phase 4 Display, on explicit approval. Migrate profile, composition, context, rotation and presentation to continuous sections using Home's styles. Preserve the Floating disabled control and copy, and keep Preview's card on pages not yet migrated. Do not start Manual or Settings.

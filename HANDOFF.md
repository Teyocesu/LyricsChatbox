# Handoff — LyricsChatbox v0.8.0

- Branch `codex/v0.8.0`; product version remains `0.7.0`. Phases 1, 2.3, 2.4, 3 and 4 are closed and frozen; `main`, tags and releases are untouched. `SPEC.md` is normative and `PLAN.md` holds evidence.
- Phase 4 closed by owner decision on automated, native isolated and visual-tree evidence. No physical testing was performed for it.
- Floating stays release-wide unavailable: saved `Compact` is preserved, and effective output is 144 units with no suffix. Do not claim Floating appearance restoration.
- Home and Display are one visual tree each, with two columns from 840 DIP of usable width. Preview drops its card only there; Manual and Settings keep it until their phases. Do not reintroduce reparenting, card wrappers or a Split mode.
- Last gates: locked restore, Release build (0/0), full 545-test suite, package policy, diff check.
- Final release gate, physical: mouse and keyboard on Display, Decorations modal, Tab order with the `TokenChips` empty stop, caption-button overlap at 820x650, non-100% DPI, screen reader, live playback on Home, candidate search results, Home tab deselection and focus-to-tab retest, VR headset view, automatic lyrics with Lyric Context, saved-false tray menu. If `Rename` is physically inaccessible, the release is BLOCKED until fixed.
- Next: Phase 5 Manual, on explicit approval. Migrate Quick messages and the draft and send flow to continuous sections with Home's styles, keeping ownership, hold, Output and Live Edit behavior unchanged. Do not start Settings.

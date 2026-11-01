# Handoff — LyricsChatbox v1.0.0

- Branch `dev/v1.0.0`, renamed from `codex/v0.8.0` with history intact; the next public release is v1.0.0 and product version remains `0.7.0` until release preparation. Phases 1, 2.3, 2.4, 3, 4 and 5A are closed and frozen; `main`, tags and releases are untouched. `SPEC.md` is normative and `PLAN.md` holds evidence.
- Phase 4 closed by owner decision on automated, native isolated and visual-tree evidence. No physical testing was performed for it.
- Floating stays release-wide unavailable: saved `Compact` is preserved, and effective output is 144 units with no suffix. Do not claim Floating appearance restoration.
- Home and Display are one visual tree each, with two columns from 840 DIP of usable width. Preview drops its card only there; Manual and Settings keep it until their phases. Do not reintroduce reparenting, card wrappers or a Split mode.
- Native Chatbox Protection is experimental, off by default and unofficial. It reads local log lines only while enabled and VRChat runs. It never promises detection, and a packet can leave before an opening is detected. Desktop passed real client gates; PCVR integration is unverified by owner waiver.
- Last gates: locked restore, Release build (0/0), full test suite, package policy, diff check.
- Final release gate, physical: mouse and keyboard on Display, Decorations modal, Tab order with the `TokenChips` empty stop, caption-button overlap at 820x650, non-100% DPI, screen reader, live playback on Home, candidate search results, Home tab deselection and focus-to-tab retest, VR headset view, automatic lyrics with Lyric Context, real music playback with Native Chatbox Protection, saved-false tray menu. If `Rename` is physically inaccessible, the release is BLOCKED until fixed.
- Neutral naming is permanent: new branches, commits, PRs, public documentation and release metadata carry no AI model or tool names. History is not rewritten.
- Open before release: startup `/chatbox/typing` false with Output Off. It conflicts with the Off boundary and must be reassessed and, if justified, corrected. Do not weaken the contract to excuse it.
- Next: the typing issue, then Phase 5B, Manual removal and consolidation, on explicit approval. Do not start Settings.

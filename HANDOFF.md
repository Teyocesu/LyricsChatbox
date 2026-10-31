# Handoff — LyricsChatbox v0.8.0

- Branch `codex/v0.8.0`; product version remains `0.7.0`. Phases 2.3 and 2.4 are closed; `main`, tags and releases are untouched.
- Floating is release-wide unavailable (`FloatingCompatibility.Available = false`). Saved `Compact` stays intact; effective output is normal 144-unit formatting with no suffix. Do not claim Floating appearance restoration. `SPEC.md` is normative; evidence is in `PLAN.md`.
- On VRChat 2026.4.1p1 Build 1937 (desktop, private instance) the suffix keeps the normal background. Manual Send showed all 144 characters through `ENDQ`, and Clear emptied the Chatbox. Loopback packets show no suffix, one Clear and no replay in 12 seconds.
- Last gates: locked restore, Release build (0/0), full 543-test suite, package policy and diff check pass.
- Release gate, unless a regression appears sooner: VR headset view, automatic lyrics with live playback, saved-false tray menu and non-100% DPI.
- Next: Phase 3 (Home visual redesign), on explicit approval. Preserve the accepted hero, note, wordmark, music ribbon and About.

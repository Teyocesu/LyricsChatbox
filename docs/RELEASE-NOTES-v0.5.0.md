# LyricsChatbox v0.5.0

Windows/WPF release for native Apple Music and VRChat Chatbox.

- Refined dark interface, cropped app icon, native-size responsive Home layout and internally scrolling recording details.
- Global Appearance: seven accent presets, custom color with contrast protection, four dark background presets and optional local artwork tint/blur.
- Saved display profiles with create, rename, duplicate, edit and delete operations; existing presentation preferences migrate.
- Current, next, previous and Adaptive lyric contexts. Semantic budgeting protects the current lyric before adjacent lines and metadata while retaining the 144-unit, nine-line and grapheme limits.
- Clear recovery states, exact-recording Ignore/Resume and a compact Lyrics Details inspector. Ignoring preserves imported lyrics and cache.
- Editable local Quick Messages prepare an unsent Manual draft, including when Live edit is enabled.
- Local VRChat OSCQuery discovery with manual destination fallback. Discovery does not acknowledge UDP delivery.
- Explicit installer download with SHA256 verification and a second verification before the separate launch action; stable releases only, optional checks and version skipping.
- Apple Music pause/resume, previous/next and active-session volume with click-to-position. Unsupported controls remain disabled; seeking is deferred because the tested Apple Music Windows session ignores position commands.

The Windows x64 installer and portable ZIP are self-contained. Installation does not enable startup or tray behavior; uninstall preserves local data. No accounts, telemetry, audio capture, new lyrics provider or cloud storage. LRCLIB remains primary, with NetEase as a conservative best-effort fallback.

Both application and installer are unsigned. See `docs/WINDOWS-SIGNING.md` for the signing investigation. Checksums verify downloaded bytes; they do not replace Authenticode or guarantee SmartScreen reputation.

Automated release validation: locked restore, Release build with zero warnings/errors, 178 passing tests, and Windows CI including locked publication. The live candidate also passed install/uninstall/reinstall data-preservation checks and an actual verified download of the existing official installer. Exact native and headset evidence and limitations are recorded in PLAN.md; no exhaustive device or mixed-monitor validation is claimed.

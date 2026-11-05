# LyricsChatbox v1.0.0

LyricsChatbox 1.0 brings a redesigned interface, a new look, and several tools that make your VRChat Chatbox easier to set up and to trust.

## Redesigned app

- Home, Display, Settings and About have been rebuilt with a cleaner layout that adapts from small windows to full screen. On narrow windows, Home shows one section at a time through tabs.
- A new logo and an updated visual system. Your accent color and background choices carry over.
- About is now a single place for version, updates, links, diagnostics, the data folder and third-party notices.

## Display and Decorations

- Profiles keep their composition, lyric context, alignment and rotating status messages, and changes save automatically.
- The Decorations menu has a new **Recent** view listing the items you inserted most recently, and many more built-in symbols.

## Help and support

- **Report issue** now opens a short guide: describe what happened, copy a ready-to-paste report, then open GitHub. Diagnostics are included only if you choose to, and you can review them first. Nothing is sent automatically.

## VRChat OSC

- Settings → VRChat OSC shows where output is being sent and how that address was chosen (found with OSCQuery, set manually, or VRChat not detected), plus the time of the last local send.
- **Send test** sends one short test message, following the normal output rules. It is not sent while Output is off, paused or held.
- A successful send means the message left your PC. VRChat does not confirm that a message was received or displayed.

## Native Chatbox Protection (experimental)

- When enabled, LyricsChatbox holds automatic output while you type in VRChat's own Chatbox, then resumes after a short wait.
- It is off by default, unofficial and best-effort. It reads local VRChat diagnostic events, may not detect every opening, and a VRChat update can stop it working.

## Usability, accessibility and fixes

- Clearer keyboard focus, accessible names, and controls that stay reachable at the smallest window size.
- More accurate status wording in several places, plus general stability fixes.

## Compatibility

- Apple Music for Windows and Spotify Desktop remain the supported playback sources.
- **Floating** is temporarily unavailable, because the previous method no longer works reliably with current VRChat. Each profile's saved preference is kept.
- Manual Chatbox messaging and Quick Messages have been removed. Use VRChat's own Chatbox to type messages. Manual lyrics matching is still available.
- Existing settings, profiles, rotating messages, decoration favorites and My items carry over. Quick messages saved by earlier versions stay on disk, unused.
- LyricsChatbox is not affiliated with or endorsed by VRChat.
- The installer and app are unsigned, so Windows may show a warning. A portable Windows x64 ZIP is also available.

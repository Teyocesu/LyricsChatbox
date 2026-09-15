# LyricsChatbox v0.5.1

Focused reliability patch for the Windows Apple Music to VRChat Chatbox workflow.

- Uses precise deterministic recording keys and safely claims/migrates v0.5.0 local lyrics, cache, corrections, manual matches and ignore decisions.
- Retries the latest current OSC payload when the local UDP send fails, retaining latest-state-wins behavior and the 1.05-second cadence.
- Commits each manual match and its synchronized lyrics as one atomically replaced local bundle, preserving previous valid state on failure.
- Makes **Use globally** remove the current recording override so the chosen global timing value applies immediately.
- Keeps imported Local LRC lyrics authoritative and blocks remote match selection with a clear removal path.
- Clears stale global errors after successful correction, manual match, ignore/resume and manual OSC operations.

No providers, privacy behavior or feature scope changed. The installer and portable ZIP remain self-contained and unsigned.

Automated release validation: locked restore, Release build with zero warnings/errors, and 189 passing tests.

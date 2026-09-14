# Current handoff

Latest override: f8a3893 and Windows CI34789674315 passed all178 tests. The local0.5.0-rc.1 portable ZIP and installer now exist under artifacts/v0.5.0-rc.1, with package-receipt.json containing sizes/hashes. The installer was compiled, not executed. The development app is closed; the actual portable executable is running, and a Windows Firewall security prompt appeared for this new path. Do not interact with that security prompt; user must handle it. No publication or visual/physical acceptance. See the final PLAN entry for package checks and limitations.


## Current checkpoint — 2026-09-13

Branch: codex/v0.5.0. Stable v0.4 and its tags remain unchanged. v0.5 is not visually accepted or released. SPEC and the full 11-feature goal remain authoritative; PLAN records detailed receipts and historical acceptance limits.

Latest changes include real capability-gated Apple Music transport, package-verified active-session volume with click-to-position, last-volume-change rescheduling, fixed-height internally scrolling recording details, readable compact Home sections, and removal of whole-dashboard scaling. Home uses the full available width at native text size. Recovery/search remain bounded to the details region. Custom window maximization respects the monitor work area.

Native checks: volume track click changed the active Apple audio session from 1.0 to 0.4469063; recording details expanded without stretching neighbors; 1280x940 -> 1023x713 switched to readable compact Home, then maximize restored the dashboard. After the work-area fix, maximize produced a 1920x1032 window with Output/footer visible, and restore returned to normal. No music was playing during resize checks. Mixed-DPI monitors and the full remaining functional/physical matrix are unverified.

The source Release app is currently open with the latest layout, volume and maximize fixes. Output is off for review; artwork tint remains on by the user's request. Re-read current settings before altering them; older QA selections are stale. Temporary QA profiles/messages were previously removed and their deletion survived restart.

Validation: Release build zero warnings/errors; v05-resize-review.trx passed 175/175 before the last maximize hook, whose subsequent Release build and native maximize/restore passed. Sixty offline layout fixtures were regenerated under ignored artifacts/v05-validation/layout. These do not prove native high-DPI or visual acceptance. Last known remote CI was de2518a / 34661858230; a new development checkpoint is being prepared.

Seeking is deferred by the decision the user delegated on 2026-09-13. Apple Music advertises no seek capability and acknowledged direct calls without changing position. Foreground shortcuts advanced approximately and resumed paused playback; no shortcut fallback is authorized or implemented. The progress bar stays read-only. The probe restored pause through explicit GSMTC TryPauseAsync, not Ctrl+Space.

Next: push the reviewed development checkpoint and inspect its Windows CI; finish native theme/profile/recovery/quick-message/update/OSCQuery workflows and sizing review; obtain actual v0.5 headset checks and explicit visual acceptance; build and verify exact installer/portable assets before final release. Do not mark the goal complete or publish based only on automated tests. No subagents authorized.
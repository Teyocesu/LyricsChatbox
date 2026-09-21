# v0.7.0 execution plan — Phases 0–4.1 implemented

## State and baseline

- Planning date: 2026-09-20.
- Branch: `codex/v0.7.0`, created from exact stable commit `33838408f29b853c8945a0595fc4400420d41d27`.
- At cycle start, `HEAD`, local `main`, `origin/main` and tag `v0.6.2` all resolved to that commit; the worktree was clean. No local/remote `v0.7.0` tag or GitHub `v0.7.0` release existed.
- Product assembly/file version remains `0.6.2`. This planning task changes only `SPEC.md`, `PLAN.md` and `HANDOFF.md`.
- Current goal: obtain product-owner physical QA for the corrected Phase 4.1 Display/token/rotation UI. About implementation, Output redesign, version bump, tag, release and main merge remain out of scope.
- Phase 0/1 evidence: focused rotation/presentation tests `80/80`; locked restore PASS; Release build PASS with 0 warnings/0 errors; full tests `420/420` with 0 skips; package-policy test PASS; `git diff --check` PASS. Quality/security review fixed null profile-entry normalization; Ponytail FULL review removed a redundant interval array and a tiny-set allocation.
- Phase 2 evidence: focused Decorations + rotation regression tests `44/44`; locked restore PASS; Release build PASS with 0 warnings/0 errors; full tests `434/434` with 0 skips; package-policy test PASS; `git diff --check` PASS. Systematic content review found 90 entries, 10,228 source bytes, 19 curated Popular flags, no duplicate IDs/content, no tabs/trailing garbage, maximum content length 48, maximum six lines and no item over 144 UTF-16 units. Security/quality review made the catalog collections actually read-only and moved the shared Unicode validator to neutral ownership; Ponytail FULL found no removable architecture.
- Phase 3 evidence: focused picker/decorations/layout/display tests `50/50`; locked restore PASS; Release build PASS with 0 warnings/0 errors; full tests `447/447` with 0 skips; package-policy test and `git diff --check` PASS. Native WPF interaction QA is still required: the available computer-use host exposed browser tabs but no native Windows app surface, so no visual/keyboard/theme claim is recorded.
- Phase 3.1 evidence: focused decoration/picker tests `31/31`; locked restore PASS; Release build PASS with 0 warnings/0 errors; full tests `450/450` with 0 skips; package-policy test and `git diff --check` PASS. Catalog review found 218 entries, all 90 prior IDs preserved, no duplicate IDs/names/content, no tabs/trailing or blank-line garbage, maximum content length 49, maximum six lines and no item over 144 UTF-16 units. Code-quality review found no blocker; Ponytail FULL and complete-diff Ponytail review found no removable architecture or complexity.
- Phase 3.2 evidence: focused decoration/picker tests `32/32`; locked restore PASS; Release build PASS with 0 warnings/0 errors; full tests `451/451` with 0 skips; package-policy test and `git diff --check` PASS. Catalog review found 327 entries/27 Popular flags with exact requested distribution, no duplicate IDs/names/content, no tabs/trailing/blank-line garbage, maximum content length 50, maximum six lines and no item over 144 UTF-16 units. Multi-axis review found no blocker; Ponytail FULL/complete-diff review removed one unnecessary guard wrapper and found no remaining removable architecture. The product owner then physically approved naming, Favorites/Popular/My items placement, scroll reset, fit language, catalog variety/density and interaction behavior; Phase 3 is closed.
- Phase 4 evidence: focused Display/rotation/manual/picker/presentation tests `160/160`; locked restore PASS; Release build PASS with 0 warnings/0 errors; full tests `473/473` with 0 skips; package policy and `git diff --check` PASS. The first product-owner physical pass found rotation-editor transition/styling defects, unstable Lyric Context discoverability/layout and the missing canonical Status profile; those findings are the bounded Phase 4.1 scope below.

## Current extension points to preserve

- `DisplayProfile` already owns preset, Custom template, fixed Message, Compact/Floating, alignment and Lyric Context; `ProfileLibrary` already supplies bounded versioned profile persistence and selected-profile migration from `AppSettings`.
- `ChatboxComposer`, `LyricContextComposer`, `MessageLayout.Align` and `ChatboxFormatter` already form the composition/budget path. `ChatboxFormatter` alone owns 144/142 UTF-16 visible budgets, nine lines, grapheme-safe truncation, control cleanup and the Compact suffix.
- `ManualChat` already owns Manual/frozen-hold semantics. `OutputPause` and `OutputEligibility` already gate output. `ChatboxScheduler` already coalesces latest desired state, dedupes and paces at 1.05 seconds without a queue.
- `MainWindow`'s 50 ms tick already selects automatic versus Manual desired text and passes one aligned/formatted payload to the scheduler. Rotation can therefore be a pure input to `{message}` rather than an output producer.
- `RefreshOutputPauseView()` already centralizes Output UI derivation. Runtime navigation persists a validated top-level section ID. Update controls, product version helpers and data-folder behavior already exist and can move to About.
- `LocalData` already uses bounded reads and atomic temp-and-replace writes; `LocalData.Presentation` already isolates profile and quick-message files. New decoration state should follow that pattern rather than enter `settings.json`.

## Proposed implementation decisions

### Display

- Page hierarchy: Profile → Composition → Lyric Context → Presentation → Live Preview.
- Existing presets remain; Custom is the only advanced composition editor.
- Token chips replace the selection or insert at the caret in the Custom template, then restore focus/caret.
- Status/Rotating Messages are visible only when Status / Time is selected or a Custom template consumes `{message}`.
- Lyric Context availability is derived from the effective `{lyrics}` use on every initialization and relevant edit.
- Alignment and Floating move visually under Presentation without changing their formatter behavior.

### Decorations

- One reusable picker and one insertion helper replace three ASCII selectors.
- Built-ins use an embedded, versioned, bounded JSON catalog. The initial catalog should be a small project-authored set. External catalog material is optional, not a prerequisite.
- `DecorationItem`: stable ID, Name, Kind, Content, Popular. `DecorationLibraryState`: Version, FavoriteIds, MyItems. A user item adds only ID, Name, Content and Kind.
- Catalog limits: 1 MiB/384 built-ins; state limits: 256 KiB/128 Favorites/64 My items; ID 64 allowlisted characters; Name 40 UTF-16 units; Content 512 UTF-16 units/nine explicit lines. Known kinds only, unique IDs, valid plain Unicode and no disallowed controls.
- Search is a direct in-memory `OrdinalIgnoreCase` match. No database/index, remote fetch, tags, ratings, Recently Used or inspector.
- A prospective-insertion preview calls the same composition/alignment/formatter logic used by the originating editor. It reports final visible usage/truncation but never rewrites stored text.

### Rotation

- `DisplayProfile` receives an optional nested version-1 `MessageRotation`; `ProfileLibrary` remains version 1. The old `Message` field stays as deterministic fixed fallback/mirror.
- Missing rotation migrates to disabled/15 seconds plus one enabled legacy item when Message is nonempty. Invalid rotation normalizes only that profile to the same legacy-safe state.
- Maximum 16 items; allowed intervals 5/10/15/30/60/120 seconds; sequential order only.
- A pure session-only controller tracks current item ID/index and remaining time per profile. It has no timer thread, network or output dependency; `MainWindow` supplies monotonic elapsed time and current automatic eligibility from its existing tick.
- Eligibility requires current profile, rotation enabled, effective `{message}`, master Output On, no Output Pause and no Manual ownership. Ineligible time freezes. An eligible stall past a deadline advances exactly one item and starts a full interval from now; missed intervals are never enumerated.
- Track and lyric changes never enter the rotation API. One enabled item is static. Mutation behavior follows SPEC's deterministic ID-based transition rules.

### About and Output

- Add `About` to the existing runtime-section allowlist without a runtime-state schema bump.
- Move update/status/actions, Open data folder and legal/notice access out of Settings. Add fixed GitHub profile, Releases and Report issue actions, plus Discord `teyocesu` and Copy.
- Fixed About identities are GitHub `https://github.com/Teyocesu`, VRChat `https://vrchat.com/home/user/usr_5560dde5-e00b-4784-a0ef-c6a6f36130d1`, and Discord `teyocesu` as display + Copy only.
- Use one minimal fixed-URI launcher with HTTPS/host allowlisting and bounded failure UI. Clipboard is explicit write-only. No WebView/router/service hierarchy.
- Keep `RefreshOutputPauseView()` as the source of the three compact presentation states: On/Active, On/Paused and Off; when Off, hide pause actions and show an existing pause only as secondary scheduled state.

## Persistence and compatibility matrix

| Store/model | v0.7.0 change | Migration/fallback | Compatibility rule |
|---|---|---|---|
| `settings.json` / `AppSettings` | None | Existing reader/fallback | Preserve every unrelated setting; do not place decoration or rotation state here. |
| `profiles.json` / `ProfileLibrary` v1 | Optional nested version-1 rotation per profile; keep `Message`; raise read cap from 160 KiB to 2 MiB | Missing/semantically invalid nested rotation becomes disabled legacy Message state for that profile; unreadable whole JSON retains current AppSettings migration | Existing profile and selected ID survive for readable libraries. No fallback resets AppSettings. v0.6.2 ignores the new field and reads fixed Message. |
| `decorations.json` v1 | New Favorites/My items state, maximum 256 KiB | Missing/malformed/oversized/unsupported → empty decoration state only | Atomic write; never invalidate settings/profiles. |
| Built-in catalog | New embedded versioned JSON, maximum 1 MiB | Invalid resource → picker unavailable with bounded feedback | No duplicate fallback catalog, runtime network or unreviewed provenance. |
| `runtime-state.json` v1 | Add `About` to valid section IDs | Unknown remains normalized to Home | Geometry and Output Pause remain untouched. |
| `quick-messages.json` | None | Existing behavior | No merge with rotating messages or My items. |
| Corrections, matches, ignore state, lyrics cache | None | Existing behavior | No migration/read-write change. |

Rollback to v0.6.2 shows the mirrored fixed Message and ignores `decorations.json`. Because v0.6.2 can drop unknown profile JSON when saving, user-facing rollback notes must recommend backing up LocalData or not editing/saving profiles before returning to v0.7.0.

## Phase 0 — data model, migration and pure rules — implemented

Purpose: establish additive schemas, validation and deterministic transition functions before UI or ticking.

- Files changed: `src/LyricsChatbox/DisplayProfiles.cs`, `src/LyricsChatbox/LocalData.Presentation.cs`, new `src/LyricsChatbox/MessageRotation.cs`, `tests/LyricsChatbox.Tests/PresentationTests.cs` and new `tests/LyricsChatbox.Tests/RotationTests.cs`. Decoration models remain Phase 2.
- Invariants: ProfileLibrary remains v1; its read cap becomes 2 MiB to contain 20 profiles × 16 worst-case escaped messages plus overhead; legacy Message is preserved exactly and rotation starts disabled; only affected nested data falls back; all counts/sizes/strings/enums/IDs are bounded; new writes are atomic; no AppSettings or unrelated LocalData mutation.
- Deterministic tests: deserialize v0.6.2 profile JSON; empty/nonempty/Unicode Message migration; configuration-save mirror; rollback-compatible fields; invalid interval/count/ID/text/line/control cases; one malformed profile rotation does not discard siblings; old fields/selected ID unchanged; full 20 × 16 × 512 escaped library below the 2 MiB cap; one-over-limit and oversized-file rejection.
- Acceptance: old fixtures round-trip without user-visible presentation loss; the migration result is independent of clock/UI; corruption isolation is demonstrated; `AppSettings` and Quick Messages are unchanged.
- Dependencies: approved SPEC only.
- Out of scope: UI, timer progression, catalog volume/content polish, output sending, version bump.

## Phase 1 — Rotating Messages core — implemented

Purpose: add the pure per-profile runtime state machine. Composition/UI integration remains Phase 4.

- Files changed: `src/LyricsChatbox/MessageRotation.cs` and `tests/LyricsChatbox.Tests/RotationTests.cs`; no `MainWindow`, composer, scheduler, output or OSC file changed.
- Invariants: the state machine has no output reference and no background timer; scheduler cadence stays 1.05 seconds; only `{message}` changes; track/lyric inputs are absent; current state is keyed by profile; inactive/ineligible time freezes; eligible stall exposes one latest result; disabled/one-item lists cause no repeated changes.
- Deterministic tests: 0 items; 1 item; A→B→C→A; disabled skip; edit current; add/reorder without reset; delete/disable current; enable/disable rotation; interval changes; profile switch/frozen remainder; Manual/Output Off/Output Pause/no-`{message}` policy freezes; resume preserves remainder; long eligible stall advances once; runtime advancement leaves the persisted legacy mirror unchanged. Source dependency review proves the core has no track, playback, window, scheduler, output, OSC or I/O reference.
- Acceptance: existing fixed Message behavior is identical with rotation disabled or one item; automatic composition uses exactly one current message; all no-backlog boundaries stay green.
- Dependencies: Phase 0 models and migration.
- Out of scope: final editor visuals, random/conditions/schedules, persisted runtime index, a new send loop.

## Phase 2 — Decorations catalog and local state — implemented

Purpose: implement validated local catalog loading, filtering and isolated Favorites/My items persistence without UI. Prospective insertion diagnostics remain with the Phase 3 originating-editor integration so there is no disconnected second formatting path.

- Files changed: new `src/LyricsChatbox/Decorations.cs`, `src/LyricsChatbox/LocalContentValidation.cs`, embedded `src/LyricsChatbox/Assets/Decorations.json`, `src/LyricsChatbox/LocalData.Presentation.cs`, `src/LyricsChatbox/LyricsChatbox.csproj`, `src/LyricsChatbox/MessageRotation.cs`, new `tests/LyricsChatbox.Tests/DecorationTests.cs` and the canonical workflow documents. `README.md`, `THIRD_PARTY_NOTICES.md`, composer/formatter/scheduler/output and UI files remain unchanged.
- Catalog: version 1, 90 entries/10,228 source bytes with counts Symbol 16, TextArt 8, Kaomoji 12, Divider 12, Frame 8, Heart 12, Music 12 and Status 10. It uses individual Unicode symbols, common short phrases, requester-provided examples and project-composed arrangements; no external dataset or runtime network source is included.
- Invariants: local-only 1 MiB/256-entry fail-closed load; exact eight-Kind allowlist; bounded valid Unicode; immutable built-ins separate from mutable user state; user IDs occupy `user-<guid>`; no user content becomes path/URL/code; stored content is never auto-truncated; formatter/composer/output remain untouched.
- State: `decorations.json` version 1 contains only ordered unique Favorites (128) and My items (64), is bounded to 256 KiB and reuses `LocalData` atomic replacement. Bad reads return empty decoration state without deleting or rewriting the file or affecting settings/profiles/rotation; a later explicit valid save may replace it.
- Deterministic tests: embedded resource/version/size/count/content review; every invalid catalog boundary; stable resource order; name/content/Kind/Popular filtering; Favorites ordering/dedupe/limits/stale resolution; My-item CRUD/namespace/collision/limits/Unicode/control rules; missing/round-trip/worst-case persistence; corruption preservation; explicit replacement and settings/profile/rotation isolation.
- Acceptance: 90-item filtering is direct stable-order `OrdinalIgnoreCase`; catalog failure exposes a bounded status and no partial entries; malformed state affects only Favorites/My items; provenance is auditable without new legal notices; existing formatter remains sole final authority.
- Dependencies: Phase 0 persistence contracts.
- Out of scope: remote/community catalogs, full-text engine, tags, Recently Used, ratings, broad third-party dataset import.

## Phase 3 — Insert picker UI and shared insertion — implemented and physically approved

Purpose: replace the three ASCII selectors with one keyboard-usable picker and one caret-aware insertion path.

- Files changed: new `DecorationPicker.xaml(.cs)`, `DecorationPickerPolicy.cs`, `TextInsertion.cs` and `MainWindow.Decorations.cs`; focused changes to `MainWindow.xaml(.cs)`, `ChatboxOutput.cs`, `MessageLayout.cs` and picker/layout tests.
- Invariants: Custom, Status and Manual use the same owned modal and pure caret-aware insertion helper; one selected item and one final Insert action; caller-owned prospective composition uses current runtime values and the existing alignment/formatter path; picker mutation reuses Phase 2 state/persistence; no direct scheduler/output/OSC path exists.
- Rendering: Symbols/Hearts/Music use the dense presentation, Kaomoji/Status/Popular/Favorites/My items compact rows, Dividers wide rows, and Text Art/Frames whitespace-preserving monospaced previews. Search delegates to `DecorationLibrary.Search`; built-in Kind modes exclude user items while Favorites preserve saved order and My items remain available without the built-in catalog.
- State and failure behavior: catalog/state load once during `MainWindow` initialization; explicit Favorite/My-item mutation rebuilds the small in-memory library and saves; failed saves remain understandable for the session; opening/closing does not write; malformed state and unavailable catalog retain Phase 2 semantics.
- Deterministic tests: caret start/middle/end, selection, multiline Unicode, exact/refused/replacement `MaxLength`, 144/142/nine-line warnings, grapheme parity, all navigation/presentation modes, active-mode search, Favorites ordering, unavailable catalog plus My items and failed decoration-state save isolation.
- Acceptance evidence: all obsolete ASCII controls/handler/model references are absent from production; XAML compiles; focused and full gates are green. Final Phase 3.2 native WPF behavior was physically approved by the product owner.
- Dependencies: Phase 2 catalog/state foundation; prospective diagnostics are completed here against each real editor path.
- Out of scope: drag-and-drop asset management, arbitrary category editing, template library, giant art support.

## Phase 3.1 — Smarter picker and catalog expansion — implemented and superseded by approved Phase 3.2

Purpose: refine the accepted Phase 3 picker without changing its modal, navigation, rendering or insertion architecture.

- Catalog: schema version 1 now has optional built-in-only Group and SearchTerms metadata. The catalog contains 218 project-composed/generic entries: Symbol 48, TextArt 26, Kaomoji 30, Divider 26, Frame 20, Heart 24, Music 24 and Status 20. All prior IDs remain stable; no external dataset or license change was introduced.
- Navigation: Suggested precedes Popular and returns at most 24 built-ins using current fit state, a fixed Custom/Status/Manual Kind preference, Popular and catalog-order tie-breaking. Category modes expose one stable-order Group chip row. Search matches Name, Content, Kind, Group and bounded aliases. Favorites and My items retain Phase 3 behavior; there is no Recent, learning, usage tracking or recommendation persistence.
- Fit and interaction: Fits reuses the originating editor's prospective insertion and existing formatter analysis through a per-picker cache. Required units are reported as Fits/Will truncate, editor overflow remains distinct, and double-click joins Enter/footer Insert while single-click remains selection.
- Deterministic tests: optional metadata validation and immutability; catalog distribution; groups/All/group+search/group+Fits; alias/Kind/Group search; target-aware stable Suggested ranking and bound; final-composition Fits at 142/144/nine lines plus editor capacity; existing insertion, state and failure behavior.
- Acceptance: focused/full gates passed; final physical acceptance is recorded under Phase 3.2.
- Out of scope: Phase 4, nested taxonomies, user tags, fuzzy/full-text search, global fit cache, online content, AI recommendations, Recent/history and usage analytics.

## Phase 3.2 — Final picker UX refinement and catalog expansion — implemented and physically approved

Purpose: apply the final product-owner picker corrections without entering the Phase 4 Display redesign.

- UX: launchers say Decorations menu; modal/heading say Decorations; Insert remains the final action. Suggested and its target ranking are deleted. Favorites is first, Popular is selected explicitly as the default, and My items moves to the footer while retaining its existing in-modal management flow.
- Viewport: category, My-items, group, search and fit-filter changes schedule the native WPF list ScrollViewer to the top after layout. Favorite and My-item mutations refresh in place without requesting a top reset.
- Fit: the toggle says Only show items that fit and explains current VRChat truncation. Footer copy describes the full prospective output and distinguishes fits, truncation, editor capacity and a genuinely unchanged visible output. Current and prospective composition share the same timestamp/context and use the existing composer/alignment/formatter only.
- Catalog: 327 local entries, maximum 384/1 MiB, with counts Symbol 72, TextArt 39, Kaomoji 45, Divider 39, Frame 30, Heart 36, Music 36 and Status 30. Content remains project-composed/generic; no dataset, dependency, network or notice change.
- Acceptance: focused/full gates passed. The product owner physically approved menu/picker naming, Favorites placement, Popular default, My items footer, scroll reset, fit language, the 327-item catalog, density/variety and interaction behavior.
- Deferred work completed in Phase 4: clickable composition token chips, Display reorganization, rotating-message editor and contextual Lyric Context/Status messages UI.

## Phase 4 — Display UX simplification and rotation editor — implemented; physical QA pending

Purpose: reorganize Display around the five user concepts and expose the bounded per-profile message list.

- Likely files: `src/LyricsChatbox/MainWindow.xaml`, `src/LyricsChatbox/MainWindow.xaml.cs`, `src/LyricsChatbox/MainWindow.Presentation.cs`, `src/LyricsChatbox/MainWindow.PresentationState.cs`, `src/LyricsChatbox/DisplayProfiles.cs`, `src/LyricsChatbox/ChatboxComposer.cs`, `src/LyricsChatbox/LyricContextComposer.cs`, `tests/LyricsChatbox.Tests/DisplayTests.cs`, `tests/LyricsChatbox.Tests/PresentationTests.cs`, `tests/LyricsChatbox.Tests/RotationTests.cs`.
- Invariants: preset semantics and profile ownership remain; status visibility derives from effective `{message}`; context availability derives from `{lyrics}` at cold start and every transition; token chips use shared insertion semantics; alignment/Floating and preview payload are unchanged; rotation list edits save only the selected profile.
- Deterministic tests: each preset's visibility; Custom adds/removes `{message}` and `{lyrics}`; cold-start selected Custom without `{lyrics}` disables context immediately; token insertion/selection; profile switching and separate rotation lists; item mutation rules; preview/output parity; existing alignment and normal/floating budgets remain green.
- Acceptance: no large active Status editor when irrelevant; a no-token rotation state looks inactive and explains how to enable it; the page reads Profile/Composition/Status messages/Lyric Context/Presentation/Live Preview without losing features; existing profile migration still passes.
- Implementation: one exact composer-token predicate drives `{lyrics}`/`{message}` support; one derived-state refresh owns Custom, Status, Lyric Context and alignment visibility from cold start onward. Token chips reuse `TextInsertion`. The profile-owned editor supports toggle, supported intervals, bounded add/edit/delete/enable/reorder and the approved Decorations picker with prospective active-message budget wording.
- Runtime: one MainWindow-owned `MessageRotator` receives the existing monotonic Tick and supplies `{message}` before composition. Output Off, Output Pause, Manual ownership and absent `{message}` freeze the remainder. `ManualChat.AutomaticAvailable` resolves timed-hold expiry before composition without duplicating ownership rules. Track/lyric changes do not enter the rotator; formatter/scheduler/OSC ownership remains unchanged; runtime progression writes no state.
- Persistence: `CurrentProfile` now updates presentation fields on the selected profile instead of reconstructing it from AppSettings, preserving Rotation across every unrelated Display edit. Configuration saves keep `DisplayProfile.Message` and `AppSettings.Message` as the first-enabled legacy mirror; duplicate copies configuration under a new runtime ID and rename keeps the existing ID/state.
- Verification: focused Phase 4 suite `160/160`; locked restore PASS; Release build 0 warnings/0 errors; full suite `473/473`, 0 skips; package policy and `git diff --check` PASS. Native smoke launch with Output Off passed; product-owner physical QA is still required.
- Dependencies: Phases 1 and 3.
- Out of scope: new tokens, multiple rotating templates, rules, alternate profile framework or standalone fix architecture for the v0.6.2 cosmetic issue.

## Phase 4.1 — rotation UX and stable Home derived state — implemented; physical QA pending

Purpose: correct only the concrete findings from the first Phase 4 product-owner physical review.

- Editor state: one small value state owns None, Adding and Editing-by-ID, with inline removal confirmation as an Editing substate. Refresh never falls back to the first item. Invalid Add keeps the draft; successful Add transitions before refresh to the new selected ID; Delete returns to None; reorder and enabled changes preserve the selected ID and draft.
- UI: rotating rows reuse the themed `PresetItem` container over a transparent list with semantic dynamic resources and retained keyboard focus/selection. Removal uses inline Cancel/Confirm controls with accessible names. The Custom Decorations launcher sits immediately beside its label; the approved picker itself is unchanged.
- Derived state: both Home and Display Lyric Context cards stay visible. Exact `{lyrics}` support controls enablement and concise unsupported copy without changing the saved ContextMode. Home therefore retains its real second card instead of leaving a layout hole.
- Profiles: canonical Status (`status`, Status, Status / Time, built-in) has no sample text and normalizes to an empty valid rotation. Fresh migration includes it. Existing valid libraries append it idempotently only when the ID is absent and capacity permits; no selection/configuration is overwritten and a full library is preserved unchanged.
- Tests: pure editor transition sequences, invalid-save recovery, delete/add-after-delete, cancellation, reorder/enable stability, derived-card support/copy, fresh/existing/customized/full-capacity profile normalization and Status behavior.
- Runtime invariants: rotator timing, Off/Pause/Manual/no-token freeze, legacy Message mirroring, scheduler ownership and zero runtime writes are unchanged.
- Verification: focused Display/Presentation/Rotation/OutputPause/DecorationPicker suite `168/168`; locked restore PASS; Release build 0 warnings/0 errors; full suite `481/481`, 0 skips; package policy and `git diff --check` PASS. Scope inspection found no runtime-core, README or Phase 5 diff, native rotation dialog or newly hardcoded theme color. Native Release smoke with Output Off confirmed themed rotation rows, no implicit row selection/editor, and the stable disabled Lyric Context card with explanatory copy; product-owner physical QA remains pending.
- Out of scope: Decorations redesign/catalog work, rotation runtime changes, Phase 5/About/Output UI, version bump, tag, release or main merge.

## Phase 5 — About, sidebar and Output UI

Purpose: separate settings from identity/maintenance/legal actions and make Output states unambiguous.

- Likely files: `src/LyricsChatbox/MainWindow.xaml`, `src/LyricsChatbox/MainWindow.Lifecycle.cs`, `src/LyricsChatbox/MainWindow.Updates.cs`, `src/LyricsChatbox/MainWindow.OutputPause.cs`, `src/LyricsChatbox/RuntimeState.cs`, `src/LyricsChatbox/ProductIdentity.cs`, optionally one small new `src/LyricsChatbox/ExternalLinks.cs`, `tests/LyricsChatbox.Tests/RuntimeStateTests.cs`, `tests/LyricsChatbox.Tests/OutputPauseTests.cs`, `tests/LyricsChatbox.Tests/UpdateTests.cs`, and optionally new `tests/LyricsChatbox.Tests/AboutTests.cs`.
- Invariants: About is one persisted top-level section; maintenance controls move, not duplicate; installed version comes from ProductIdentity; only fixed allowlisted HTTPS URLs launch; Discord only copies; local folder/notice targets are app-owned constants; `RefreshOutputPauseView()` stays authoritative; master Off and Pause remain separate.
- Deterministic tests: About normalization/restore; version text; exact allowed/rejected URI cases; launcher/clipboard exceptions become UI failure results; no clipboard read; Output Off/Active/each Paused summary/pause-remains-while-Off/Resume/Change; all existing pause modes and no-replay/typing tests.
- Acceptance: Settings contains settings; About contains identity/version/maintenance/links/legal; no crash on unavailable shell or clipboard; Off hides active pause actions; re-enable returns to an extant pause truthfully; small-height sidebar remains usable.
- Dependencies: fixed About identities recorded above. Existing update/data-folder code is reused.
- Out of scope: embedded browser, arbitrary link input, Discord URL, new pause mode, Output semantics rewrite or duplicate view model.

## Phase 6 — integration, regression, accessibility and hardening

Purpose: prove the four areas compose safely and clean up only implementation duplication discovered by real call sites.

- Likely files: the focused files above plus `tests/LyricsChatbox.Tests/BoundaryTests.cs`, `tests/LyricsChatbox.Tests/MessageLayoutTests.cs`, `tests/LyricsChatbox.Tests/ProductIdentityTests.cs`, `tests/LyricsChatbox.Tests/MaintenancePolicyTests.cs` when applicable, `README.md` only after behavior is implemented and approved.
- Invariants: old-track epoch isolation, playback, lyric resolution, Manual/Pause/Off, typing, Unicode, alignment, formatter and scheduler contracts remain; no secrets/new telemetry/network endpoint; keyboard focus and accessible names are meaningful; package includes required notices and excludes internal planning documents.
- Deterministic checks: focused tests first, then subsystem tests, then full restore/build/test/release validation; `git diff --check`; repository scope/secrets/dependency/license/package audits; explicit rapid transition tests across profile/message/Manual/Pause/Off; small-window/XAML binding smoke checks.
- Acceptance: all automated tests green; no new compiler warnings; no unreviewed dependency; no direct send/second formatter; accessibility basics and failure UI verified; docs accurately describe implemented behavior.
- Dependencies: Phases 0–5.
- Out of scope: physical claims, publication, speculative abstraction extraction. Add a helper only after at least two real call sites or when it enforces a security boundary.

## Phase 7 — physical QA

Purpose: validate actual WPF interaction and upgrade behavior without claiming release readiness from unit tests.

- Likely files: `PLAN.md` for evidence and only defect-driven production/test changes approved from findings.
- Invariants: use a copy of real v0.6.2 LocalData; retain an untouched backup; keep real VRChat output disabled during development UI QA; use loopback OSC only for transmission verification; never overwrite the sole user state copy.
- Physical matrix: upgrade real v0.6.2 state; selected profile/old Message/custom template/alignment/Floating/context/settings/cache/corrections/matches survive; create/edit/reorder/disable rotation; switch profiles; Manual/Off/timed/track/indefinite Pause freeze and resume; long sleep/stall sends no burst; Spotify playback plus changing lyrics does not reset message state; insert every decoration type into Custom/Message/Manual; selection/caret/keyboard/Escape/focus; 144/142/nine-line warnings versus loopback payload; About links/failure handling/Discord copy/data folder/notices; Output card transitions; small window/DPI; restart/persistence and rollback guidance.
- Acceptance: observed results and environment are recorded; failures are fixed and re-run; no real headset transmission is inferred from loopback; product-owner visual acceptance is captured before release preparation.
- Dependencies: Phase 6 green candidate.
- Out of scope: tag, GitHub release, automatic installation, new feature work during QA.

## Phase 8 — release preparation

Purpose: prepare, but do not publish, v0.7.0 only after automated and physical acceptance.

- Likely files: version/project identity, README user sections, release notes/updater metadata, packaging policy/assets/notices and `PLAN.md`/`HANDOFF.md`; exact files depend on the established release workflow.
- Invariants: version bump occurs only here after acceptance; user-facing notes omit internal prompts/models/branches/checklists; licenses ship; old tags/releases remain immutable; main is merged only with explicit approval; no credentials.
- Deterministic checks: clean restore/build/test; portable/installer policy and launch; hashes; GitHub Actions green; diff/scope/secrets/license review; version consistency; candidate artifacts match commit.
- Acceptance: a reviewable release commit/artifact exists with all gates recorded. Tag/release/publication require separate explicit authorization.
- Dependencies: Phase 7 accepted.
- Out of scope: silent tag/release/merge, branch cleanup before a verified release, unrelated maintenance.

## Security and hardening checklist for implementation

- Trust boundaries: bundled JSON, mutable local JSON, user-entered Unicode, fixed external links, clipboard write, packaged notice/data paths and final UDP OSC. There is no authentication, account, upload, web content or remote catalog boundary.
- Validate at each boundary with allowlisted versions/types/intervals/hosts and bounded sizes/counts/lengths/lines. Revalidate final OSC text through existing formatter cleanup.
- Treat catalog/user text only as data: no HTML, commands, executable content, path derivation or URI construction. Reject/normalize invalid UTF-16, NUL and C0/C1 controls other than LF.
- External launch accepts only compile-time HTTPS URIs on the explicit host allowlist and catches shell failures. Clipboard writes only the fixed username on explicit click and catches contention.
- Local state uses known LocalData paths and atomic replacement. Malformed optional state has local fallback; errors shown to users do not expose stack traces or unrelated paths.
- No dependency is required for the planned feature set. Any proposed dependency or external dataset must receive ownership/provenance/license/package review before introduction.

## Research conclusions

- MagicChatBox: its Personal Status feature confirms that rotating user-authored status values are useful. Its broader integrations/rules/composition architecture and custom source-available license make it a product reference only; its source and architecture are not copied. LyricsChatbox confines rotation to the existing profile `{message}` value. Source: https://github.com/BoiHanny/vrcosc-magicchatbox .
- VRChat: official Chatbox OSC documentation states a 144-character limit and at most nine displayed lines. The 2026.2.1 notes document a five-messages-per-five-seconds leaky bucket and an automatic-message exemption. Existing 1.05-second latest-state scheduler remains the conservative unchanged boundary. Sources: https://docs.vrchat.com/docs/osc-as-input-controller and https://docs.vrchat.com/docs/vrchat-202621 .
- Catalog licensing: `kaomojikan/kaomoji-data` is MIT and has a category/data structure that could support a pinned curated subset, but its roughly 1,800 entries exceed the product need. Prefer original low-hundreds content; if any external subset ships, preserve the pinned copyright/license/commit in notices. Unicode's license covers Unicode data, not third-party website curation. Sources: https://github.com/kaomojikan/kaomoji-data and https://www.unicode.org/policies/licensing_policy.html .
- WPF: `TextBox.SelectionStart`, `SelectionLength` and `SelectedText` provide native caret/selection replacement; `Clipboard.SetText` supports the explicit copy action; `ProcessStartInfo.UseShellExecute` is the supported shell launch mechanism. Sources: https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.textbox.selectionstart?view=windowsdesktop-10.0 , https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.textbox.selectedtext?view=windowsdesktop-10.0 , https://learn.microsoft.com/en-us/dotnet/api/system.windows.clipboard.settext?view=windowsdesktop-10.0 and https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.useshellexecute?view=net-10.0 .

## Complexity challenge and deletions

- Are we recreating MagicChatBox? No: no integrations, rules, conditions, schedules, script engine, global status subsystem or copied source. Rotation is one profile field feeding one existing token.
- Is the picker becoming an asset manager? No: immutable built-ins, Favorites, bounded My items and simple search only. Tags, ratings, Recent, inspectors, duplication flows, sharing and remote catalogs are rejected.
- Is rotation a second scheduler? No: the pure controller selects a value; the existing scheduler remains the only sender/pacer/retry owner.
- Are persistence models redundant? No: profile-owned rotation lives with profiles; user catalog state is isolated in one decoration file; AppSettings and Quick Messages remain untouched. A separate favorites file and a global rotation store were rejected.
- Are abstractions premature? The only justified shared pieces are the picker/insertion path used by multiple editors and a fixed-link launcher enforcing a security boundary across multiple About actions. A decoration repository/service stack, generic asset model, rotation service/timer and Output view model are rejected.
- Is migration deterministic? Yes: legacy Message → disabled 15-second list with at most one exact item; fixed mirror rule and per-profile invalid fallback are explicit.
- Can Off/Pause/Manual guarantee no backlog? Yes: eligibility freezes ineligible time; eligible stalls compute one state; only latest composition reaches the existing coalescing scheduler.
- Are concepts understandable? Profile chooses saved behavior, Composition chooses text structure, Lyric Context chooses neighboring lyrics, Presentation chooses alignment/Floating, and Preview shows reality. Rotating Messages appear only when used; Insert performs one task.
- Does About remove clutter? Yes: update/data/legal/link actions move from Settings and are not duplicated.
- Potential slop removed: no `{rotation}` token, no full-template rotation, no new timer thread, no arbitrary URL service, no Discord link guess, no database/search engine, no wholesale external catalog, no persisted runtime deadline, no fifth major feature.

## Open product questions

None for implemented Phases 0–4.1. The canonical VRChat URL is resolved. Phase 4.1 product-owner physical QA remains an execution gate, not an open product decision.

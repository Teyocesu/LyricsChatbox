# v0.8.0 execution plan — Phase 1 COMPLETE; Phase 2 About implemented and delivered, owner QA pending

## State and baseline

- Planning date: 2026-09-22. Branch `codex/v0.8.0` was created from exact released `main` commit `b663555fa604ec86062a6348876f1c27fc31a041`. `git fetch --prune` completed; local `main`, `origin/main` and branch base matched; initial worktree was clean and the `v0.7.0` tag existed.
- At initial planning (2026-09-22), work was documentation and reference preservation only. Product project version remains `0.7.0`; no package or release work has begun.
- Canonical visual reference: `docs/visual/v0.8.0-about-reference.png`. Requirements and adaptation policy are in `SPEC.md` § v0.8.0; the screenshot's inaccurate copy and mock data must not be shipped.

## Existing UI inventory and migration map

| Area | Current implementation | Migration implication |
|---|---|---|
| Shell and navigation | `MainWindow.xaml` uses a 194 DIP sidebar, `DockPanel`, four primary `RadioButton`s plus About in a separate bottom panel, page `Visibility` toggles in `MainWindow.xaml.cs`; selected section is saved in runtime state. | Widen nominal sidebar, keep one nav order, move About into the same visual rhythm, preserve section persistence and caption hit testing. |
| Output | Rounded `Border` inside sidebar; `EnabledBox`, power/status text and Pause/Resume actions; state derived by `RefreshOutputPauseView()` via `OutputSidebarPresentation`. | Replace only presentation shell; keep derivation and actions. Add truthful configured destination and decorative visualizer after compact-space policy is validated. |
| About | Five stacked `Card` borders with identity/privacy, author/project links, Updates, support/diagnostics, notices; About already has working links and updater controls. | Recompose into hero, two-column bands and full-width Updates. Reuse handlers in `MainWindow.About.cs`, `MainWindow.Updates.cs` and `MainWindow.Diagnostics.cs`. Keep expanded updater/diagnostic UI reachable. |
| Other pages | Home dashboard uses multiple cards and its own compact arrangement; Display, Manual and Settings are stacked card forms. Persistent Live Preview currently appears below every non-Home page, including About. | Migrate one page at a time to the continuous surface; keep function and state. Suppress About preview during its phase without deleting preview state. |
| Styles and theme | `Themes/Dark.xaml` owns shared `Card`, typography, navigation and control templates; `Appearance.cs`/`ThemeColors` creates semantic brushes and custom accents. Some XAML gradients and code-behind presentation use literal colors/brushes. | Add narrow shared section/row/nav styles; retain dynamic resources. Audit literal colors per touched screen, not as a global mass edit. Ensure all themed controls retain dark templates. |
| Resize | `MainWindow.xaml` starts at 1280 × 940 DIPs with 820 × 650 minimum. `WindowLayout.Apply` changes shell margins and Home's compact dashboard based on height and width, and moves preview/content hosts. | Isolate About's width/height policy from Home's existing moves. Nominal QA needs a larger capture window; short/narrow QA must verify text, scroll, Output anchoring and focus. |

## Design system implementation decisions

- Surface: continuous canvas; sidebar slightly differentiated; no About cards; interactive controls may retain bounded surfaces for usability. Keep current semantic brushes as the source of colors.
- Type: display wordmark and hero copy, then tracked uppercase section labels, regular aligned row titles/values and muted secondary descriptions. Prefer Segoe UI/Segoe UI Variable already used by the project; avoid fixed text-height clipping.
- Spacing: nominal sidebar ≈230–250 DIP; content gutters ≈30 DIP; lower columns each about half of available width with ≈25 DIP breathing room around a one-pixel divider. Row rhythm ≈42–56 DIP, expanding for wrapped text. Fine accent rules under headings and neutral rules between rows.
- Navigation: five existing destinations, icon + label, low-contrast active tint and narrow accent marker. About remains last in the nav group. Output starts after a top separator and stays bottom anchored.
- Decorative hero: theme-tinted abstract music mark/curves/particles behind the hero, with conservative opacity and no readability loss. Keep expensive effects and resize behavior under review; no external runtime media source.
- Output visualizer: decorative state motif. It does not imply real audio levels, send volume, or OSC delivery. The text and existing actions are authoritative. Test reduced-motion and narrow/short sidebar conditions before keeping any animation.
- Responsive: nominal reference ≈1448 × 1086 image; WPF uses DIPs. Below ≈900 DIP content width, stack About sections in semantic order. About body scrolls at short heights while Output remains bottom anchored; navigation can scroll independently. Do not reduce body type to force the nominal composition into 820 × 650.

## About mapping and content constraints

- Reference geometry: boundary x≈239; content x≈268–1416; hero y≈50–365; lower grid y≈394–949; Updates y≈969–1058. The first lower row has Overview left and Community & Contact right; the second has Project & Tools left and Privacy & Data right; Updates spans the width. Use proportional Grid structure, not absolute Canvas coordinates.
- Overview must use real installed version, Windows x64 support, Apple Music for Windows and Spotify Desktop as playback sources, and Teyocesu. Do not repeat the screenshot's “Local Files and more” playback claim.
- Community & Contact has fixed GitHub and VRChat links and Discord `teyocesu` Copy only. Project & Tools reuses repository, releases, issue, notices, data folder and diagnostic actions. Privacy & Data is explicit about lyrics metadata, optional update network calls and configured OSC destination.
- Updates keeps the existing in-app flow. Its horizontal summary row becomes a wrapping/stacking layout at small widths; release notes, verified installer workflow, progress, skip/cancel and startup preference remain usable as expandable detail.
- The nominal About screen excludes the persistent Live Preview footer. Accessibility, resize, dynamic updater content and theme contrast are the only grounds for geometric adaptation; any deviation from the stored screenshot should be recorded here during implementation with a native capture.

## Risks and checks

| Risk | Planned control |
|---|---|
| Monolithic XAML and `WindowLayout.Apply` mutate element placement; About redesign could disturb Home or preview restoration. | Keep existing named controls/handlers; change one viewport at a time; verify repeated navigation Home → About → Display → About and saved-section restart. |
| Bottom sidebar can overflow at 650 DIP height or 200% display scaling. | Exercise minimum size and high DPI; let navigation scroll while Output controls remain accessible; hide only decorative waveform if needed. |
| Two-column rows wrap unevenly with long localized/dynamic text. | Use `Grid` sizing with Auto rows and value wrapping; test long destination, version, update status and release notes. |
| New hero effects may cost render time or conflict with theme/contrast. | Start with static theme resources; add decoration only after text and resize pass. Check idle responsiveness and reduced motion. |
| The mockup contains false product/source/update claims. | Bind version/state from existing sources and review all About copy against README, `ProductIdentity`, updater and privacy boundaries. |
| Hiding/moving controls may alter tab order or leak default WPF appearance. | Native keyboard/screen-reader spot checks and all built-in/custom themes; avoid new control templates unless shared style is necessary. |

## Phases and physical QA checkpoints

| Phase | Outcome | Focused verification / native checkpoint |
|---|---|---|
| 0 — reference and architecture | Stored reference, canonical SPEC/PLAN/HANDOFF and migration decisions. | Diff/document review; verify branch/base and no product-code or version changes. Product owner reviews geometry, visualizer semantics and responsive policy before implementation. |
| 1 — shell and Output | Sidebar/nav/caption/Output form one continuous surface, existing states and navigation survive. | Navigation persistence and Output-state tests where logic changes; native On/Off/Pause/scheduled-pause, 820 × 650 and keyboard review. |
| 2 — About | Hero, four lower sections, full-width Updates match reference at nominal size. | Focused About/updater tests; native screenshot comparison at nominal dark/rose theme, fixed links/Copy, update expansion, narrow/high-DPI/theming review. |
| 3 — Home | Home adopts surface language without losing transport, lyric, profile, preview and recovery paths. | Focused presentation/playback checks; native ordinary and compact Home QA. |
| 4 — Display | Profile/composition/context/rotation/presentation form aligned continuous sections. | Focused profile/rotation/composition checks; native editor, focus and preview QA. |
| 5 — Manual | Quick messages and draft/send flow use the new hierarchy. | Focused Manual/output checks; native draft, live-edit and ownership QA. |
| 6 — Settings | Appearance, behavior, lyrics and OSC settings migrate without changing values or controls. | Focused persistence/discovery/theme checks; native all-theme and OSC destination QA. |
| 7 — polish | Responsive, accessibility and style cleanup across migrated screens. | Native keyboard, focus, 100/125/150/200% scale, reduced-motion and short-window matrix; remove obsolete styles only after use audit. |
| 8 — physical acceptance | Product owner compares native app against canonical About image and exercises functional paths. | Record accepted captures/deviations in PLAN; resolve visible regressions before release work. |
| 9 — release preparation | Only after acceptance: version bump, full gates, packaging and user-facing notes. | Locked restore, Release build, full tests, package verification, CI and final Windows smoke. Publication remains a separate explicit release decision. |

## Evidence and decision log

- 2026-09-22: `git fetch --prune` succeeded; initial status clean; `main`/`origin/main`/HEAD all `b663555fa604ec86062a6348876f1c27fc31a041`; `v0.7.0` tag present. Created `codex/v0.8.0` from that exact commit.
- 2026-09-22: inspected `MainWindow.xaml`, navigation, `WindowLayout`, About/Output/updater code, `Themes/Dark.xaml`, `ThemeColors`, product version and README. Stored the user-provided screenshot without alteration.
- 2026-09-22: selected decorative visualizer semantics to avoid implying audio capture or OSC delivery; selected a proportional WPF Grid and ≈900 DIP content breakpoint because the existing 820 DIP minimum cannot accommodate the two nominal columns legibly.
- Current verification: documentation diff and repository state only. No UI build/test claim; no implementation has begun.

## Phase 1 implementation (2026-09-22, branch `codex/v0.8.0`)

Starting HEAD `e04f349b92e986ce80d6ad322d3b40de5c635d1b`, worktree clean, `main`/`origin/main` at `b663555fa604ec86062a6348876f1c27fc31a041`.

Files changed (production):
- `src/LyricsChatbox/MainWindow.xaml` — sidebar widened 194 → 240 DIP (reference ratio 239/1448 ≈ 16.5%); brand spacing refined (36px icon, 15pt title, real `ProductIdentity` version via existing `VersionCaption`); About moved from the separate bottom `AboutNavigationPanel` into `NavigationPanel` beneath Settings (order Home/Display/Manual/Settings/About); Output card `Border` removed and replaced by a continuous-surface bottom region: 1px `LineBrush` separator, existing `EnabledBox` toggle + `OutputPowerText`, status text, new `OutputDestinationText`, unchanged Pause/Resume/Change controls, new static `OutputVisualizer` motif (18 theme-accent bars, non-focusable, no narration).
- `src/LyricsChatbox/Themes/Dark.xaml` — `NavigationItem` restyled: transparent default, `SelectedBrush` tint, 3-DIP left `AccentBrush` marker visible only when checked, `HoverBrush` hover, `FocusBrush` keyboard-focus border, corner radius 8 → 6. All `DynamicResource`; no hardcoded rose.
- `src/LyricsChatbox/OutputSidebarPresentation.cs` — added pure `FormatDestination(host, port)`: `To host:port`, IPv6 literals bracketed (`To [::1]:9000`); no receiver-presence claim.
- `src/LyricsChatbox/MainWindow.OutputPause.cs` — `RefreshOutputPauseView()` additionally drives the destination line from `destinationSelection.Effective(settings)` (actual configured/discovered destination, no parallel state) and motif opacity (1.0 Active, 0.35 Off/Paused). `Describe()` semantics untouched.
- `src/LyricsChatbox/MainWindow.Presentation.cs` — `FindNavigationButton` searches the single `NavigationPanel`; no `AboutNavigationPanel` references remain anywhere.
- `src/LyricsChatbox/WindowLayout.cs` — nominal sidebar padding updated for 240 DIP; short-height policy extended only with visualizer-hide-first (`OutputVisualizer` collapses below 540 content height; text/controls always stay). Version collapse, nav-scroll, and content behavior unchanged.

Files changed (tests): `tests/LyricsChatbox.Tests/AboutTests.cs` — 5 new `FormatDestination` cases (IPv4, localhost, LAN, two IPv6). No existing test weakened.

Visual decisions: static (non-animated) motif shipped — animation would need a second runtime subsystem for purely decorative value; text stays authoritative. Destination prefix `To …` kept from the reference role; IPv6 uses bracket form. Sidebar is a flat continuous surface: no rounded card, no outer border beyond the 1px content separator.

Responsive: 820 × 650 verified native — nav fully visible, Output text + Pause reachable, motif visible (content height > 540 threshold); below-threshold heights collapse motif first, then version, with nav scrolling. Content pages untouched (expected old-card mix).

Test evidence: baseline focused 64/64 green before edits; after: locked restore, Release build 0 warnings/0 errors, full suite 509/509 green, `Test-PackagePolicy.ps1` PASS, `git diff --check` clean.

Native QA evidence (Release exe, PrintWindow captures): nominal Home (shell/nav/Output/destination/dim motif), Settings via UI Automation select (marker follows, old cards usable), Output ON (Active + Pause + full-opacity motif), 820 × 650 minimum (all reachable), About select + restart-restore (About persisted and restored), toggle back OFF. QA side-effects reset (Home, 1280 × 940, Enabled=False). No VRChat running, so no real transmission occurred. Captures: `phase1-window.png`, `phase1-settings.png`, `phase1-on.png`, `phase1-min.png`, `phase1-about.png` (local temp, not committed).

Deviations from reference: brand version shows real v0.7.0 (product truth; no 0.8.0 bump); Output header keeps checkbox-toggle-left + ON/OFF text-right instead of a right-side switch (existing accessible control preserved); motif is static, not animated; body pages remain old cards (Phase 2+ scope).

Deferred: About body redesign (Phase 2); multi-theme visual sweep beyond Rose/Midnight (geometry identical, DynamicResources only — needs product-owner eyes); 125/150/200% DPI matrix (policy unchanged, owner to confirm).

## Phase 1.1 shell fidelity correction (2026-09-23, branch `codex/v0.8.0`)

Starting HEAD `fae9efde1eb07acf47a94a5c77181b0eb848ed41`, worktree clean, `main` at `b663555fa604ec86062a6348876f1c27fc31a041`. Owner verdict on Phase 1: functional PASS, visual fidelity FAIL — sidebar kept too much v0.7.0 language. No Phase 2 content; no behavior change.

Files changed (production):
- `SPEC.md` — one-line normative update: waveform is a decorative playback-activity motif (no capture/analysis); animation only while selected source Playing + Output enabled + unpaused; static otherwise, under reduced motion, dim when Off/paused.
- `src/LyricsChatbox/MainWindow.xaml` — brand recomposed vertical/centered (52px current asset, 17pt wordmark, centered version; short heights shrink to 38px via `WindowLayout`); nav icons 20→22, labels to 15pt; Output header is now gear icon + `Output` label with the real content-less `EnabledBox` switch docked right (redundant ON/OFF text removed); status row is a `SuccessBrush` dot + status text, destination, new activity line (`Sending lyrics...`/`Ready`), unchanged buttons, bigger centered 14-bar motif (56px, wave silhouette).
- `src/LyricsChatbox/Themes/Dark.xaml` — nav rows get more rhythm (padding 16,13; margin 6); new shared `ContextMenu` (explicit dark template — the default template leaked a white gutter), flat `MenuItem` and `Separator` styles, all `DynamicResource`. Fixes “no white surface” violation; only flat menus exist (Pause menu).
- `src/LyricsChatbox/OutputSidebarPresentation.cs` — pure additions only: `ActiveStatusText`/`SendingText`/`ReadyText` constants and `IsSending(enabled, paused, sourcePlaying, automaticAvailable)`. `Describe()` semantics untouched.
- `src/LyricsChatbox/MainWindow.OutputPause.cs` — `RefreshOutputPauseView()` derives active/sending from existing state (`engine.Snapshot.State`, `manual.AutomaticAvailable`), drives dot/activity/destination/opacity, and starts/stops the Storyboard on transitions only (single bool guard, no timer/engine). Animation additionally gated on `ClientAreaAnimation`.
- `src/LyricsChatbox/WindowLayout.cs` — brand margin/icon short-height values for the new vertical brand.

Files changed (tests): `AboutTests.cs` — 6-case `IsSending` theory. No existing test weakened.

Ponytail FULL notes: static motif shipped in 1.0, animated in 1.1 per explicit owner decision; animation is 8 XAML `DoubleAnimation`s (staggered, ±6 around base heights), no engine/worker; `Storyboard.Stop` restores base heights. Flat-menu template carries a scope comment.

Test evidence: focused 69/69 green before edits; after: locked restore, Release build 0/0, full suite 515/515, package-policy PASS, `git diff --check` clean.

Native QA evidence (Release exe, captures in local temp `p11-*.png`, not committed): 1448×990 reference-like Home/Off; ON-no-playback shows green `OSC Output Active` + `Ready` (no false Sending) with static full motif; dark Pause menu verified after template fix (white gutter gone, disabled item dimmed); Paused shows summary + Resume/Change + dim motif; About at 1448×990; 820×650 minimum all reachable. Playing+ON animation path NOT observed live: no player installed and this box reports `ClientAreaAnimation=False`; gate logic unit-tested, bar names verified against storyboard targets, API usage compile-checked — owner must confirm motion with real playback. No VRChat running; no transmission occurred. QA side-effects reset (Home, 1280×940 Normal, output off, Chrome window restored).

## Next action (Phase 1.1 superseded by 1.2 below)

Product-owner physical review of Phase 1.1 shell fidelity; do not start Phase 2 until accepted.

## Phase 1.2 waveform + shell micro-polish (2026-09-23, branch `codex/v0.8.0`)

Starting HEAD `dc22c0bb4b03d343ab39f6bad0904d4ec5487562`, worktree clean, `main` at `b663555fa604ec86062a6348876f1c27fc31a041`. Owner confirmed 1.1 gates work (playback gate, `Sending lyrics...`, animation runs) but rejected: equalizer-like motif, selected-nav fake bold, loose Pause alignment. Visual-only iteration; no behavior change.

Files changed (production):
- `src/LyricsChatbox/Themes/Dark.xaml` — removed `FontWeight=SemiBold` from the `NavigationItem` checked trigger. Selection is now marker + `SelectedBrush` tint + `AccentBrush` text at identical weight: no width/baseline/row shift.
- `src/LyricsChatbox/MainWindow.xaml` — waveform redesigned to a 13-bar wave silhouette (thin rounded capsules, dip + dominant center peak, progressive taper, ~87 DIP wide, 58px tall): static bars at the edges, 6 staggered center animations (1.2–1.6s, ±4) replacing the 8 faster uniform ones; action `WrapPanel` rhythm 6→12 top margin so Pause/Resume/Change sit flush with the text block above the motif.

Files changed (tests): none — no logic touched (`IsSending`/`Describe`/destination/persistence/OSC all unchanged). Ponytail notes: one-line style deletion, geometry-only XAML swap, one margin value; no new abstractions, state, or semantics.

Test evidence: focused 75/75 green before edits; after: locked restore, Release build 0/0, full suite 515/515, package-policy PASS, `git diff --check` clean.

Native QA evidence (Release exe, captures in local temp `p12-*.png`, not committed): real Apple Music session found paused (not started by QA — left untouched, no audio played). Home/Settings/About selected at 1448×990 — no bold shift, glyphs/labels stable, marker+tint obvious; new static motif reads as one wave form; Pause flush-aligned with 12/16 rhythm; Paused at 820×650 shows summary + aligned Resume/Change + dim motif with About reachable (IsOffscreen=False). Animated-with-playback path still not observable on demand (source paused; pressing play would intrude on the owner's session) — owner already confirmed motion works on 1.1 geometry and only timing/ports changed. No VRChat running; loopback-only destination.

Environment incident (full disclosure): to run the mandated animations-enabled QA I toggled the OS “Animation effects” (SPI_SETCLIENTAREAANIMATION) on; the enable persisted (registry write 23:40) and subsequent OFF calls via every documented parameter combination report success yet fresh-process `ClientAreaAnimation` still reads True (raw SPI GET read 1 even before any write, suggesting this box disagrees with itself). I did NOT bit-twiddle the registry. Owner restore if desired: Settings > Accessibility > Visual effects > Animation effects → Off. No app, repo, or user-data impact beyond that cosmetic OS toggle.

QA side-effects reset: pause resumed (no persisted pause), Home, 1280×940 Normal; `Enabled=True` left untouched as found (owner's own QA state); Chrome window restored.

## Phase 1.3–1.4 wave decision history (2026-09-23)

The owner replaced the equalizer motif with a continuous ribbon and physically reviewed multiple WPF path revisions. Those iterations confirmed the existing Output-state gate and motion behavior, but the latest visual feedback still called out line merging, one-way travel and underuse of the available space. The owner explicitly authorized SkiaSharp for this visual. The old WPF waveform and its storyboard are now removed; no prior renderer remains active.

## Phase 1.5 — Skia music ribbon (2026-09-23)

Committed HEAD remains e0be07fd74109f160ed5e47f6ae97c017507227b on codex/v0.8.0; product version remains 0.7.0. This is local, uncommitted work. No reset, restore, checkout, stash, commit or push.

The Output slot remains 74 DIP and keeps the existing hide-first responsive rule and surrounding shell. MusicRibbonWaveform is an SKElement using SkiaSharp 4.151.2's CPU raster backend. MusicRibbonWaveModel produces 28 traces from normalized horizontal u, depth v and elapsed time t: broad/medium/fine center fields, four uneven localized lobes, a sign-changing twist/spread field, small depth-dependent phase and x perspective, and a smooth edge envelope that converges every trace onto the center axis. Three back-to-front depth tiers vary opacity/width; a restrained same-accent wider pass adds glow. WPF's live AccentBrush supplies color. Canvas pixel dimensions are converted to WPF DIPs using the actual SKElement dimensions, so geometry and stroke widths track display scaling.

Motion is driven by Stopwatch elapsed seconds and CompositionTarget.Rendering, subscribed only while the existing IsSending && ClientAreaAnimation gate is true and the element is loaded and visible. Hiding/unloading detaches rendering; SKPathBuilders and SKPaints are cached and disposed on unload, with only three tier snapshots created and disposed per frame. All other states render deterministic t=0, full geometry; the parent retains the existing Off/Paused dimming.

The project adds only the direct SkiaSharp.Views.WPF reference at 4.151.2. Locked transitives include SkiaSharp/Views.Desktop.Common/NativeAssets.Win32 4.151.2, OpenTK modules 4.3.0, OpenTK.GLWpfControl 4.2.3, and OpenTK.redist.glfw 3.3.0-pre20200830200122. The required MIT/GLFW license texts and Skia native third-party notices are bundled; package-policy allowlist is updated. Publish.ps1 strips only the unneeded native libSkiaSharp.pdb, leaving the runtime DLL and the existing no-symbol policy intact.

Automated evidence: focused geometry tests 5/5; full Release suite 520/520; locked restore PASS; solution Release build 0 warnings/0 errors; package-policy PASS; published ZIP/staging parity PASS (506 files, 12 license/notice files); libSkiaSharp.dll present (12,274,488 bytes); an isolated bitmap/draw smoke using the published SkiaSharp assemblies and native DLL PASS; dotnet list package --vulnerable --include-transitive reports none; git diff --check PASS. The release app's normal executable was not overwritten.

Final validation (2026-09-23, finalizer session): locked restore PASS; Release build 0/0; full suite 520/520; package-policy PASS; fresh `Publish.ps1` to an isolated verify folder PASS (506 files, 12 licenses, ZIP parity + SHA256); libSkiaSharp.dll present, libSkiaSharp.pdb excluded, no stray PDBs; the fresh publish launches with a real window against live Apple Music (Playing) showing Output Active/Sending and the ribbon rendering. The isolated verify folder was removed afterwards; the QA artifact folder remains a local integration artifact only.

## Phase 1.5 visual QA: PASS

Product owner reviewed the Skia music ribbon physically and confirmed: “me gusta como quedó”. The wave is FROZEN — no SKGLElement migration, no SkSL, no trace-count/motion/glow/size/renderer changes.

## Phase 2 — About initial implementation (2026-09-23; superseded after owner review)

Status: the initial Phase 2 implementation, validation and delivery were completed, then product-owner visual review returned FAIL and requested Phase 2.1. The initial two-band section layout and scrollable minimum-size page below are historical evidence; Phase 2.1 supersedes them. Its baseline was `codex/v0.8.0` at `9e63dbae96f28801ad0df5783c992903c9083df0`; `main`/`origin/main` remained `b663555fa604ec86062a6348876f1c27fc31a041`.

Production files changed:
- `src/LyricsChatbox/MainWindow.xaml` — replaced the About card stack with a continuous hero and two-column Grid: Overview / Community & Contact, then Project & Tools / Privacy & Data, followed by full-width Updates. Added a few semantic About styles, thin theme-brush separators, keyboard-focusable action rows, accessible names/live statuses and static, non-interactive, accent-tinted hero curves. Discord remains copy-only; diagnostics keeps copy and export actions; packaged third-party notices remain the existing workflow.
- `src/LyricsChatbox/WindowLayout.cs` — applies the About-only 900-DIP content-width breakpoint, placing sections in the specified single-column reading order below it and remapping the update summary. Suppresses the persistent Live Preview and page heading while About is selected; existing preview reparenting/restoration and all shell/output/ribbon behavior remain in place.
- `src/LyricsChatbox/MainWindow.About.cs` and `src/LyricsChatbox/MainWindow.xaml.cs` — retain existing safe link, clipboard, notices, data-folder actions and route feedback to the relevant section status.
- `src/LyricsChatbox/MainWindow.Updates.cs` — keeps the existing stable update checker/download/verification/skip/cancel/launch flow and controls; initializes the presentation as “Not checked yet” and displays the local time after a completed check. The check timestamp is session-only; no updater state or service was added.

No tests or package dependencies were changed in the initial Phase 2 delivery. Existing focused About/updater/runtime/lifecycle coverage passed 88/88 then; the original full validation is recorded below. Product version remained `0.7.0`; no release, tag, installer or `main` change was in scope.

Original Phase 2 native QA used a temporary copy of the required starting tree and a Release build with an isolated `UserData` root and playback polling disabled in the temporary copy. The normal product data and running playback were not used. Captures are local temporary artifacts, not repository files: `C:\Users\jhvan\AppData\Local\Temp\LyricsChatbox-AboutQA-1790203623086\captures\about-reference-size.png`, `about-lower-updates.png`, `about-minimum-top.png`, `about-minimum-bottom.png`, and `about-blue-graphite.png`. UI Automation expanded Update preferences and confirmed its startup checkbox and the stable update action are exposed by name and visible; no external update request was triggered.

Original Phase 2 result (superseded): at 820×650, the page remained vertically scrollable. The product owner rejected that visual result and requested the no-scroll, higher-fidelity Phase 2.1 correction. The initial implementation used the actual installed display version (`0.7.0`), accurate supported-source/privacy copy, and “Not checked yet” rather than the mock reference's static `0.8.0` / latest-release claim.

Full validation:
- `dotnet restore LyricsChatbox.slnx --locked-mode` — PASS; all projects up to date.
- `dotnet build LyricsChatbox.slnx -c Release --no-restore` — PASS; 0 warnings, 0 errors.
- `dotnet test LyricsChatbox.slnx -c Release --no-restore` — PASS; 520/520.
- `pwsh -NoProfile -File scripts/Test-PackagePolicy.ps1` — PASS.
- Focused About/updater/runtime/lifecycle checks — PASS; 88/88.
- `git diff --check` — PASS after final source and documentation edits.

Delivery: Phase 2 implementation commit `a7eea90` was pushed to `origin/codex/v0.8.0`; `main` was not modified. The delivery-state documentation update is committed and pushed separately after implementation validation.

## Phase 2.1 — About high-fidelity correction (2026-09-23)

Status: implementation and implementation-side validation are complete; product-owner physical comparison remains the next gate. No Phase 3 or v0.8.1 feature was started. Product version stays `0.7.0`.

Changes: the hero now uses the requested factual description and first-person origin copy, a custom WPF vector note, and static theme-aware star/nebula/wave art. Sections flow independently in two columns, with a width/height-responsive layout and no About page scroll container. The compact 820×650 layout retains all content and controls. The Updates band contains the themed Check for updates action and update-preferences flyout; updater behavior remains on the existing handlers. The official VRChat outline logo and Discord Clyde mark are bundled with source/trademark provenance in `THIRD_PARTY_NOTICES.md`. That notice is copied to build and publish outputs; a focused regression test asserts its normal output location and marker. `SPEC.md` now makes the no-scroll minimum-viewport requirement normative.

Native QA (isolated temp user data and QA executable; product data/playback untouched): About was visually checked at 1448×1016 reference-like, 1280×940 and 820×650. All primary rows, Diagnostics, OSC output, Updates status/version/time and its actions fit without clipping. A wheel gesture at 1280×940 did not move the page. Blue/Graphite preserved the geometry and theme accents. The update preferences flyout opened and displayed its startup option. Clicking Third-party Notices from the portable build opened `THIRD_PARTY_NOTICES.md` visibly from the portable output directory. The normal Release output contains the notice file, but its first launch raised a Windows Defender Firewall prompt before the About action could be clicked. No Allow/Cancel choice was made; closing the isolated QA process dismissed the prompt without a firewall setting change, so a physical normal-Release click remains unverified.

Evidence update (2026-09-24): after the final privacy-copy-only wording refinement, the isolated app was rebuilt and visually recaptured at 1448×1016. The earlier 1280×940 and 820×650 captures predate that text edit; no layout code changed in the edit, but the current CUA session exposed no native-app controls to repeat compact captures. Product-owner physical comparison remains the acceptance gate.

Automated validation:
- `dotnet restore LyricsChatbox.slnx --locked-mode` — PASS.
- Focused About tests (`FullyQualifiedName~AboutTests`) — PASS; 28/28.
- `dotnet build LyricsChatbox.slnx -c Release --no-restore` using a temp output root — PASS; 0 warnings, 0 errors.
- `dotnet test LyricsChatbox.slnx -c Release --no-restore` using the same temp output root — PASS; 521/521.
- `pwsh -NoProfile -File scripts/Test-PackagePolicy.ps1` — PASS.
- `git diff --check` — PASS after documentation edits; no whitespace errors.

## Phase 2.2 — About final fidelity and responsive cascade (2026-09-24)

Status: implementation and automated/package validation are complete on `codex/v0.8.0`. Product-owner physical comparison remains the final acceptance gate. Phase 1, updater behavior, app version `0.7.0`, and the deferred v0.8.1 entries remain unchanged.

Changes:
- Replaced the runtime vector hero mark/art with bundled raster resources: `AboutHeroNote.png` (1024 × 1536, 1,251,624 bytes) and `AboutHeroBackdrop.png` (2172 × 724, 2,067,097 bytes). Both use high-quality WPF bitmap scaling. The hero copy now separates a 66-DIP wordmark, 18-DIP description and 15-DIP origin paragraph with fixed 10/14-DIP spacing and 680/760-DIP maximum widths. A horizontal opacity ramp keeps the art subdued behind the copy and more visible at the right. Both PNGs are WPF `Resource` items.
- Set the centered About canvas maximum to 1400 DIP. Wide mode begins at 1000 DIP of usable content width and keeps the two independent columns. Removed the star-sized spacer that pushed Updates down; Updates now follows the taller column stack with a 28-DIP gap. Ordinary rows use 42-DIP wide / 40-DIP compact minimums, 13/14-DIP labels and values, and neutral separators at 0.52 opacity.
- Below 1000 DIP, the existing section panels move into one vertical cascade: Overview, Community & Contact, Project & Tools, Privacy & Data. Hero remains first and Updates last. The compact hero places the note beside the wordmark, then lets the description and origin paragraph span the full content width. The About `ScrollViewer` uses vertical `Auto` and horizontal `Disabled`; changing layout mode resets scroll to the top and reuses the same sections and update controls while restoring keyboard focus after a reparent.
- Compact Updates uses four semantic rows: status, current version, last checked, and the existing check/preferences controls. The former fixed 116-DIP spacer is now content-sized; no control or updater handler is duplicated.
- Privacy copy now states the local processing, sign-in, analytics, lyrics metadata, GitHub update-check, and configured OSC destination facts without implying that the app never uses the network. The Diagnostics primary action uses a copy icon; Export remains a separate secondary control because it is an existing independent action.
- Increased the official VRChat horizontal-logo slot to 48 × 24 DIP wide / 42 × 21 compact. Bundled the Discord official Symbol SVG as a 520 × 384 transparent PNG in official Blurple, with provenance in `THIRD_PARTY_NOTICES.md`. Replaced the OSC branch drawing with the bundled 256 × 256 PNG raster of Microsoft's Segoe Fluent `Network` U+E968 glyph; no font dependency is added at runtime. Generic About row icons and third-party link handlers were otherwise retained.
- Updated the normative About width/scroll rules in `SPEC.md`. README and updater implementation were not changed.

Validation:
- Focused About tests — PASS, 33/33; Release restore with `--locked-mode` — PASS.
- Isolated Release solution build — PASS, 0 warnings / 0 errors. Full Release test suite — PASS, 526/526.
- `scripts/Test-PackagePolicy.ps1` — PASS. Portable self-contained publish — PASS, 506 files / 12 license files; ZIP/staging parity and checksum verification passed. Reflection-based inspection of the published WPF resource bundle found all four new PNG resources.
- `git diff --check` — PASS.
- The CUA desktop inventory exposed no native app surfaces (`apps=[]`), so a live screenshot/resize pass and 100/125/150/200% physical DPI matrix were unavailable. The bundled note is substantially higher resolution than its maximum 100 × 116 DIP rendering and uses `HighQuality` scaling; this is resource/configuration evidence, not a physical DPI pass. Product-owner visual review at wide and compact sizes remains pending.
- Delivery: implementation commit `cd983f4` is pushed to `origin/codex/v0.8.0`. `main` and `origin/main` remain `b663555fa604ec86062a6348876f1c27fc31a041`; no tag, release or version bump was created. Product-owner visual acceptance remains pending.

### Deferred v0.8.1 scope

- Update Available dialog for eligible startup checks, with explicit Download now, Later and Skip this version actions; never auto-install.
- Guided Bug Report and diagnostics helper, with user-controlled copying/sharing and no automatic upload.
- Evaluate a structured GitHub bug issue form.

## Next action

Product owner physically compares Phase 2.1 About with the canonical reference and accepts or requests corrections. Do not start Phase 3 or implement the deferred v0.8.1 scope before that review is complete.

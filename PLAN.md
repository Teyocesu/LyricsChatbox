# v1.0.0 execution plan — Instrumento revamp in progress

## State and baseline

- Retargeted 2026-11-01: the release planned as v0.8.0 is now v1.0.0, and `codex/v0.8.0` was renamed to `dev/v1.0.0` with history intact. Dated entries below keep the names they were recorded with. No v0.8.0 was ever released.
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
- Delivery: Phase 2.2 implementation commits `cd983f4` and `595e6e9` are pushed to `origin/codex/v0.8.0`. `main` and `origin/main` remain `b663555fa604ec86062a6348876f1c27fc31a041`; no tag, release or version bump was created. Product-owner visual acceptance remains pending.

## Phase 2.3 — About final layout and polish (2026-09-25)

Status: the Phase 2.3 physical correction and hero-note refinement are implemented on the same uncommitted `codex/v0.8.0` worktree from `e7efed683b1b5294487ee630c42c0cfa26d21bcf`. Native wide/compact visual QA and automated/package validation pass. Product-owner final visual review remains the acceptance gate; no commit or push is authorized before explicit approval. Phase 1, updater logic, product version `0.7.0`, README, `main`, and deferred v0.8.1 scope are unchanged.

- About now uses a full-bleed `AboutRoot` spanning the whole post-sidebar `ContentPanel`. About removes that panel's outer margin while other pages retain it. A theme canvas fills all four edges; the static atmospheric layer covers the full width behind the scroll viewport and fades to the left/bottom before section rows. The existing hero raster assets remain. `AboutPage` is a separate centered content canvas, capped at 1400 DIP with 30-DIP side gutters and deliberate top/bottom padding. No negative-margin artwork placement remains.
- About's `ScrollViewer` is vertical `Auto`, horizontal `Disabled` at every width. The 1000-DIP usable-content breakpoint reserves 60 DIP of gutters and one scrollbar width before choosing wide columns. A tall wide page naturally has no visible scrollbar; a short wide page can scroll to Updates. Compact retains the approved Hero → Overview → Community → Project → Privacy → Updates order and generous spacing. Width-mode changes reset the offset to the top; the same section controls move between the two independent wide columns and the compact cascade, preserving handlers and avoiding duplicates. Updates follows the taller column with a 28-DIP gap.
- Hero uses the accepted note and wordmark with a Semibold title, 18/24 description in `TextBrush`, 14/20 origin in `MutedBrush`, shorter line lengths, and concise factual copy. Rows retain 40–42-DIP minimum heights and 14/20 body text; section accent rules remain strong while internal separators use lower opacity. The approved Privacy wording is unchanged.
- The official VRChat Press Kit logo is shown through a source rectangle that removes only its transparent internal padding; its file and aspect ratio remain unchanged. Discord Clyde remains official Blurple. OSC now uses a locally rasterized Microsoft Fluent UI System Icons `Branch Fork` 24 Regular icon, pinned to commit `a563cf9166f4f91aa617557ed272612b7f0a2f72` with source/MIT attribution in the packaged third-party notice; the rejected Network glyph asset was removed. Diagnostics exposes distinct labeled Copy diagnostics and Export buttons with the existing handlers, tooltips, automation names, and focus styling.
- Focused About tests pass 37/37, including real WPF vertical overflow with horizontal movement disabled and repeated wide/compact reparenting without duplicated controls. Locked restore, Release build (0 warnings/0 errors), full tests (530/530), package-policy script, and `git diff --check` pass. An isolated portable publish passes staging/ZIP parity (506 files, 12 license files); its WPF bundle contains the new OSC PNG and existing hero/brand resources but no obsolete OSC PNG, and its notice contains the pinned source/license attribution. No installer, tag, release, or version bump was made.
- Native QA attempt: an isolated temporary QA clone used separate `UserData`, a unique mutex, and disabled playback polling. Its Release executable launched with a responsive WPF main window. Computer-use inventory still returned `apps=[]`, and native launch/window APIs were unavailable, so the fullscreen, reference-like, 1280×940, short-wide, breakpoint, 820×650, resize-cycle, and 100%/non-100% DPI visual checks could not be performed. This process-launch check is not visual acceptance.

### Phase 2.3 physical correction pass (2026-09-28; awaiting final note reference)

- The residual fullscreen scrollbar came from accumulated real content height: the 288-DIP hero minimum, 20-DIP eyebrow gap, hero/title/description spacing, 28-DIP Updates gap, and duplicate page/Updates bottom padding. Reduced only those excesses while retaining readable type and `Auto` vertical scroll. At the current 1920×1032 desktop viewport, native QA shows both columns and complete Updates without a scrollbar. A short wide viewport still exposes the scrollbar and reaches the last action; compact retains vertical scroll and no horizontal scroll.
- Diagnostics is one grid row with aligned icon, label, optional wide-only description, and compact trailing `Copy` / `Export` buttons sharing one style and baseline. Existing handlers, tooltips, automation names, focus and feedback remain. Native QA confirmed Copy shows its status and Export opens the save dialog; the dialog was cancelled without writing a file.
- Updates now chooses its own wide, medium, or compact layout using usable content width, independently of About's two-column breakpoint. Wide distributes status/version/last check/actions proportionally; medium uses status above a version/last-check/actions row; compact stacks status/version/last check and keeps the CTA beside its 38-DIP gear. Empty download status no longer consumes a blank final row. The calm backdrop tracks scroll offset so high-energy art stays with the hero instead of passing behind section rows. Compact right gutter is 42 DIP and final page padding is 24 DIP.
- Native WPF QA used the isolated clone (separate data/mutex, no playback polling): fullscreen 1920×1032, reference-like 1448×990, wide-short 1600×650, medium 1380×940, just-above About cascade 1318×940, just-below 1314×940, and compact 820×650. The wide → compact → wide resize cycle preserved one copy of each section/action and proper reading order. At 1600×650, the scrollbar's measured extent/viewport were 1038/650 DIP and scrolling exposed Updates; at 820×650, the bottom actions remained visible with about 23 DIP of final breathing room. VRChat logo and OSC Branch Fork remained legible; no row clipping or horizontal overflow was observed. This is a 100% DPI physical pass; 125/150/200% scaling was not physically exercised.
- Focused About tests pass 39/39. Locked restore PASS; Release solution build 0 warnings/0 errors; full suite 532/532; package policy PASS; isolated portable publish and ZIP parity PASS (506 files, 12 licenses). Published WPF resources include the current note, backdrop, OSC Branch Fork, and Discord mark; the obsolete OSC PNG is absent. `git diff --check` PASS. These checks will need rerunning after the note asset is replaced.
- The owner provided a final luminous note image, which was first bundled exactly as supplied and physically reviewed. The later owner direction narrowed this task to visual integration of that note with the existing right-hand ribbon. The current `AboutHeroNote.png` is an image-edited transparent 1254×1254 PNG (1,051,423 bytes): the supplied note silhouette remains, while finer flowing magenta/violet strands, more translucent interiors and a lighter outline echo the background wave. The backdrop, title and section layout were not changed in this refinement. Native WPF review at 1904×1016 and 820×650 shows the note legible without taking focus from the wordmark; the fullscreen scrollbar remains collapsed and compact scrolling remains available. The owner's direct single-note image and subsequent refinement request supersede the earlier double-note shape direction for this iteration.
- After this asset-only refinement: focused About 39/39, full Release 532/532, locked restore, zero-warning build, package-policy and isolated portable publish/ZIP parity pass (506 files, 12 licenses). The published WPF note resource was byte-compared with the final PNG and matched. Non-100% DPI visual inspection remains unperformed; the 1254-pixel source is bundled for high-quality downscaling.

### Deferred v0.8.1 scope

- Update Available dialog for eligible startup checks, with explicit Download now, Later and Skip this version actions; never auto-install.
- Guided Bug Report and diagnostics helper, with user-controlled copying/sharing and no automatic upload.
- Evaluate a structured GitHub bug issue form.

### Hero branding final pass (2026-09-28)

- Brought the note, existing wave, and live-text wordmark into one luminous visual system. The final transparent note is a restrained, open eighth note with a few rose/violet filaments; `AboutHeroNote.png` is 1254×1254 (705,910 bytes), displayed at 108×108 wide and 54×54 compact. Lyrics uses a near-white 8%-accent sheen; Chatbox uses a readable selected-accent gradient. Both update with the appearance choice, while the title remains real text.
- Kept the backdrop and section layout intact. Tightened note/title optical alignment within the existing wide and compact hero slots; the transparent note sits over the wave without obscuring the title or competing with the wave's brighter strokes.
- Final native WPF regression at 100% DPI: maximized 1920×1032 has no scrollbar; 1406×650 wide-short, 1380×940 medium, and 820×650 compact show vertical scrolling as needed and no horizontal scrolling. Wide-short and compact scrolling reach the final Updates/actions; resize and section cascade remain intact. Output stayed Off. The rebuilt app is open maximized on About for owner review.
- Locked restore, Release build (0 warnings/0 errors), full tests (532/532), package policy, portable publish and exact embedded PNG comparison pass; `git diff --check` passes. Product version remains 0.7.0. Worktree remains dirty: no commit or push. Next action is product-owner final visual approval; Phase 3 and deferred v0.8.1 work remain out of scope.

## Next action

Phases 2.3, 2.4, 3 and 4 are closed. Next is Phase 5, the Manual redesign, on explicit approval. Settings remains unstarted, and deferred v0.8.1 work remains out of scope.

## Phase 2.4 — Floating investigation reconciliation and contract (2026-10-03)

Contract-phase status before implementation: READY FOR IMPLEMENTATION REVIEW. This recorded the research-only state at contract freeze; runtime then remained v0.7.0. Normative behavior is in `SPEC.md` § v0.8.0 normative Floating compatibility contract. The implementation and current status are recorded below.

### Preserved empirical findings

The findings below are **CONFIRMED** prior direct observations supplied by the owner/preserved investigation. This session did not repeat physical QA, OSC probes, installed-app hashing or restoration. It independently reviewed workspace code and current primary sources. The confirmed client regression and its internal cause are separate claims.

- User confirmed Floating preview is correct but the in-game background remains wide even with VRChat Chatbox Size at 100%. The selected installed profile's Floating flag is true, and v0.7.0 already appends the compact suffix to nonempty OSC text.
- The initial LF-to-VT transport hypothesis was insufficient: the user's updated in-game screenshot shows a single long line still fills the available Chatbox width. The regression after VRChat 2026.3.3 Build 1919 is confirmed by the direct isolation below. Timing alone does not establish its internal cause.
- The earlier claim that long unbroken lyric lines caused the Floating failure was incorrect. A clean v0.7.0 build with automatic wrapping was installed, but the user reproduced the same opaque background with short text and explicit breaks. Automatic 20-column wrapping changes layout and is not a Floating fix. Both the wrapping and LF-to-VT experiments have now been removed from workspace source; original line layout and transport are restored.
- An isolated temporary .NET probe loaded the installed DLL and used its real `ChatboxOutput.Send` against an ephemeral loopback UDP receiver. Synthetic short, two-line and 142-unit inputs retained the `03 1F` suffix, correct `/chatbox/input` address, `,sTF` tags, strict UTF-8 and aligned OSC padding. No lyrics or personal message bodies were collected. This verifies the installed formatter/transport, not arbitrary live app state.
- With explicit user authorization in a private VRChat instance and LyricsChatbox paused, independent raw OSC sends compared equal-length `Floating A` (normal) and `Floating B` (suffix `03 1F`). The user confirmed both had the same ordinary background. `Floating C` (suffix `03 1F 200B`, to distinguish trailing-control stripping) also had the same background. The failure is reproducible without LyricsChatbox's compositor, profiles, scheduler or encoder. This client currently does not honor the known minimal-background trick.
- A final sentinel probe sent `Floating D` + `03 1F` + ` TAIL`. The user saw only `Floating D`; end-text behavior still occurs, so simply stripping the entire suffix is an insufficient explanation. It does not prove which internal renderer/layout path VRChat uses.
- The original installed v0.7.0 DLL was restored after user Exit, SHA256 `D94A040AD9DAF3757426074EDE268B50E2C5190BCE1D340825B740251A94F2FE`, hash-matched to the pre-experiment backup. Its isolated real-send probe preserves the suffix and original LF layout without injected wraps. `ChatboxOutput.cs` matches original workspace HEAD again. Synthetic test text was cleared and the original installed executable relaunched. Profiles and unrelated About work remain intact. Floating compatibility is still unresolved on this client; physical acceptance is failed. Any replacement must be proved in-game before changing the sender again; the supported Chatbox OSC API provides no background-width/style control.

### Independently checked primary sources and inference boundary

Fetched/read on 2026-10-03; source facts are distinguished from claims about the private VRChat implementation.

| Finding | Classification and source |
|---|---|
| Live 2026.3.3 is October 2, Build 1919, with Unity 6 migration and keyboard refresh. Its notes do not announce a compact-background regression, cause or fix. | **CONFIRMED:** [official live notes](https://docs.vrchat.com/docs/vrchat-202633). Their silence proves nothing about developer intent. |
| Chatbox documents 144 characters, nine displayed lines including wrapping, text/send/notification arguments and typing. That documented interface has no background-width/style command or client-build signal. | **CONFIRMED:** [official Chatbox reference](https://docs.vrchat.com/docs/osc-as-input-controller#chatbox). Precise UTF-16 counting is the app's conservative policy, not a guarantee in those docs. |
| TMP 3.0.9 `CalculatePreferredValues` enters its line/end branch at ETX (L4621); when ETX is not last it updates `maxXAdvance` and zeros local `renderedWidth` (L4639–4645), terminates (L4686–4688), and returns `renderedWidth` after margins/rounding (L4791–4804). | **CONFIRMED source behavior:** [official package](https://download.packages.unity.com/com.unity.textmeshpro/-/com.unity.textmeshpro-3.0.9.tgz), `package/Scripts/Runtime/TMP_Text.cs`. Download via official CDN redirect; SHA1 `5a427a571275febcf7e63e8b6f1bcd30f67fe77d` matches 3.0.9 dist shasum in [Unity registry](https://packages.unity.com/com.unity.textmeshpro). Metadata confirms 3.0.9. No package installed/executed. |
| Unity's uGUI 2.0.0 mirror for 6000.0.64f1 accumulates maximum preferred visible width before ETX termination; the inspected ETX path does not reset it. | **CONFIRMED source behavior:** [pinned mirror commit](https://github.com/Unity-Technologies/uGUI/commit/02b15c632efaabe526a05df739bb0d3566203cce), [`TMP_Text.cs` L4595 and L4709–4711](https://github.com/Unity-Technologies/uGUI/blob/02b15c632efaabe526a05df739bb0d3566203cce/com.unity.ugui/Runtime/TMP/TMP_Text.cs#L4595). Commit metadata confirms 6000.0.64f1; package.json confirms uGUI 2.0.0. Raw file SHA256 `85fb0f0e4f6854200e33d366762b673688a4e8ead95cddb1c63eb226a37a45cd`. This is a Unity 6 generation comparison, not the exact library in VRChat. |
| These paths explain how ETX followed by another character could previously produce near-zero preferred width while now retaining visible width, with end-text behavior surviving. | **INFERENCE:** strongest technical explanation consistent with sources and observations. VRChat's exact layout call, TMP version/customizations and instrumented before/after path remain **UNVERIFIED**. The earlier exact-client 6000.0.67f1 / later 6000.0.69f1 extension is not retained as independently verified client-source evidence. |
| VRCOSC documents a character trick and its public source appends the same suffix. | **CONFIRMED for that tool's docs/code only:** [maintainer docs](https://vrcosc.com/docs/v2/chatbox), [pinned `ChatBoxManager.cs`](https://github.com/VolcanicArts/VRCOSC/blob/d4eff6c451be16564596699245a60a4a6d6128bd/VRCOSC.App/ChatBox/ChatBoxManager.cs#L350), suffix at L286/L353. No contemporary in-game fix or reproduction for that tool verified; no universal community impact claim. No MagicChatBox implementation inspected/copied. |
| Existing OSCQuery validates destination and writable Chatbox capability, not a trustworthy running-client build. | **CONFIRMED local boundary:** `OscDiscovery.cs` uses NAME, OSC_IP/PORT/TRANSPORT and TYPE/ACCESS. [Official OSCQuery](https://docs.vrchat.com/docs/oscquery) and [VRChat community reference](https://github.com/vrchat-community/osc/wiki/OSCQuery) describe discovery, not a version/compatibility guarantee. **UNVERIFIED:** any trustworthy build signal suitable for this app; do not treat the service-name suffix as a build or assume a LAN destination equals the local client. |

Python TLS rejected the package CDN certificate; normal Windows `Invoke-WebRequest` retrieved the public archive without disabling certificate verification and its registry hash matched. Archive/source inspection used memory and temporary files outside the repository; no production dependency/network call was added.

The regression is confirmed; Unity/TMP attribution remains inference. No workaround has yet been verified. Do not assert definitive Unity causation, this exact VRChat internal path, impossibility of a workaround, developer rejection as a bug, or that all OSC tools are affected.

### Existing dependencies, alternatives and chosen policy

- `ChatboxFormatter.Analyze` strips input controls, budgets 142/144 and appends the suffix only after nonempty compact formatting. `Visible` hides it; `Packet` only encodes. Input text cannot intentionally use ETX to suppress content because `CleanText` removes it. No other content or transport function depends on the marker.
- `ChatboxScheduler.Set` receives a compact bool and dedupes the full formatted payload, so adding/removing the suffix is observable at normal cadence. Its epoch, retry, force-send, pacing and clear semantics do not depend on ETX. Policy-induced changes still go through this one scheduler.
- `ManualChat` owns raw draft/desired/hold/typing state and has no Floating dependency. `MainWindow.Tick` currently passes saved `settings.Compact` separately to context composition, visible Preview (including unsent draft), Manual formatting and scheduler; all require the same effective value.
- `LyricContextComposer.Fits` uses 142/144 before selecting neighbors: changing only the final formatter would still drop context. `MainWindow.Decorations` passes the saved flag to prospective composition and `TextInsertion.Preview`/formatter analysis, including rotating-message and failed-insertion paths. Those fit/filter/counter inputs must also be effective.
- `ApplyPreviewView` currently labels Floating, budgets 142, and uses transparent compact padding from the saved flag; this implies an active effect. README's prior counter claim was also inaccurate: Live Preview counts visible units; Manual counts full payload. No other textual pixel-perfect promise found; Center/Right already says approximate.
- `DisplayProfile.Summary`, initialization/profile selection, `DisplayChanged`, `RememberProfileChanges` and tray actions need saved/effective distinction. Never write effective=false to `settings.Compact`, `UpdatePresentation`, migration or saves. A checked disabled control must retain true through unrelated edits; the tray must not bypass unavailable UI.
- **Decision:** adopt SPEC's release-wide unavailable policy, preserve stored preference, omit `03 1F` while unavailable and use normal 144-unit behavior everywhere. No observed feature depends on the suffix beyond the lost visual effect, two-unit reservation, preview extraction and payload dedupe. The invisible end-text effect has no needed role in current sanitized app output. Avoiding future renderer quirks is a precaution, not a newly observed defect. Retain legacy compatibility behind capability for future verified restoration; no user override.
- Alternatives: retaining the suffix loses capacity with no verified visual benefit; erasing preference/removing Floating is destructive; wrapping/LF-to-VT/`03 1F 200B` failed to restore the effect; another trick is unverified. Build scraping/process/files/logs/service names have no trusted signal and mishandle custom destinations. Remote flags/backend/telemetry add prohibited network/privacy scope. A future verified technique remains possible.
- Proposed authority: one small `FloatingCompatibility` policy exposes availability and pure saved-preference AND capability derivation. Existing formatter/context/scheduler/insertion bool arguments represent effective state. Make both availability states testable without mutable production-global test state. UI derives from the same authority; no scattered literal false gates, persisted capability or service hierarchy. Exact helper shape remains an implementation choice.

### Every surviving dirty hunk reviewed

| File | Reconciliation |
|---|---|
| README.md | Retained rendering dependency/Preview caution; replaced hypothetical failure with the confirmed Build 1919 limitation, explicitly kept current v0.7.0 suffix/142 behavior, corrected visible-versus-payload counters. No future disabled/144 mitigation advertised as current; unrelated content retained. |
| MainWindow.xaml | Corrected only the preserved tooltip to describe the failed current visual effect. Same enabled checkbox, layout and handlers; no implemented capability claim or TMP/control bytes/Unity speculation. Proportional WPF build required. |
| PLAN.md | Preserved all empirical/rollback evidence and its provenance; removed the regression-versus-causation conflation, separated source facts/inference, removed overconfident exact-client/.69 wording, and recorded decision/alternatives/implementation-test design. Prior Phase 2.3 history retained. |
| HANDOFF.md | Replaced the appended causal synopsis with concise continuation state and gates; detailed research lives here. Preserved About closure/QA and frozen hero/wave context. |
| SPEC.md | Removed the investigation sentence from historical v0.2 and made line-layout preservation normative in the new v0.8.0 contract. Explicit precedence freezes preference, capability, budgets/suffix, UI, Preview, restoration and acceptance; no research narrative. |

### Implementation and minimum meaningful tests — design only

Production areas expected to change after independent review:

1. Add the small capability authority; `MainWindow.xaml.cs` Tick supplies its effective value to `LyricContextComposer.ComposeProfile`, both formatter calls and `scheduler.Set`. Existing low-level algorithms, CompactSuffix and OSC encoder can stay intact behind that effective bool.
2. `MainWindow.PresentationState.cs`: effective Preview budget/label/bubble and matching Manual count. `MainWindow.Decorations.cs`: effective context/prospective formatter analysis. No second formatter or fit budget.
3. `MainWindow.xaml`, initialization/`MainWindow.Presentation.cs`/`DisplayChanged`: disabled saved-state checkbox, accessible explanation and safe save paths. `MainWindow.Lifecycle.cs`: unavailable tray action with saved state. `DisplayProfiles.cs`: honest summary presentation without altering profile data. Verify LocalData, do not change its schema/migration.
4. Update relevant README only once behavior exists and record implementation/QA in PLAN/HANDOFF. No unrelated About/provider/playback/privacy/version changes.

Minimum regression boundaries (extend existing suites; no tests written now):

- `PresentationTests`/`LifecycleTests`: actual temporary-directory LocalData profile/settings round trips with saved true and built-in Minimal, select/duplicate, unrelated presentation edit/save and restart preserve true/schema. Exercise UI initialization/profile-switch/save paths, not just serializer equality.
- `DisplayTests`/`BoundaryTests`: saved true + unavailable yields normal 142/143/144/145-unit boundary behavior with no suffix, intact grapheme truncation/empty clears; saved false is byte-equivalent to current normal behavior in both states. Testable available + saved true restores legacy 142-plus-suffix without migration; retain valid legacy tests under that path.
- Shared production-path regression: automatic, Manual Send/Live Edit and unsent Manual Preview use the same effective policy; loopback emitted text matches formatted Preview-visible text and 144 counters/normal bubble. Preserve hold, Off/Pause, current-epoch/no-backlog invariants using existing coverage rather than duplicating the scheduler suite.
- `DecorationPickerTests`/`PresentationTests`: unavailable saved true allows 143/144-unit prospective insertion and rejects truncating 145/tenth explicit line. Adaptive context includes a neighbor whose real aligned composition fits at 143/144 but not legacy 142. Include fixed template/indentation/grapheme boundaries to expose composition-versus-formatting disagreement.
- Focused WPF/tray regression: unavailable action cannot change preference; stored checked true survives unrelated saves, profile switches refresh derived UI, summaries/explanation do not claim active Floating, and policy restoration is testable without a product override. Test behavior, not helper/class trivia.

Implementation physical QA: isolated data first, then authorized real VRChat Build 1919/current-client automatic and Manual/live-edit output with saved true/false, short/multiline/Unicode boundary text, normal capacity and empty clearing; Preview/counters; disabled Display/tray checked/unchecked states, keyboard/screen-reader explanation, profile switch/unrelated edits/restart, Decorations fit and context. Do not call the visual effect restored. A future compatible technique requires separate in-game proof before release. This research session sent no OSC and touched no installed binaries/live settings/playback.

Future validation order: focused compatibility/persistence/UI checks, affected composition/Manual/Decorations subsystem checks, then full release restore/build/test/package and physical gates. Contract phase uses only diff validation and proportional tooltip build; no ceremonial test run.

### Contract-phase validation

- Contract-phase repository gate PASS: branch/HEAD matched the request; exactly the original five dirty paths and no staged changes before/after reconciliation. Every initial diff hunk was reviewed. No production C# changes, tests added, installed-app changes or OSC sends in that phase.
- Contract-phase proportional validation PASS: `dotnet build src/LyricsChatbox/LyricsChatbox.csproj -c Release --no-restore` with `OutputPath` redirected to a temporary directory compiled the corrected tooltip in WPF; 0 warnings, 0 errors. Normal Release output was not overwritten. No full build/test/package run was necessary at that point.
- Contract-phase `git diff --check` PASS; Git reported only LF-to-CRLF conversion notices, no whitespace errors. At that handoff the sole production-file delta was the tooltip and the five-file worktree remained unstaged at HEAD `71636a3e7bfe202567865ae0733d5c99358a85c1`. Implementation followed in the later section below.

## Phase 2.3 final two-column Updates refinement (2026-10-03)

- From 560 to 1119 DIP, Updates uses two columns: the Stable channel block and paired actions are on the left; Last checked and Current version are stacked on the right. The icon has more separation from its text. Below 560 DIP the existing stacked fallback remains; at 1120 DIP and above the wide layout remains unchanged.
- Locked restore and Release build pass (0 warnings/0 errors); About tests pass 40/40, the full suite passes 533/533, and `git diff --check` passes.
- The freshly built Release process is running (PID 52256). Native visual inspection and the requested resize matrix could not be performed in this session: the computer-use adapter reports `apps=[]` and does not expose its native app API. Product-owner visual QA remains pending; no commit or push.

## Phase 2.3 final Updates cascade polish (2026-10-03)

- Added an Updates-only 560-DIP compact-columns breakpoint; narrower layouts retain the stacked fallback, while the existing medium and wide arrangements remain unchanged. Metadata and actions stay grouped with spacing at compact widths.
- Native visual checks passed at 1920×1032, 820×650, and across 875→886 DIP at the internal breakpoint, plus a maximized→compact→maximized resize cycle. No clipping or horizontal scroll appeared; wide Updates retains its prior arrangement.
- Locked restore, Release build (0 warnings/0 errors), About tests (39/39), full tests (532/532), package policy, and final `git diff --check` pass. No settings, profiles, Floating/OSC behavior, or other About sections were changed in this pass. Product-owner visual approval was pending at this point; see the later closure record below.

## Phase 2.3 closure (2026-10-03)

- Product-owner supplied authoritative PHYSICAL QA: PASS. The accepted About includes the hero, note, wordmark, wave, all About sections, Diagnostics, wide Updates, the final cascade Updates layout, responsive behavior, and scrolling.
- Final Updates cascade: Stable channel, helper text, Check for updates, and gear are grouped in the left column; Last checked and Current version are separate stacked metadata blocks in the right column. The narrower fallback remains stacked, and wide layout is unchanged.
- Final validation: locked restore PASS; Release build PASS (0 warnings/0 errors); focused About/Appearance tests PASS (47/47); full Release suite PASS (533/533); package policy PASS; `git diff --cached --check` and `git diff --check` PASS.
- Validation ran with the intentionally preserved unstaged Floating-only README wording, MainWindow tooltip text, and Floating investigation notes present. Those residual changes are docs/presentation only; no Floating runtime behavior was committed or changed by this closure.
- Phase 2.3 is committed and pushed only to `codex/v0.8.0` as `Complete v0.8.0 About Phase 2.3`. Floating investigation remains dirty and uncommitted for Phase 2.4. No merge, tag, release, version bump, or Phase 2.4 implementation was made.

## Phase 2.4 — Floating compatibility implementation (2026-10-03)

Status: implementation and automated validation complete; product-owner UI and physical VRChat QA remain outstanding. The product version stays `0.7.0`. The normative contract remains in `SPEC.md`; investigation provenance and alternatives remain above.

- Added one pure `FloatingCompatibility` authority: the shipped release-wide `Available = false`, `Effective(saved, available = Available)` and one `SummaryLabel`, with no mutable test state or runtime/build detection. Budgets stay owned by `ChatboxFormatter`.
- `MainWindow.Tick` now resolves once and sends the same effective value to Lyric Context, visible/Manual formatting and `ChatboxScheduler`. Preview takes its budget from the formatter's analysis and uses the normal raised bubble and padding while unavailable. Manual budget now uses `ChatboxFormatter.Analyze` visible units and its limit, excluding any legacy suffix. Decorations composition, fit preview and filtering use effective Floating.
- Display continues to load and show the stored `Compact` value, disables the control while unavailable and presents a visible explanation; `DisplayChanged` writes the existing stored value while unavailable. Profile summaries and Preview share one saved-but-unavailable label. Tray keeps the item checked from saved state, labels it unavailable and disables it. Diagnostics retains `Compact` as the stored field and adds `FloatingEffective`.
- README now describes the shipped behavior without asserting Unity/TMP causation. No settings/profile schema or updater changes; no new network behavior. `ChatboxFormatter` retains its legacy 142-plus-suffix implementation and grapheme-safe truncation for an explicitly available test policy.
- New focused regressions cover all four policy combinations; 143/144/145 boundaries; legacy 142-plus-suffix restoration; saved `true` through unrelated edits, profile duplicate/switch/save and restart; diagnostics; automatic/Manual/Preview/scheduler agreement; adaptive context and Decorations fitting; empty clear and grapheme safety.

### Phase 2.4 validation and QA state

- Focused compatibility/formatter/boundary/profile/Display/Decorations/Manual tests: PASS, 138/138.
- `dotnet restore LyricsChatbox.slnx --locked-mode`: PASS. Release solution build: PASS, 0 warnings / 0 errors. Full Release suite: PASS, 543/543. `scripts/Test-PackagePolicy.ps1`: PASS. Final `git diff --check`: PASS.
- First implementation pass (2026-10-03): native inspection could not run, because Computer Use reported `apps=[]` and the live LocalData Output preference is enabled. No live settings were changed and no OSC was sent then. The audit below supersedes this gap.
- Outstanding before any in-game test: tray checked/disabled states on the real tray menu; Decorations 143/144/145 fit in the picker; visual review of Display and Preview.
- Then in the owner's private VRChat session, on the installed client: (1) send a short normal lyric/message; (2) confirm the normal background is expected; (3) confirm text transmits; (4) test a payload above 142 and at most 144 visible units; (5) clear output; (6) Manual send; (7) switch to a profile with saved Floating=true; (8) restart and confirm the preference remains saved. This does not claim the Floating visual effect is restored.
- Branch remains `codex/v0.8.0` at committed HEAD `71636a3e7bfe202567865ae0733d5c99358a85c1`. Implementation is uncommitted for independent review; no commit, push, merge, tag, release or version bump.

### Phase 2.4 audit, reduction and native QA (2026-10-31)

Status: implementation repaired and reduced; automated gates and native loopback QA pass; owner tray, Decorations and in-VRChat QA remain. Still uncommitted on `codex/v0.8.0` at `71636a3e7bfe202567865ae0733d5c99358a85c1`; product version stays `0.7.0`.

- Compatibility recheck: live VRChat is now 2026.4.1 Build 1936 (October 28), open beta last listed Build 1935. [Live](https://docs.vrchat.com/docs/vrchat-202641) and [open-beta](https://docs.vrchat.com/docs/vrchat-202641-open-beta) notes mention chatbox only for the typing indicator and nameplate height; nothing covers background width or control-character handling. The [Chatbox reference](https://docs.vrchat.com/docs/osc-as-input-controller) still has no width/style control. Web searches found no verified restored technique. **UNVERIFIED:** whether the suffix behaves differently on Build 1936, because the failure was physically proven on Build 1919 and this session did not test in-game. The owner's probe below decides that; do not infer a fix from note silence.
- Copy repairs against SPEC: checkbox label `Floating` (was `Floating text`), exact visible explanation, one stale README two-unit claim removed. UI Automation confirms the disabled checkbox exposes the supporting copy as HelpText through its tooltip, so no extra attribute was added.
- Reduction: removed `FloatingCompatibilityResult` (it restated inputs), the duplicated 142/144 constants and `VisibleLimit` (Preview now uses the formatter's `Limit`), `PreservePreference` (its available branch was untestable behind a constant; one inline guard in `DisplayChanged` replaces it), the unreachable tray-click guard on a disabled item, the two differing "unavailable" labels, and the `EffectiveFloating` rename. Pre-existing `LyricContextComposer.Fits` literals were left alone.
- Isolated native QA: a scratch clone patched only for data root, mutex name and no playback polling, with Output on, discovery off and destination `127.0.0.1:19777`, driven by UI Automation with a local UDP capture. Real app results with saved `Compact=true`: checkbox on and disabled, `Toggle()` rejected with `ElementNotEnabledException`; budget `n / 144`; Preview normal bubble, `Floating unavailable`; automatic packets, Manual Send of a 145-unit draft (144 sent, `144 / 144 · trimmed`) and Clear (empty) all carried no U+0003/U+001F. Unrelated alignment edit, Minimal → Lyrics → Minimal switching (checkbox followed the saved value), exit and relaunch all kept `Compact=true` in `settings.json` and `profiles.json`, schema version 1. Live `settings.json` was untouched. Captures at 100% DPI only.
- Not exercised natively: real tray menu, Decorations picker UI, automatic lyric output and Lyric Context with live playback (unit-covered), Available=true build, non-100% DPI. No VRChat client received anything.
- Gates after the final code change: locked restore PASS; Release build 0 warnings/0 errors; focused 117/117; full 543/543; package policy PASS under Windows PowerShell (`pwsh` is not installed); `git diff --check` PASS.

### Phase 2.4 physical QA, performed by the agent at owner direction (2026-10-31)

Status: PASS for every item below. Product behavior unchanged and no code changed. Still uncommitted at `71636a3e7bfe202567865ae0733d5c99358a85c1`; the owner retains final acceptance.

- Client evidence from the VRChat log: `2026.4.1p1-1937-299983b13b-Release`, `--no-vr` desktop, `~private` instance. The log's startup `OSC enabled: False` line did not prevent receipt: all packets below displayed.
- The original probe no longer existed (its temp folders were empty), so it was rebuilt from the Floating A/B definition above. Dry-run bytes verified before sending; LyricsChatbox was not running during the probe.
- Floating A/B on Build 1937: `Floating A` (normal) and `Floating B` + `03 1F` had the same opaque rounded background and width in zoomed captures, and the suffix produced no visible glyph. Legacy Floating is **not restored** on this client, so the unavailable policy stays. This is a short-text desktop observation through captures, not a headset check.
- Isolated build of the current tree (separate data/mutex, no playback polling, discovery off; installed v0.7.0 untouched and not running). Saved `Compact=true` on Minimal, destination `127.0.0.1:9000`.
- Real VRChat Manual: a 144-unit draft (`144 / 144` on both counters) displayed in full through its final `ENDQ`, on the normal background, with no stray glyph. Clear emptied the Chatbox. Output was turned off afterwards.
- Tray (real notification-area menu): `Compact / Floating (unavailable)` disabled and checked; clicking it left `Compact=true`. Only the saved-true state was opened; saved-false tray state was not captured.
- Decorations in the real picker on saved-true Custom: QA 143 `143 / 144 · Fits`, QA 144 `144 / 144 · Fits`, QA 145 `145 / 144 · Will be truncated`; `Only show items that fit` hid only QA 145.
- Display and profiles: checkbox on/off follows Minimal/Lyrics and stays disabled, explanation visible, Preview `Floating unavailable` only on saved-true. `Compact=true` held in `settings.json` and `profiles.json` after switching, forced process exit and relaunch.
- Not covered: VR headset view, other viewers' clients, automatic lyric output with live playback, Lyric Context with live lyrics, non-100% DPI.

### Phase 2.4 final verification (2026-10-31)

Verdict: PASS for implementation and the physical checks recorded above; awaiting owner acceptance. No commit, push, merge, tag or release.

- Manual Send/Clear was already complete; no further VRChat traffic was sent. The Build 1937 capture shows all 144 characters (five rows of 27 plus nine), ending in `ENDQ`, on the normal background. The later capture shows an empty Chatbox.
- Packet evidence, added on loopback because VRChat holds port 9000: on the final tree with saved `Compact=true`, Output on, port `127.0.0.1:19777`, one `/chatbox/input` of 144 units ending `38 39 45 4E 44 51` with zero `03`/`1F` bytes, one empty Clear, and no further packet in a 12-second watch past the 8-second hold. This proves payload bytes, not VRChat rendering; the capture above covers rendering.
- Audit of A–I: stored `Compact` stays true; effective Floating is false on every path; production passes only the effective value to composition, formatting and scheduling; `settings.Compact` now reaches only persistence, summaries, tray state, Diagnostics and the policy input; legacy 142+suffix stays testable through the `available` argument with no mutable state; the disabled checkbox and tray item cannot write, and `DisplayChanged` keeps the stored value; `ManualChat` and `ChatboxScheduler` are unchanged. No unrelated files changed.
- Simplification: `FloatingCompatibilityTests.cs` reduced from 183 to 114 lines, same 10 cases. Removed checks that re-tested formatter behavior already covered by `BoundaryTests`, `DecorationPickerTests` and `DisplayTests`, and the "agreement" test that never exercised `Tick`. Removed the `FloatingAvailable` diagnostics field, a build constant. Corrected the README tray sentence.
- Known limitation: no automated test can detect a future `Tick` or Decorations call site passing `settings.Compact`, because `MainWindow` is not unit-constructed. A source-text test would assert structure, so none was added. Native QA and review cover this boundary.
- Other limitations: the disabled checkbox tooltip is fixed unavailable copy, so any future restoration must change it with `Available`; a future technique needs its own proof.
- Gates on the final tree: locked restore PASS; Release build 0 warnings/0 errors; Floating tests 10/10; full 543/543; package policy PASS; `git diff --check` PASS.
- Evidence stays outside the repository in the session scratchpad `qa24` folder: `vr-1-A.png`, `vr-2-B.png`, `vr-compare.png`, `vr-4-manual-send.png`, `vr-5-manual-clear.png`, `packet-evidence.txt`, `tray-true.png`, `decorations-fit.png`.

### Phase 2.4 closure (2026-10-31)

- The product owner accepted functional QA after the final verification report. Commit-readiness audit: PASS, with no code change since the gates above.
- Moved to the v1.0.0 final release gate unless a specific regression appears: VR headset presentation, how other players see output, automatic lyric playback with Lyric Context, tray menu with a saved-false profile, and 100/125/150/200% DPI.
- Retained deliberately: the legacy 142-plus-suffix path, required by SPEC; the `available` argument that keeps it testable without global state; the inline `DisplayChanged` guard; and the pre-existing 142/144 literals in `ChatboxFormatter` and `LyricContextComposer`, where merging them would be a speculative refactor.
- Raw VRChat logs, screenshots and QA clones stay local and out of Git.

## Phase 3 — Home visual revamp (2026-10-31)

Normative contract: `SPEC.md` § v0.8.0 normative Home contract. Starting HEAD `7b41cbabcef13904e00857ca5679100da5a2e579`, clean worktree.

Impact map before editing:

| Existing structure | Finding | Decision |
|---|---|---|
| `HomeFit`, `HomeDashboard`, `HomeCompact`, `CompactHeroHost`, `CompactSectionHost`, `CompactToolsHost` | Two visual trees joined by reparenting in `WindowLayout.Apply`. | Replace with one tree; mode changes placement and visibility only. |
| `NowPlayingCard`, `HeroGrid` | Lyric competes with metadata and is hidden at compact sizes. | Full-bleed hero; lyric becomes the focal element and stays visible in every mode. |
| Six Home cards, `HomeDetailsScroller`, `InspectorScroller`, quick-message scroller | Nested scrolling and card grid. | Sections in two columns inside one scroller. |
| `HomeSectionBox` ComboBox | Hides section names behind a popup. | Same `SelectedIndex` contract as visible tabs. |
| `showSourceStatus` argument | Existed only to keep ambiguity guidance when compact hid the status row. | Remove; the status row is always visible. |
| `MatchCard`, `HeroSurfaceSizeChanged`, `DashboardTitle`, `StatusPill`, `DashboardQuickTemplate` | Serve only the old cards. | Remove. |
| `PreviewCard` | Shared by every page; other pages keep cards until their phases. | Keep the existing host move; strip the card only while on Home. |

### Phase 3 implementation and validation (2026-10-31)

Status: first implementation complete and uncommitted, awaiting product-owner visual approval. Product version stays `0.7.0`.

- Composition: full-bleed Home with no page heading. Hero with artwork, track metadata, the lyric as the largest text, and a player bar. Sections flow in two independent columns using About's labels, rules and divider: left Chatbox preview and Lyrics details; right Profiles, Lyric context, Quick messages and Lyrics tools.
- Modes: Full, Split (hero beside one section) and Stacked (hero above one section), chosen by `WindowLayout.SelectHomeMode`. Split and Stacked share section tabs. One visual tree: a mode changes grid placement and visibility only.
- Removed: `HomeFit`, `HomeDashboard`, `HomeCompact`, three compact hosts, six Home cards, `MatchCard`, `HeroLyricPanel`, `HeroProviderBadge`, nested detail and quick-message scrollers, `HeroSurfaceSizeChanged`, `DashboardTitle`, `StatusPill`, `DashboardQuickTemplate`, the `showSourceStatus` parameter and the section-reparenting code.
- Retained: all Home controls, names, handlers and accessible names; the Preview move between Home and other pages, because those pages keep their cards until their phases; the `RecoveryCard` and `ContextCard` names, now plain sections.
- New: `HomeMode` policy, Home styles for rule, separator, status, detail rows, header action, choice item and section tab, and `HomeQuickTemplate`. `HomeSectionBox` is now a tab list with the same `SelectedIndex` contract.
- Correctness findings fixed: star-row span inflated the hero; the atmosphere mask ended on a hard edge because blur overflow stretched it; Ctrl+click could deselect the active tab and collapse every section, now guarded and verified; a dangling status divider when source text is empty; `ContextCard` visibility could fight section selection.
- Gates on the final tree: locked restore PASS; Release build 0 warnings/0 errors; focused 144/144; full 549/549 (six new mode-boundary cases, none removed or weakened); package policy PASS; `git diff --check` PASS.
- Native QA used an isolated Release clone with separate data and mutex, Output off, discovery off and a loopback-only destination. Nothing was sent to VRChat. The clone adds a QA-only fixture that writes simulated values into the real controls; that fixture is not in the repository. Sizes: 1448x990, 1920x1032 maximized, 1600x650, 1280x800, 820x650, and boundaries 1139/1141x900, 1299/1301x700 and 1448x759/761. Themes: Rose/Midnight and Blue/Graphite, plus artwork tint off. States: no track (real), simulated playing, long names, missing artwork, unavailable lyric with recovery, context enabled and disabled, expanded details, match panel, and all six sections at minimum. Three resize and navigation cycles left each control exactly once and the selected profile unchanged. Focus inside a section selected its tab on entering Stacked. No horizontal scrolling. About, Manual, Settings and Preview on Display show no regression.
- Gaps: live playback could not be observed, because Apple Music was open with no active media session, so real artwork, lyric timing, transport commands and real ambiguity were not exercised on the new layout. Candidate search results were not populated. Non-100% DPI, screen readers and physical keyboard tab order were not exercised.
- Simplification: production diff +317/-277, about 1.1:1. The XAML grows by 44 lines, mostly the new styles; `WindowLayout.cs` is net zero despite adding the mode policy.

### Phase 3 final visual corrections (2026-10-31)

The owner accepted the overall Home direction and requested two corrections. This supersedes the Split mode and lyric line limits recorded above; the earlier section stays as history. `SPEC.md` holds the current behavior.

- Wide-short: Split left most of 1600x650 empty. Investigation showed the two columns need only 840 DIP of usable width and already have an internal scroller, so Split protected no usability requirement and was removed. Columns now depend on width alone (`UsesHomeColumns`), and height only scales the hero. At 1600x650 all sections are in two columns, with the lower ones reached by inner scrolling. Tabs appear only below 840 DIP.
- Long lyric: the artwork no longer takes part in hero row sizing. It sets a minimum hero height through a negative margin and `HeroGrid.MinHeight`, so ordinary lyrics leave the page still and longer lines use more rows. Line allowance: 5 at 900+ DIP height, 4 at 760+, 3 below, in columns; 4 and 2 when stacked. Measured progress bar position: unchanged for one and two lines at 1448x990 and 1280x800, and through three lines at 1600x650. The 214-character fixture shows in full at 1448x990 in four lines and truncates by ellipsis at 820x650. An unusually long line moves the sections down while it shows; the transport stays visible.
- Correctness review: selection survives width round trips; three resize and navigation cycles left each control once with the profile unchanged; no horizontal scrolling at any size. The deselection guard and focus-to-tab logic are unchanged except for a variable rename, and were not re-exercised because that requires taking window focus while the owner was playing VRChat.
- Simplification: removed `HomeMode`, `SelectHomeMode`, the Split branches, the height condition on columns and the canvas column grid. The mode test became a two-case width boundary test.
- Gates on the final tree: locked restore PASS; Release build 0 warnings/0 errors; focused 140/140; full 545/545 (six mode cases replaced by two); package policy PASS; `git diff --check` PASS.
- Native QA ran in the isolated clone at below-normal priority while the owner played, with Output off, no OSC and no taskbar button. Measured foreground stayed unchanged. The clone window opened at the left monitor edge for about two seconds at each launch before moving off-screen, because Windows repositions it at startup. Off-screen windows are not repainted, so `PrintWindow` frames were stale and were discarded; final captures render the clone's visual tree to a file. Playback in captures is simulated.
- Gaps carried forward: live playback with real artwork, lyric timing and transport; non-100% DPI; screen readers and physical keyboard order.

### Phase 3 closure (2026-10-31)

- CLOSED: the product owner accepted the final Home design after independent screenshot review of the wide-short, long-lyric and compact layouts and of About and Manual.
- Functionality audit against the starting commit: all 20 removed `x:Name`s are layout containers, and every removed name's function moved to a kept control; no accessible name or tooltip was removed; every XAML handler exists in code; the only handler removed is `HeroSurfaceSizeChanged`, which clipped the deleted card.
- Test review: the starting commit has 543 tests and the Phase 3 test diff is additive, so the suite gains 2 cases at 545. The 549 figure counted six uncommitted Split-era mode cases, which covered only the removed policy and were replaced by the `UsesHomeColumns` boundary. No committed test was removed or weakened.
- Correctness and simplification passes found no blocking issue and no further reduction worth a rebuild. `HomeCanvas`, `HomeAtmosphere`, `HomeColumns` and `HomeSectionsPanel` keep unreferenced structural names, matching About's convention.
- Validation evidence is the 545/545 run recorded above; the application binary it loaded matches the final source, and only documentation changed afterward.
- Moved to the v1.0.0 final release gate: live playback with real artwork, lyric timing and transport commands on the new layout; candidate search results; tab deselection and focus-to-tab behavior retest; non-100% DPI; screen readers and physical keyboard order; VR headset view and how other players see output; automatic lyrics with Lyric Context; tray menu with a saved-false profile.

## Phase 4 — Display visual revamp (2026-10-31)

Normative contract: `SPEC.md` § v0.8.0 normative Display contract. Starting HEAD `61dc896d99c8172cf96fdae5b111120b70081204`, clean worktree.

Impact map before editing:

| Existing structure | Finding | Decision |
|---|---|---|
| Five `Card` borders in one `StackPanel`, including named `StatusPanel` and `LyricContextPanel` | Code uses those names only for `Visibility`. | Replace borders with plain sections; handlers untouched. |
| `PresetItem` style | Used only by `PresetBox` and `RotationList`. | Move both to `HomeChoiceItem` and delete the style. |
| `PageTitle` heading and shared `ContentPanel` margin | Display is the only remaining screen in this direction still using them. | Treat Display as full-bleed like Home and About. |
| `PreviewCard` in `PersistentPreviewHost` | Shared with Manual and Settings. | Keep the host; strip the card only on Display using the existing Home branch. |
| `WindowLayout` | No Display-specific layout exists. | One small method moves the supporting column at the existing 840-DIP boundary. |

### Phase 4 implementation and validation (2026-10-31)

Status: first implementation complete and uncommitted, awaiting product-owner visual approval. Product version stays `0.7.0`.

- Composition: full-bleed Display with a `DISPLAY` eyebrow, Profile as a full-width band, then a wider primary column for Composition and Status messages beside a supporting column for Lyric Context and Presentation. Preview stays pinned below the scrolling page through the existing `PersistentPreviewHost`.
- Reflow: the supporting column moves under the primary column below 840 DIP of usable width, reusing the existing boundary. The profile controls and editor actions wrap natively, so no new breakpoint exists. No control is reparented.
- Removed: five `Card` wrappers, the `PageTitle` heading on Display and the `PresetItem` style. `StatusPanel` and `LyricContextPanel` keep their names as plain sections.
- New: `ApplyDisplayLayout`, plus a `continuousPreview` flag so Display reuses Home's preview treatment. The scrollbar column is always reserved so content keeps Preview's right edge and does not reflow when scrolling appears. No handler, binding, state or persistence code changed, and no tests changed.
- Gates on the final tree: locked restore PASS; Release build 0 warnings/0 errors; focused 204/204; full 545/545; package policy PASS; `git diff --check` PASS.
- QA method: a Release clone patched only for data root, mutex and no playback, with `window.Show()` replaced by an in-process driver. No window was shown: 100 ms sampling across a complete run found only invisible WPF helper windows, and the foreground window never changed. Output stayed off, discovery off, no OSC. This is real rendering of the real visual tree and real handlers, not desktop interaction.
- Frozen pages: Home, About, Manual and Settings rendered byte-identical to the committed HEAD at 1448x990 and 820x650, 8 of 8, in Rose/Midnight.
- Display results at all five sizes, in Rose/Midnight and Blue/Graphite: zero clipped controls and no horizontal overflow. Driven through real handlers: Custom editor and conditional panels, token insertion at the caret and over a selection, Decorations insertion and picker content, Lyric Context availability with saved mode kept, rotation add, enable, interval, move, disable, cancel and confirm removal, profile create, rename, duplicate, delete, built-in protection and Home selection sync. `Compact=true` stayed stored through every edit with effective Floating false. Preview moved correctly across eight page changes and regained its card on Manual and Settings.
- Not exercised: real keyboard focus, caret visuals and physical tab traversal (tab order was read from the visual tree), mouse hover and focus states, open dropdowns, tooltips, the modal Decorations dialog and its focus return, Manual ownership text with Output on, live playback and non-100% DPI.
- Simplification: production diff +108/-114, net -6 lines. `ApplyHomeLayout` and `ApplyDisplayLayout` share a few column-placement lines; they stay separate to keep Home frozen.

### Phase 4 native interaction QA (2026-10-31)

Status at the time: all targeted interactions passed on a real window on a hidden Windows desktop. Physical visible QA was blocked because the owner was actively playing and visible interaction needs keyboard focus. The closure below records the owner's decision.

- Method: an isolated Release clone with synthetic data, Output off and discovery off ran with a real HWND on a separate hidden desktop at below-normal priority. No OSC was sent, and the foreground window on the owner's desktop was unchanged. This exercised real WPF activation, keyboard focus, popups and the modal dialog. Input was synthesized inside the app: text through the WPF input pipeline, buttons through automation peers, and Tab and Shift+Tab through WPF focus traversal. No physical mouse or keyboard was used, and no person saw the run.
- Results, 35 of 35 checks passed: profile rename by typing and dropdown selection with Home sync; typing at the caret, token insertion at the caret and focus return; Decorations modal opening, search focus, insertion and focus return to the editor; status message add, edit, save, cancel and confirmed removal; Lyric Context dropdown and disabled explanation; Floating visible, disabled, not focusable and not toggleable, with `Compact=true` stored after unrelated edits; 18 Tab stops with Shift+Tab reversing them exactly; profile switch and return; and at 820x650 Preview visible at both scroll ends with no horizontal scrolling.
- Finding, not a regression: `TokenChips` is an empty Tab stop between the Custom template and the first token. The control is unchanged from the starting commit.
- Audit: the six-file scope holds. No `x:Name`, handler, accessible name or tooltip was removed, Display has no `Card` or `SectionTitle` use, `PresetItem` is gone, and both Floating strings are unchanged.

### Phase 4 closure (2026-10-31)

CLOSED by product-owner decision. The independent reviewer approved the visual design. The owner closed the phase on the evidence below and explicitly deferred physical mouse and keyboard QA to the v0.8.0 release gate, adjusting the earlier closure gate.

Evidence by method; these are different kinds of proof:

| Method | Scope | Result |
|---|---|---|
| Automated tests | Logic and policy, no rendering or interaction | 545/545. No test changed, and the tested binary matches the final build |
| Native isolated WPF automation | Real window, focus, popups and modal on a hidden desktop, with input synthesized inside the app | 35/35 checks passed |
| Visual-tree screenshots | Rendering at five sizes in Rose and Blue, handlers driven in-process, no window shown | Reviewer approved; zero clipped controls; Home, About, Manual and Settings byte-identical to the starting commit, 8 of 8 |
| Physical user testing | Real mouse, keyboard and eyes on the interactive desktop | NOT PERFORMED for Phase 4; deferred below |

Final audit: six-file scope; no production file changed after the 545/545 run; the QA clone's Display sources match the repository; Floating, profile, rotation, composer and persistence code are not in the diff; no `x:Name`, handler, accessible name or tooltip was removed; no test or quality gate was weakened. No further simplification was warranted.

Release gate additions, all physical on the interactive desktop:

- Real mouse and keyboard interaction and focus on Display.
- Decorations modal opening, inserting and returning focus.
- Tab order, including the `TokenChips` empty tab stop.
- Caption-button overlap at 820x650. When scrolled, content passes under the caption buttons and can cover `Rename`. If `Rename` is physically inaccessible, the release is BLOCKED until fixed.
- Non-100% DPI and screen-reader behavior.

## Phase 5A — Native Chatbox Protection (2026-11-01)

Replaces the planned Manual visual revamp. Manual is not removed here and will be evaluated after this feature passes review and physical QA. `SPEC.md` holds the contract.

### PoC evidence (temporary observer outside the repository)

- Desktop, client running in `--no-vr`: 7/7 intentional opens and closes detected, 47 to ~213 ms from synthesized input to observer, across Y, Action Menu, Esc and the X. One close/open pair at 23:15:20–23 stays unexplained. Quick Menu, Action Menu, main menu and the search field produced no Chatbox events. Sending with Enter or the arrow keeps the popup open.
- PCVR through SteamVR and Virtual Desktop, three runs on one client session: 7 pairs against 6 scripted, then 5 pairs against 4 scripted, with the extra pair in a block where the owner reported extra actions but not a Chatbox action. All scripted transitions were present. Search field: no Chatbox event. Idle: 0 events in 4 min 5 s. Action-to-detection latency in PCVR is unmeasured. Log-to-observer upper bounds were 0.09–1.06 s with one-second timestamps.
- Privacy: one synthetic phrase, typed and sent from PCVR, was absent from the session log while a positive control was found. This is limited evidence, not a general guarantee.
- Log format is undocumented. Client build for the PCVR runs was not read.

### Impact map

- **Settings:** two appended `AppSettings` values; a new Settings card only. `IsValid` is unchanged so a bad cooldown cannot reset the file; the cooldown is clamped on use.
- **Output eligibility:** `Tick` computes one blocked value from Pause and the hold and passes it to the unchanged `OutputEligibility.CanSend`; it is the only caller that sends.
- **Scheduler:** new `Rearm()`, which clears `last` and `retry` without forcing, so an empty desired text stays unsent. `ReceiverChanged` is unchanged and is not used on release.
- **Manual:** the hold reuses `PauseOutput`/`ResumeOutput`, which already suppress `desired` and `pendingSend` and freeze the 8 s hold. Send, Clear and Live Edit checks use one blocked test. `ResumeOutput` runs only when neither block remains.
- **Typing:** the routine `typing.Take` is skipped while held. Lifecycle and destination `SendTyping(false)` calls go through a held check. Output Pause keeps its own explicit Clear.
- **Rotation:** eligibility receives the blocked value, so the rotator freezes.
- **Preview, Manual view, OSC text, sidebar, tray:** one extra branch after Paused. The sidebar reuses the Resume button.
- **Diagnostics:** state name only. Fixed-text events, never log content.
- **Lifecycle:** process changes come from the existing 2 s receiver check. No second process query.
- **Runtime persistence:** none. `RuntimeState` is unchanged.
- **Outbound audit:** `output.Send` is called from `Tick` and from Output Pause entry only. `--osc-test` is a separate process mode.

### Implementation plan

1. `NativeChatbox.cs`: strict line matcher, bounded background-safe log reader, and one pure policy.
2. `MainWindow.NativeChatbox.cs`: direct wiring in `Tick`, one poll in flight every 200 ms.
3. Scheduler `Rearm`, eligibility input, Manual and typing guards.
4. Settings card and status text.
5. Focused tests with synthetic logs and a fake clock, then loopback and isolated native QA.

### Phase 5A validation (2026-11-01, uncommitted, awaiting independent review)

- Automated: locked restore, Release build 0/0, focused 46/46, full suite 591/591, package policy PASS, `git diff --check` clean. No existing test changed; no suppressions.
- Loopback and native, simulated: 41/41 checks in an isolated clone with its own data, a synthetic log folder, a synthetic VRChat process id and a private UDP listener, on a hidden desktop. Foreground unchanged. Rotation in the running window was not exercised; it is covered by a unit test and the shared blocked value.
- Decisions: `Finish` never closes, because all 21 PoC closings carried a removal line. The hold reuses Manual's Pause path. `Rearm` was added instead of reusing `ReceiverChanged`.
- Not performed: physical testing and any run against real VRChat or its log. No Desktop or PCVR pass exists for this feature.

### Phase 5A independent finding validation (2026-11-01, uncommitted)

External review findings were treated as hypotheses and validated against the code, SPEC and evidence.

- **R1, continuity loss during the cooldown: rejected as a defect.** SPEC scopes Uncertain to the open state, and the popup was confirmed closed. A transient read failure keeps the offset, so no event is lost. Only external deletion, truncation or replacement loses data, and the same loss in Normal has the same hazard. Holding on that signal would strand output after a harmless read failure. Pinned by tests.
- **R2, status wording: confirmed, alternative fix.** The sidebar and Settings stated an inference as fact, without the experimental caption beside it in the sidebar. The status now reads `Chatbox detected open`.
- **R3, matcher stricter than the PoC: needs evidence.** Not loosened. The strict form matches the one directly inspected real session, for example `2026.10.31 02:38:33 Warning    -  VP Popups_Chat OnPageAddedToStack()`, but the 21 live detections ran through the permissive observer, which recorded neither level nor suffix. Physical QA gate: confirm the status changes to `Chatbox events seen this session` on the first real open. Inspect the matcher first if it does not.
- **R4, same-size replacement: rejected as a defect.** VRChat writes a new timestamped file for each boot. A static replaced file has nothing to read, and the fingerprint catches it on the next growth.
- **R5, tests:** removed two duplicates of `OutputPauseTests` and `RotationTests` and two redundant rows. Kept the rest, each with a distinct failure mode.
- Deferred: missed removal line stays held until Resume now; a stalled read leaves the last label until a session or toggle change; forged log lines; PCVR latency.
- Simplified: private hold enum, unreachable availability branch, repeated blocked values, redundant clamp.

Validation: locked restore, Release build 0/0, focused 44/44, full suite 589/589, package policy PASS. Isolated native and loopback QA 43/43, now including rotation in the running window. No physical test or real VRChat run. The earlier external review bundle predates these edits.

### Phase 5A Gate A, real client parser check (2026-11-01)

PASS for one Desktop cycle. Unmodified production `NativeChatbox.cs` in an isolated build with its own data, on a hidden desktop, reading the real log path from its end. Owner confirmed Desktop mode. Output off, destination 127.0.0.1:19779, discovery off.

- Real lines recognized: Y to `ChatboxOpen` in 231 ms, Esc to `WaitingAfterClose` in 260 ms, `Normal` 15.03 s later. Times use one host clock with 25 ms state sampling, and input was synthesized.
- Resolves R3 for this client on Desktop only. Client build not read. PCVR not run through the production parser.
- Isolation: no `/chatbox/input` reached the private listener. Its only recorded packet was the startup typing clear below. The owner's settings and runtime state hashes are unchanged.
- Separate finding, not fixed: with Output Off, `TypingSignal` sends one `/chatbox/typing` false on the first tick, because `last` starts null. `Reset` reopens the same path after a receiver change. Present at the base commit. It conflicts with the Off no-transmission contract, while disable and shutdown clears are specified. Low severity. Decide separately, after reviewing typing tests.

### Phase 5A Desktop Gate B (2026-11-01)

PASS. Owner decision: Desktop integrated QA decides acceptance for v1.0.0 and PCVR integrated QA is waived. PoC PCVR evidence stays valid in its own scope and does not cover this implementation.

Setup: production code unchanged from Gate A, isolated data, hidden desktop, Desktop client confirmed by the owner and the HUD, invite instance owned by the owner with 1 of 32 people. The application sent to a private relay on 19779, which recorded every packet and forwarded it to 127.0.0.1:9000 only after Output was enabled. Text was the harmless `LC-QA-###`, changed every 2 s. No music playback. Input was synthesized.

| Scenario | Result | Evidence type |
|---|---|---|
| A normal output | Text reached the real bubble | Packet capture, visual |
| B open and type | Held; `pocdraft` kept intact for 7 s with the ticker running | Packet capture, visual |
| C send with Enter | Popup stayed open, hold stayed, sent text stayed visible | Packet capture, visual |
| D close and resume | Text still protected 13 s after close; current text returned | Packet capture, visual |
| E reopen in cooldown | Cooldown cancelled; a new full cooldown followed the next close | Packet capture, state |
| F negative controls | Quick Menu, main menu, search field: no hold, packets continued. Action Menu opening not visually confirmed in this run | Packet capture, state, visual |
| G Off and Pause | Off: no packet, hold end did not enable output. Pause: its one Clear and typing clear, then silence until resumed | Packet capture, state |
| H recovery | Resume now sends current text when eligible, and never overrides Off or Pause. Held Manual draft never sent | Packet capture, state |

Measurements: 9 opens detected in 218 to 366 ms and 9 closes in 126 to 355 ms from synthesized input, with 25 ms state sampling. 8 complete cooldowns of 15.01 to 15.08 s. 105 packets: 102 input, of which 2 empty from explicit Pause, and 3 typing. Minimum input spacing 1.065 s. Across 172.3 s held in 11 windows, no packet fell inside an active hold. An audit by window listed 4, all first resume packets within 31 ms of the sampled window end, an artifact of sampling. In 2 of 9 opens a packet left 204 and 268 ms after the key press and before detection: the known detection window, not a hold violation.

Separate finding, not fixed: the startup `/chatbox/typing` false with Output Off conflicts with the SPEC Off boundary and the master switch rule for typing. Classification: confirmed, low severity, non-blocking. Smallest fix: `TypingSignal.Take` sends false only after true was sent, with its own tests, as a separate change in the v1.0.0 correctness gate.

No client modification, memory access or injection. Screenshots were deleted. Owner settings and runtime state hashes unchanged.

### Phase 5A closure (2026-11-01)

CLOSED / ACCEPTED by product-owner decision for the v1.0.0 development branch, experimental and off by default. This is not a release approval.

Evidence:

- Automated: 589/589 tests, Release build 0/0, package policy PASS.
- Isolated WPF and loopback: 43/43 checks.
- Gate A, real Desktop parser: PASS.
- Gate B, real Desktop OSC integration: PASS, with no automatic packets during established holds and no stale message or empty automatic Clear on resume.
- Cooldown and rapid reopening: PASS. Output Off and Pause precedence: PASS.

Limitations:

- Detection is unofficial and best-effort.
- Packets may escape before an opening is detected.
- PCVR final integration is unverified, by owner waiver for v1.0.0.
- Real music playback remains a release-level QA check.
- Packet evidence came from a private relay in the path, not direct capture on port 9000.

Open: the inherited startup `/chatbox/typing` false with Output Off is confirmed and not fixed here. It must be independently reassessed and, if justified, corrected before the v1.0.0 release. The Output Off contract stays as written.

Earlier release gates stay in force, including the physical checks from Phases 3 and 4 and the `Rename` blocker. Manual remains until Phase 5B.

### Typing signal correction (2026-11-01)

Classification: confirmed, alternative fix, low severity. Output Off sent `/chatbox/typing` false at startup, on destination changes, on power events and at shutdown, against the Off boundary and the master switch rule in SPEC. SPEC is unchanged: disabling and shutdown clears are required only when typing was active.

- `TypingSignal` keeps its state across `Reset`, which now only makes active typing resend at once. Inactive typing stays silent.
- `ClearTyping` goes through the signal and sends only after a true.
- Suppressing false while `Reset` still cleared the state was rejected: a reset during active typing would lose the pending clear.

Loopback, same script on base and fix, on a hidden desktop with private ports and no VRChat. Output Off: base 6 typing packets, fix 0. Active typing keeps the same clears on focus loss, Output Off, Pause, destination change, hold release and shutdown, with no packet during the hold and true resent to a new destination. Validation: locked restore, Release build, full suite, package policy and diff check.

## Phase 5B — Manual removal (2026-11-01)

`SPEC.md` holds the contract. This is a removal, not a revamp. Manual lyrics matching is a different feature and stays.

### Impact map

| Area | Removed | Kept, and why |
|---|---|---|
| Navigation and UI | Manual nav item, `ManualPage`, Home Quick Messages section, Home `Messages` tab, two quick templates | `InspectorManual` and `Manual match` label, `Manual destination` status: lyrics and OSC, not messaging |
| Output state | `ManualChat`, draft, ownership, pending send, 8 s hold, focus handlers | `IsOutputBlocked`, used by `Tick` and the sidebar |
| `Tick` | Manual desired, alignment and analysis branches, `ApplyManualView`, typing send | Automatic composition, `outputBlocked`, scheduler |
| Scheduler | `forceSend` and `force`: only Manual Send and Clear used them | `ReceiverChanged`: process, destination, Pause and power callers. `Rearm`: hold release. `retry`: failed sends. Rate limit |
| Typing | `TypingSignal`, `SendTyping`, `TypingPacket`, `ClearTyping`, Pause typing clear: `manual.Typing` was the only source of true | None. A false with no true would only risk touching the native indicator |
| Output Pause | `OutputPauseEntryEffects`, reduced to one text Clear decision | Pause semantics and persistence |
| Rotation and sidebar | `automaticAvailable` input: it reflected only Manual ownership | Eligibility from message use, Output and blocked state |
| Decorations | `Manual` target | Custom and rotation targets |
| Persistence | `TypingIndicator`, `LiveEdit`, `ManualAlignment`, Quick Message models and `LocalData` access | Unknown JSON values are ignored by the serializer, so old files load |
| Runtime state | `Manual` in `Sections` | Existing fallback opens Home and keeps window and Pause state |
| Diagnostics | `manualMode` and `Mode` | Everything else |
| Tests | Contracts of the removed features | Every surviving invariant, rewritten to automatic output where needed |

### Order

UI and handlers, direct rewiring of `Tick`, dead state and types, persistence, scheduler and typing, then tests and documentation.

### Phase 5B validation (2026-11-01, uncommitted, awaiting review)

- Automated: locked restore, Release build 0/0, focused 205/205, full suite 581/581, package policy PASS, `git diff --check` clean. The suite went from 591 to 581 only through retired contracts: Manual, Live Edit, typing and Quick Message tests, one About row and one Rotation row. Receiver absence, rotation eligibility and resume tests were rewritten on automatic output.
- Isolated native and loopback, simulated: 65/65 checks on a hidden desktop with seeded legacy data, a synthetic log and a private listener. Four navigation sections; `Manual` runtime state opens Home; 32 views across four sizes and Rose and Blue without horizontal overflow; 15 packets, all `/chatbox/input`, none for typing including shutdown; pause sends one text Clear; hold, cooldown, reopen, empty resume and rotation unchanged; `quick-messages.json` keeps identical bytes and time; retired settings keys are dropped and other values kept.
- Not performed: physical testing, real VRChat, and real music playback. Native Chatbox Protection detection code is unchanged, so its Desktop gates are not repeated, but this build has not run against the real client.

### Phase 5B closure (2026-11-01)

CLOSED / ACCEPTED by product-owner decision after independent source and visual review of the 36-file diff. This is not a release approval.

- Manual Chatbox messaging and Quick Messages are removed. Navigation has four pages.
- Production net reduction: 335 lines.
- 581/581 automated tests and 65/65 isolated native checks.
- `quick-messages.json` user data is preserved, unread and unmodified. Manual lyrics matching is preserved.
- Independent code review PASS. Visual screenshot review accepted.
- Physical QA remains a release gate: real input and focus, `Rename` at 820x650, non-100% DPI, screen reader, real music playback, Native Chatbox Protection on this build, tray, startup and updater.

Next: Phase 6, Settings visual consolidation, on explicit approval.

## Phase 6 — Settings visual consolidation

### Approach

- `SPEC.md` holds the contract. Settings joins Display as a full-bleed workspace: eyebrow, tracked labels, rules, center divider and pinned continuous Preview. Card and page-heading wiring existed only for Settings, so it is gone.
- Arrangement verified on native renders: first column Playback source, Lyrics sources, VRChat OSC; second column Appearance, Application behavior, Native Chatbox Protection. Accent and Background share a row so the columns balance. Column order is also the single-column reading and Tab order, which needs no reparenting.

### Findings and classification

| Finding | Evidence | Classification |
|---|---|---|
| Scrolled controls entered the 36-DIP caption area, under the window buttons and where a click drags the window | Native scrolled renders, `WindowChrome.CaptionHeight`, SPEC shell rule | CONFIRMED — FIX, Settings only: page scrolls below the area, position at rest unchanged |
| Slider focus was only 75% opacity on a 14-DIP dot | Native focus render | CONFIRMED — FIX in the theme: white thumb ring on keyboard focus. The unfocused state is pixel-identical |
| Display and About also scroll under the caption area | Code reading | DEFERRED — NON-BLOCKING: frozen pages, already in the caption and `Rename` release gate |
| ComboBox focus changes text color only | Native focus render | REJECTED — NOT A DEFECT: visible, shared by frozen pages |
| The stated arrangement could harm reading order | Tab walk, automation tree, renders at each size | REJECTED — NOT A DEFECT: arrangement kept |

### Phase 6 validation (2026-11-01, uncommitted, awaiting review)

- Automated: locked restore, Release build 0/0, focused 279/279, full suite 581/581 with no test changes, package policy PASS, `git diff --check` clean. Layout is visual, so no test was added.
- Isolated native, simulated: 140/140 checks across three runs on a hidden desktop with synthetic data and a private listener. The registry startup store, mDNS browse, lyrics lookups and player switching were stubbed. The live handlers ran for source selection, accent, hex, color picker Cancel and OK, background, tint, startup success and failure, tray options, Import, Retry, Forget, OSCQuery, Advanced destination, Apply and errors, and every protection state. Also checked: 21-step Tab order, reachability at seven sizes plus 820x500, no horizontal extent, one Preview, focus and draft kept across 840 DIP, and direct startup on Settings.
- Frozen pages: 60/60 native renders of Home, Display and About pixel-identical to `c3e780d`, across five sizes, Rose and Blue, scrolled ends and Home tabs. Focused slider states are excluded from that comparison and changed on purpose.
- Copy: all 103 tracked attributes of the Settings block survive, apart from the six group titles, which keep their plain text as accessible names.
- Not performed: physical input, real DPI, a screen reader, real VRChat or real music playback. Scaling was simulated by DIP size and render scale only.

### Phase 6 closure (2026-11-01)

CLOSED / ACCEPTED by product-owner decision after independent source and visual review of the seven-file diff. This is not a release approval.

- The Settings continuous visual redesign is accepted. All four pages use the approved visual system with one shared continuous Preview.
- 581/581 automated tests, 140/140 isolated WPF checks and 60/60 frozen-page renders identical.
- Independent source and visual review PASS, with no production defects identified.
- Physical QA remains a release gate: real input, focus and popups, `Rename` at 820x650, non-100% DPI, screen reader, real music playback, Native Chatbox Protection on this build, the real color picker and LRC import, tray, startup and updater.

Next: v1.0.0 release readiness, on explicit approval.

## Instrumento visual revamp

Approved by the owner on 2026-11-02 with a control baseline taken at `38e24da`. `SPEC.md` holds the contract. Stages, one local commit each: 0 contract, a tokens, b shell, c Home, then on separate approval d Display, e Settings, f About, g Logo, h Decorations.

Phase 6 stays recorded as closed: its evidence belongs to that build. The revamp replaces its visual result, so release QA must run on the final revamp.

- Authorized now: stages 0, a, b and c, then stop for owner review of real WPF renders.

### Stages 0, a, b and c (2026-11-02, local commits, awaiting owner visual review)

- Automated: Release build 0/0, 581/581 tests with no test changes, at each stage.
- Isolated native, simulated: 115/115 checks on a hidden desktop with synthetic data and covers. Flows covered rail, logo, Output on and off, Pause menu, Resume, Held, scheduled hint, error row, profiles, Lyric Context, Manage, Recording & timing, Search alternatives open and close, Import cancel, Ignore, recovery, section tabs and focus-to-tab. Search itself, real transport, real volume and real artwork delivery were not run.
- Text over artwork: lowest measured contrast 4.99:1 with a light cover, in 16 renders across two sizes, Rose and Blue, tint on and off, light and dark covers.
- Baseline at `38e24da`: all event handlers are kept and `OpenAbout` is added. Removed names: the approved waveform pair, `HomeColumnDivider`, `HomeLeftColumn`, `HomeRightColumn` and an internal style `Marker`. Five headings dropped a redundant accessible name because their text now reads the same.
- SkiaSharp: no code consumer remains after the waveform. The package, `Publish.ps1`, notices and license still reference it, so removal stays a separate cleanup.
- Display, Settings and About are transitional until their stages.

### Shell and Home refinement (2026-11-02)

Owner-approved after review of real renders. Inspector scale, atmosphere masks reusing the existing artwork tint, compact lyric proportions and Output state colors. Output Off stays neutral with a scheduled pause, and the hint keeps the pause information.

- 581/581 tests and 120/120 isolated native checks. Lowest text contrast over a light cover: 5.13:1.
- Blur at 64 DIP: software render about 405 ms at 1448x990 against 49 ms without it, once per artwork with caching. Live CPU stayed at or below 0.4%, measured on a hidden desktop, so nothing was optimized. Not measured on a visible GPU window.
- The five retired heading names read the same through their text in the automation tree, so they were not restored.

### Stage d, Display (2026-11-02, local commit, awaiting owner visual review)

Three columns with the editor and Preview together, one tree, Preview moved only between its hosts. Below 1020 DIP after the rail the columns stack with Preview pinned.

- Stacked column scrollers pass the wheel to the page through `ColumnScrollViewer`.
- `Rename` is clear of the caption band at seven sizes: `WM_NCHITTEST` returns client and the hit test reaches the button, also when scrolled. A physical click is still outstanding.
- `TokenChips` is no longer an empty Tab stop, and profile rows expose their name instead of the record text.
- Editor line numbers were skipped: they would need scroll and wrap synchronization for no functional gain.

### Display adjustment and stage e, Settings (2026-11-02)

Display is accepted by the owner: Preview fits its content up to nine Chatbox lines, and it stays pinned when stacked.

Settings is committed locally and awaits owner visual review. It has an index with real state summaries, 240-DIP control rows, and the shared Preview pinned below the content.

- The index stays at every width and narrows to 180 DIP below 1000 DIP after the rail.
- A startup crash from `SelectedIndex` firing during XAML load was found by native QA and guarded with `ready`.
- The new summary copy is limited to `All off` and `N on`; the other summaries reuse existing state text.
- `UsesHomeColumns` no longer has a production caller and stays only because a test covers it. Review it during cleanup.

### Stage e accepted, stage f About (2026-11-02)

The owner accepted Settings. Physical QA keeps one observation: legibility of the 180-DIP index at 820x650 with DPI and text scaling. Do not change that layout without a demonstrated problem.

About is committed locally and awaits owner visual review: editorial identity column with logo and wordmark, Overview, Community & contact and Project & tools rows, and a 400-DIP panel for Updates and Privacy & data.

- Automated: 578/578. Three tests left with the code they covered: Updates reflow modes and About section reparenting, both retired by this layout.
- Isolated native: 382/382 across all pages, with links, folders, installer launch and clipboard intercepted in the QA copy. Update check, download and installer states were injected with synthetic text, and no network or installer ran. `MainWindow.Updates.cs` is unchanged.
- Approved pages: 68 of 76 renders pixel-identical. The other 8 differ only in the clock digit of the Status / Time preview.
- Existing artwork files stay bundled, as `AboutArtworkAndBrandMarksAreBundledAsWpfResources` requires, even though About no longer shows them.

### Stage f accepted, final phase Logo and Decorations (2026-11-02)

The owner accepted About, so all four main pages are accepted. Logo and Decorations ran as one phase, committed locally and awaiting owner visual review.

- Logo: master SVG, WPF-rendered PNG frames, regenerated ICO, vector `AppLogo` in the rail and About. `Convert-Icon.ps1` gained `-FrameDirectory`, and `Render-AppIcon.ps1` is new. `AppIcon.png` is no longer an application resource because nothing loads it.
- Decorations: no cards, client-drawn frame with its own Close, same handlers, plus `ClosePicker`. Removed the unused `SectionTitle` style.
- SkiaSharp cleanup stays out of this phase, for the final reductive closure.

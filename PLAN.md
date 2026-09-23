# v0.8.0 execution plan — Phase 0 ready for review

## State and baseline

- Planning date: 2026-09-22. Branch `codex/v0.8.0` was created from exact released `main` commit `b663555fa604ec86062a6348876f1c27fc31a041`. `git fetch --prune` completed; local `main`, `origin/main` and branch base matched; initial worktree was clean and the `v0.7.0` tag existed.
- Current work is documentation and reference preservation only. Product project version remains `0.7.0`; no UI implementation, behavior change, package or release work has begun.
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

## Next action

Review Phase 0's `SPEC.md` visual contract and stored reference with the product owner before implementing Phase 1.

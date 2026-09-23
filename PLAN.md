# v0.8.0 execution plan — Phase 1.1 implemented, owner review pending

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

## Next action (Phase 1.2 superseded by 1.3 below)

Product-owner physical review of Phase 1.2 shell micro-polish; do not start Phase 2 until accepted.

## Phase 1.3 Output wave motif redesign (2026-09-23, branch `codex/v0.8.0`)

Starting HEAD `122f78a83e657ed84e25fa3ea57fd1bc1ac37e29`, worktree clean, `main` at `b663555fa604ec86062a6348876f1c27fc31a041`. Shell approved except the motif: owner rejects bars/capsules/equalizer look, wants a continuous ribbon/wave per the canonical sidebar motif (no second wave image was attached — derived from `docs/visual/v0.8.0-about-reference.png`). Visual-only iteration; no behavior change.

Implementation choice: 3 overlaid vector `Path`s (main 1.75px full `AccentBrush`; two phase-shifted compressed echoes at 0.45/0.28) forming one tapered wave packet (~128 DIP wide, 58px tall, round joins/caps), centered in the `OutputVisualizer` cell (container changed `StackPanel`→`Grid`, one-line `WindowLayout` lookup update). Animation is lateral counter-drift on main/upper echo (2.6s/3.1s) plus opacity breathing on the lower echo (3.6s) — same `OutputWaveformStory` key and gating (`IsSending` + `ClientAreaAnimation`), so code-behind is untouched. No timers/services/dependencies; green dot stays the only semantic green.

Files changed (production): `MainWindow.xaml` (storyboard + motif), `WindowLayout.cs` (container type). Tests: none — no logic touched. Ponytail notes: bars deleted, no new abstractions/state/semantics; geometry points computed once via script, pasted as static data.

Test evidence: focused 75/75 green before edits; after: locked restore, Release build 0/0, full suite 515/515, package-policy PASS, `git diff --check` clean.

Native QA evidence (Release exe, captures in local temp `p13-*.png`, not committed): owner’s Apple Music session found genuinely PLAYING (untouched — no transport commands sent). Sending state shows green `OSC Output Active` + `Sending lyrics...`; wave renders as one continuous layered form (closeups confirm); pixel-diff of the wave region across 1.3s proves live motion (291/3750 sampled pixels changed, sidebar-only crop); Off shows dim static wave; Paused at 820×650 shows summary + aligned Resume/Change. No VRChat running; loopback-only destination.

QA side-effects: app LEFT OPEN and playing-adjacent (owner session active — closing would kill live OSC output): output restored ON + unpaused, Home, 1448×990. No pause persisted; no settings flipped.

## Next action (Phase 1.3 superseded by 1.4 below)

Product-owner physical review of Phase 1.3 wave redesign; do not start Phase 2 until accepted.

## Phase 1.4 wave motion correction (2026-09-23, branch `codex/v0.8.0`)

Starting HEAD `c13f8e836137e507b43b0e94b0e535a22eedd310`, worktree clean, `main` at `b663555fa604ec86062a6348876f1c27fc31a041`. Owner verdict on 1.3: vector paths approved, but lateral `TranslateTransform.X` drift reads as a rigid sticker sliding — the wave must deform its shape, not translate. This phase changes motion only.

Implementation: removed all translation; the same 3 paths now morph about their center (`RenderTransformOrigin 0.5,0.5`): main gets skew shear (±8°, 2.8s) plus amplitude breath (ScaleY 1→1.1, 3.7s), upper echo breathes at a different period (1→1.16, 4.3s), lower echo keeps opacity breathing (0.3→0.6, 3.6s). Ends stay anchored while crests sweep and swell — centroid fixed, silhouette evolves. Transforms are named Freezables targeted directly (no indexed paths). Same storyboard key/gate, so code-behind untouched; static base values unchanged, `Stop()` restores them.

Files changed (production): `MainWindow.xaml` only (storyboard + path transforms). Tests: none — no logic touched. Ponytail notes: 4 small animations replace 3; no new elements, abstractions, state, or semantics.

Test evidence: focused 75/75 green before edits; after: locked restore, Release build 0/0, full suite 515/515, package-policy PASS, `git diff --check` clean.

Native QA evidence (Release exe, captures in local temp `p14-*.png`, not committed): owner's Apple Music session genuinely PLAYING (no transport commands sent, nothing paused). Sending hierarchy correct; three wave-region frames 1.4s apart show visibly different crest/valley configurations with anchored ends (`p14-deform-strip.png`); wave-region pixel-diff 267/4250 per interval confirms live motion; Off shows dim static wave; Paused at 820×650 correct. No VRChat running; loopback-only destination. QA answer: true shape deformation, not rigid slide.

QA side-effects: app LEFT OPEN on the live owner session (closing would kill mid-song OSC output): pause resumed, output restored Off (launch-found state), Home, 1280×940. Music and Apple Music untouched.

## Next action

Product-owner physical review of Phase 1.4 wave motion; do not start Phase 2 until accepted.

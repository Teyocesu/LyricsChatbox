# Application icon

The application icon is the mark "Nota que habla": a rose musical note whose head forms a chat bubble, as one continuous silhouette on a dark rounded tile. It was specified in the owner-approved Instrumento brief.

`Brand/AppIcon.svg` is the master artwork, on a 48 x 48 grid. To regenerate the icon set from it:

```powershell
powershell -NoProfile -STA -File scripts/Render-AppIcon.ps1
powershell -NoProfile -File scripts/Convert-Icon.ps1 -FrameDirectory src/LyricsChatbox/Assets/Brand/Png
```

`Render-AppIcon.ps1` renders `Brand/Png/AppIcon-<size>.png` at 16, 24, 32, 48, 64, 128 and 256 px with WPF, omitting the inner line at 24 px and below so the mark stays legible. `Convert-Icon.ps1` packs those frames into `AppIcon.ico` and writes the 256 px frame to `AppIcon.png`.

The executable, window, tray, installer and installed shortcuts use `AppIcon.ico`. The rail and About use the same design as the `AppLogo` vector resource in `Themes/Dark.xaml`, which must stay in step with the SVG.

`About/` holds earlier About artwork. It stays bundled, although About no longer displays it.

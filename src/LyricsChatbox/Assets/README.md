# Application icon

User-supplied artwork selected on 2026-09-09: a rose chat bubble and musical note on a dark rounded tile. The source was supplied as `ChatGPT Image 9 sept 2026, 21_06_57.png`. The user clarified that only the outer rounded-square tile should form the icon, excluding the surrounding background. The built-in image editing tool extracted the tile onto transparent corners before ICO conversion.

`AppIcon.png` is the extracted tile resized to 256 × 256, preserving alpha. `AppIcon.ico` contains 16, 24, 32, 48, 64, 128 and 256px PNG frames, converted with `scripts/Convert-Icon.ps1` using Windows System.Drawing. The executable, window, sidebar, tray, installer and installed shortcuts use these resources.

Edit prompt: Extract only the existing outermost pink-edged dark rounded-square tile and its interior; tightly frame it, make everything outside the rounded boundary transparent, preserve the interior shapes, colors, lighting and proportions; no new design, text, or checkerboard pixels.

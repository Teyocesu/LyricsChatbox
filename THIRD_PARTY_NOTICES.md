# Third-party components

The Windows x64 self-contained distribution includes .NET and Windows interop components. This document does not assign a license to LyricsChatbox's original code.

| Component | Version in this build | Terms / source |
|---|---|---|
| .NET runtime | 10.0.11 | MIT and bundled third-party notices; [dotnet/runtime](https://github.com/dotnet/runtime) |
| Windows Desktop / WPF | 10.0.11 | MIT; [dotnet/wpf](https://github.com/dotnet/wpf) |
| Microsoft.Windows.SDK.NET.Ref projection | 10.0.19041.57 | [Windows SDK terms](https://go.microsoft.com/fwlink/?LinkId=837577) and [redistributable list](https://learn.microsoft.com/en-us/legal/windows-sdk/redist#microsoftwindowssdknetref) |
| C#/WinRT runtime | supplied by the Windows SDK projection package | MIT; [microsoft/CsWinRT license](https://github.com/microsoft/CsWinRT/blob/master/LICENSE) |
| Zeroconf | 3.7.16 | MIT; © Claire Novotny 2016–2024; [upstream](https://github.com/novotnyllc/Zeroconf/tree/bd4bd55e044a9aa4b8656741214c989a5ee50ded) |
| System.Reactive (Zeroconf dependency) | 5.0.0 | MIT; .NET Foundation and Contributors; [upstream license](https://github.com/dotnet/reactive/blob/103c252a0ec94eac753f353131ad95cc0be1b390/LICENSE) |
| SkiaSharp.Views.WPF, SkiaSharp.Views.Desktop.Common, SkiaSharp, SkiaSharp.NativeAssets.Win32 | 4.151.2 | MIT; [SkiaSharp source](https://github.com/mono/SkiaSharp/tree/release/4.151.2); native-component notices are bundled in `licenses/SKIASHARP-THIRD-PARTY-NOTICES.txt` |
| OpenTK and OpenTK.* modules | 4.3.0 | MIT; [OpenTK source](https://github.com/opentk/opentk/tree/4.3.0) |
| OpenTK.GLWpfControl | 4.2.3 | MIT; [upstream license](https://github.com/opentk/GLWpfControl/blob/master/LICENSE.md) |
| OpenTK.redist.glfw | 3.3.0-pre20200830200122 | GLFW zlib license; bundled as `licenses/GLFW-LICENSE.txt`; [GLFW](https://www.glfw.org/) |

The distribution's `licenses` folder contains the .NET license and third-party notices, WPF license, C#/WinRT license and Windows SDK license. The SDK redistributable list explicitly includes Microsoft.Windows.SDK.NET.dll and WinRT.Runtime.dll. These binaries are distributed unmodified for Windows interop.

Zeroconf and System.Reactive license texts are also bundled. Zeroconf is used only for OSCQuery service discovery; LyricsChatbox does not advertise an inbound service or use an additional UI framework.

SkiaSharp is used for the decorative Output ribbon through its CPU-backed WPF `SKElement`. The WPF package also brings OpenTK and `OpenTK.GLWpfControl` transitively; LyricsChatbox does not create an OpenGL control or context. Their required license texts, GLFW's license, SkiaSharp's MIT text, and SkiaSharp's upstream native third-party notices are bundled under `licenses/`.

Development/test-only dependencies are not included in the runnable distribution: xUnit 2.9.3, xUnit analyzers 1.18.0 and Visual Studio runner 3.1.4 (Apache-2.0); Microsoft.NET.Test.Sdk, TestHost, ObjectModel and CodeCoverage 17.14.1 (MIT); Newtonsoft.Json 13.0.3 (MIT). Package license metadata was checked in the restored NuGet packages.

LRCLIB and NetEase are external HTTPS services, not bundled implementation code. NetEase uses an independently written community REST adapter; this does not assert an official developer agreement, availability guarantee or lyric redistribution license. Current full service terms could not be verified during RC evaluation. Cached/imported lyrics remain in the user's local application-data directory; no provider lyric corpus is part of this repository or distribution. No MagicChatBox code was copied.

The optional Windows installer is built with Inno Setup, copyright Jordan Russell and Martijn Laan. The unmodified installer engine retains its upstream notices. Inno Setup is a build tool, not an application runtime or update service. Upstream license: https://github.com/jrsoftware/issrc/blob/is-7_1_0/license.txt . Windows Forms NotifyIcon uses the existing Microsoft Windows desktop runtime; no third-party tray/UI package was added.

## Brand assets

The About page includes the white outline VRChat logo from the [official VRChat Press Kit](https://hello.vrchat.com/press) and the Discord Clyde symbol from [Discord's official brand assets](https://discord.com/branding). The bundled Discord PNG is a transparent rasterization of Discord's [official Symbol SVG](https://cdn.prod.website-files.com/6257adef93867e50d84d30e2/66e3d80db9971f10a9757c99_Symbol.svg); it preserves the source aspect ratio and official Blurple (`#5865F2`) color without redrawing or recoloring the mark. These marks identify the linked services; their owners retain all rights. Their use does not imply sponsorship, affiliation or endorsement, and they are not open-source components of LyricsChatbox.

The About OSC row uses a bundled raster of the Microsoft Segoe Fluent Icons `Network` glyph (U+E968), documented in [Microsoft's Segoe Fluent Icons reference](https://learn.microsoft.com/en-us/windows/apps/design/iconography/segoe-fluent-icons-font). Only the resulting PNG is bundled, so the installed font is not a runtime dependency. The hero note and atmospheric backdrop are original generated artwork for LyricsChatbox.

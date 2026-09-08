# Third-party components

The Windows x64 self-contained distribution includes .NET and Windows interop components. This document does not assign a license to LyricsChatbox's original code.

| Component | Version in this build | Terms / source |
|---|---|---|
| .NET runtime | 10.0.11 | MIT and bundled third-party notices; [dotnet/runtime](https://github.com/dotnet/runtime) |
| Windows Desktop / WPF | 10.0.11 | MIT; [dotnet/wpf](https://github.com/dotnet/wpf) |
| Microsoft.Windows.SDK.NET.Ref projection | 10.0.19041.57 | [Windows SDK terms](https://go.microsoft.com/fwlink/?LinkId=837577) and [redistributable list](https://learn.microsoft.com/en-us/legal/windows-sdk/redist#microsoftwindowssdknetref) |
| C#/WinRT runtime | supplied by the Windows SDK projection package | MIT; [microsoft/CsWinRT license](https://github.com/microsoft/CsWinRT/blob/master/LICENSE) |

The distribution's `licenses` folder contains the .NET license and third-party notices, WPF license, C#/WinRT license and Windows SDK license. The SDK redistributable list explicitly includes Microsoft.Windows.SDK.NET.dll and WinRT.Runtime.dll. These binaries are distributed unmodified for Windows interop.

Development/test-only dependencies are not included in the runnable distribution: xUnit 2.9.3, xUnit analyzers 1.18.0 and Visual Studio runner 3.1.4 (Apache-2.0); Microsoft.NET.Test.Sdk, TestHost, ObjectModel and CodeCoverage 17.14.1 (MIT); Newtonsoft.Json 13.0.3 (MIT). Package license metadata was checked in the restored NuGet packages.

LRCLIB and NetEase are external HTTPS services, not bundled implementation code. NetEase uses an independently written community REST adapter; this does not assert an official developer agreement, availability guarantee or lyric redistribution license. Current full service terms could not be verified during RC evaluation. Cached/imported lyrics remain in the user's local application-data directory; no provider lyric corpus is part of this repository or distribution. No MagicChatBox code was copied.

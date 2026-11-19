# Libraries inside Hush.exe

`build.bat` packs these files into the exe as resources, unchanged.

| File | Version | License | Source |
|------|---------|---------|--------|
| Microsoft.Web.WebView2.Core.dll, Microsoft.Web.WebView2.WinForms.dll | 1.0.4314.35 | BSD ([WebView2-LICENSE.txt](WebView2-LICENSE.txt)) | https://www.nuget.org/packages/Microsoft.Web.WebView2 |
| WebView2Loader.dll (native, x64) | 1.0.4314.35 | BSD ([WebView2-LICENSE.txt](WebView2-LICENSE.txt)) | same package, `runtimes/win-x64/native`. Written to `%LOCALAPPDATA%\Hush\` at the first start, because Windows can't load a native DLL from memory |

The window's fonts sit in `ui/fonts/`: Geist and Geist Mono (SIL OFL 1.1, [Geist-LICENSE.txt](../ui/fonts/Geist-LICENSE.txt)) and Instrument Serif italic (SIL OFL 1.1, [InstrumentSerif-LICENSE.txt](../ui/fonts/InstrumentSerif-LICENSE.txt)), the latin subsets from Fontsource 5.3.0, the same ones as davide-mariotti.github.io. The icons in `ui/icons.js` are Lucide 1.74.0 (ISC).

To update WebView2: download the package from nuget.org, take the two DLLs from `lib/net462` and the loader from `runtimes/win-x64/native`, replace them here and run `build.bat`.

@echo off
rem Builds Hush.exe with the C# compiler that ships with Windows (.NET Framework 4). Nothing to install.
rem The libraries in lib\ and the window's page in ui\ go inside the exe as resources: one file to download.
cd /d "%~dp0"
setlocal enabledelayedexpansion
set LIBS=
for %%f in (lib\*.dll) do set LIBS=!LIBS! /resource:%%f,%%~nxf
for %%f in (ui\*.html ui\*.css ui\*.js) do set LIBS=!LIBS! /resource:%%f,ui/%%~nxf
for %%f in (ui\fonts\*.woff2) do set LIBS=!LIBS! /resource:%%f,ui/fonts/%%~nxf
echo Compiling Hush...
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /optimize+ /platform:x64 /target:winexe /out:Hush.exe ^
  /win32icon:icon\hush.ico /win32manifest:hush.manifest /resource:icon\hush.ico,Hush.ico ^
  /resource:icon\tray-off.ico,TrayOff.ico /resource:icon\tray-live.ico,TrayLive.ico ^
  /reference:lib\Microsoft.Web.WebView2.Core.dll /reference:lib\Microsoft.Web.WebView2.WinForms.dll ^
  /reference:System.Web.Extensions.dll %LIBS% ^
  Hush.cs MainForm.cs AssemblyInfo.cs
if %errorlevel% neq 0 (
    echo Compilation failed.
    pause
    exit /b %errorlevel%
)
"%~dp0Hush.exe" --selftest
if %errorlevel% neq 0 (
    echo Self-test failed.
    pause
    exit /b %errorlevel%
)
echo Packing Hush-portable.zip (the same exe plus Hush.ini)...
powershell -NoProfile -Command "$d = Join-Path $env:TEMP 'hush-portable'; Remove-Item $d -Recurse -Force -ErrorAction SilentlyContinue; New-Item -ItemType Directory $d | Out-Null; Copy-Item Hush.exe $d; Set-Content (Join-Path $d 'Hush.ini') '; Hush settings. While this file sits next to Hush.exe, nothing goes to the registry.'; Compress-Archive (Join-Path $d '*') Hush-portable.zip -Force; Remove-Item $d -Recurse -Force"
echo Done: Hush.exe and Hush-portable.zip

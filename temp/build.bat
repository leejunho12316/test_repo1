@echo off
rem Build with the C# compiler bundled in Windows (.NET Framework 4.8). No install needed.
setlocal
set "ROOT=%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

"%CSC%" /nologo /codepage:65001 /target:winexe /optimize+ /out:"%ROOT%KeyboardGuitar.exe" ^
  /resource:"%ROOT%patterns.md",KeyboardGuitar.patterns.md ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll ^
  "%ROOT%src\Core\*.cs" "%ROOT%src\App\*.cs"
if errorlevel 1 exit /b 1

"%CSC%" /nologo /codepage:65001 /target:exe /out:"%ROOT%tests\tests.exe" /r:System.Core.dll ^
  "%ROOT%src\Core\*.cs" "%ROOT%tests\*.cs"
if errorlevel 1 exit /b 1

"%ROOT%tests\tests.exe" "%ROOT%patterns.md"

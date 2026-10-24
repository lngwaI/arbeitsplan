@echo off
rem Release-Build: ohne Testschalter (kein --user/--shot/--selftest), ohne Debug-Symbole, eine exe -> release\Arbeitsplan.exe
rem Gleicher Compiler und gleiche API-Grenzen wie build.cmd (x86, .NET 4.0).
set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist release mkdir release
"%CSC%" /nologo /target:winexe /platform:x86 /codepage:65001 /optimize+ /debug- /warn:4 /win32manifest:src\app.manifest /out:release\Arbeitsplan.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll src\Core\*.cs src\Win\*.cs
if errorlevel 1 (echo BUILD FEHLER) else (echo OK release\Arbeitsplan.exe)

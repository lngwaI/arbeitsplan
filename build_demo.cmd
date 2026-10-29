@echo off
rem Baut die Vorfuehr-Version bin_demo\ArbeitsplanDemo.exe (Define DEMO, siehe docs/Demo.md). Nur fuer Vorfuehrungen:
rem nie in make_dist.cmd / den Release-Workflow aufnehmen und nie an Mitarbeiter verteilen.
set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist bin_demo mkdir bin_demo
copy /y config\statuscolors.ini bin_demo\ >nul
"%CSC%" /nologo /target:winexe /platform:x86 /codepage:65001 /optimize+ "/define:TESTHOOKS;DEMO" /win32icon:assets\arbeitsplan.ico /out:bin_demo\ArbeitsplanDemo.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.DirectoryServices.AccountManagement.dll /r:System.Security.dll src\Core\*.cs src\Win\*.cs
if errorlevel 1 (echo BUILD FEHLER) else (echo OK bin_demo\ArbeitsplanDemo.exe)

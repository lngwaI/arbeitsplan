@echo off
rem Baut bin\Arbeitsplan.exe mit dem in Windows enthaltenen .NET-Framework-Compiler (kein SDK noetig).
rem x86 + nur .NET-4.0-APIs: laeuft auf 32/64-Bit-Windows; ab XP SP3 mit installiertem .NET 4.0.
set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist bin mkdir bin
copy /y config\statuscolors.ini bin\ >nul
"%CSC%" /nologo /target:winexe /platform:x86 /codepage:65001 /optimize+ /define:TESTHOOKS /out:bin\Arbeitsplan.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll src\Core\*.cs src\Win\*.cs
if errorlevel 1 (echo BUILD FEHLER) else (echo OK bin\Arbeitsplan.exe)

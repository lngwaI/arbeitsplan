@echo off
rem Baut Arbeitsplan.exe mit dem in Windows enthaltenen .NET-Framework-Compiler (kein SDK, keine Installation noetig).
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist bin mkdir bin
"%CSC%" /nologo /target:winexe /codepage:65001 /optimize+ /out:bin\Arbeitsplan.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll src\*.cs

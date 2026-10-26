@echo off
rem Erstellt den kopierbaren Programmordner dist\Arbeitsplan\ (Release-exe, ini, Anleitung, Vorlagen, leerer Datenordner).
rem Vorhandene Daten in dist\Arbeitsplan\daten bleiben erhalten.
cd /d "%~dp0"
call "%~dp0build_release.cmd"
if errorlevel 1 exit /b 1
set D=dist\Arbeitsplan
if not exist %D%\daten mkdir %D%\daten
if not exist %D%\Vorlagen mkdir %D%\Vorlagen
copy /y release\Arbeitsplan.exe %D%\ >nul
copy /y config\statuscolors.ini %D%\ >nul
if not exist %D%\Arbeitsplan.ini copy /y config\Arbeitsplan.ini %D%\ >nul
copy /y config\LIESMICH.txt %D%\ >nul
echo OK %D%

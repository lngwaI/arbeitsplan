@echo off
rem Erstellt den fertigen Ordner fuer das Laufwerk: dist\Arbeitsplan\  (Programm\ = exe + ini, daten\ = Daten, Pakete, Sicherungen)
rem Vorhandene Daten in dist\Arbeitsplan\daten bleiben erhalten. Der einzige Weg zum Release (build.cmd ist nur fuer Entwicklung/Tests).
cd /d "%~dp0"
call "%~dp0build_release.cmd"
if errorlevel 1 exit /b 1
set D=dist\Arbeitsplan
if not exist %D%\Programm mkdir %D%\Programm
if not exist %D%\daten\Pakete mkdir %D%\daten\Pakete
if not exist %D%\daten\sicherung mkdir %D%\daten\sicherung
copy /y release\Arbeitsplan.exe %D%\Programm\ >nul
if not exist %D%\Programm\Arbeitsplan.ini copy /y config\Arbeitsplan.ini %D%\Programm\ >nul
copy /y config\LIESMICH.txt %D%\ >nul
echo OK %D%

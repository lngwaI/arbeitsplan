@echo off
rem Erzeugt Beispiel-Arbeitsplan\ (gitignored) aus beispiel\ + frisch gebauter Release-exe. Startet nichts.
rem Aufruf: scripts\beispiel-bauen.cmd   -> danach Ordner kopieren, Programm\Arbeitsplan.exe starten (siehe beispiel\README.md).
rem Der Ordner wird jedes Mal komplett neu erzeugt. Der Ausgangsplan (Sicherung) wird mit der Test-exe bin\Arbeitsplan.exe
rem erzeugt (nur wenn sie sich starten laesst, sonst weglassen: dann startet das Programm mit leerem Plan).
setlocal
pushd "%~dp0.."
set T=Beispiel-Arbeitsplan
set LOG=%TEMP%\arbeitsplan_beispiel_build.txt

if exist %T% if not exist %T%\LIESMICH.txt (echo %T% wurde nicht von diesem Skript erzeugt, bitte erst selbst pruefen. & goto fehler)

if exist release\Arbeitsplan.exe del release\Arbeitsplan.exe
call .\build_release.cmd > "%LOG%" 2>&1
if not exist release\Arbeitsplan.exe (type "%LOG%" & echo Release-Build fehlgeschlagen. & goto fehler)

if exist %T% rmdir /s /q %T%
mkdir "%T%\Programm" "%T%\daten\Pakete" "%T%\daten\sicherung"
copy /y release\Arbeitsplan.exe "%T%\Programm\" >nul
rem ini: Platzhalter __LOGIN__ durch den Windows-Namen ersetzen (Einrichtung/Admin beim ersten Start)
powershell -NoProfile -Command "(Get-Content 'beispiel\Arbeitsplan.ini' -Encoding UTF8) -replace '__LOGIN__',$env:USERNAME | Set-Content '%T%\Programm\Arbeitsplan.ini' -Encoding UTF8"
copy /y "beispiel\daten\Pakete\*.apkg" "%T%\daten\Pakete\" >nul
copy /y beispiel\daten\equipstatus.csv "%T%\daten\" >nul
copy /y beispiel\daten\Equipment-Liste.txt "%T%\daten\" >nul
copy /y beispiel\README.md "%T%\LIESMICH.txt" >nul
rem Statusdatei "frisch" (Dateizeit = jetzt)
powershell -NoProfile -Command "(Get-Item '%T%\daten\equipstatus.csv').LastWriteTime = Get-Date"

rem Ausgangsplan als Sicherung (Wiederherstellen im Programm); mit Test-exe, aktuelle Woche
if not exist bin\Arbeitsplan.exe call .\build.cmd >nul 2>&1
if exist bin\Arbeitsplan.exe (
  bin\Arbeitsplan.exe --makebeispiel "%CD%\%T%\daten" >nul 2>&1
)
dir /b "%T%\daten\sicherung\*.apbak" >nul 2>&1 || echo Hinweis: kein Ausgangsplan erzeugt - das Programm startet mit leerem Plan.
echo OK %T%
popd
endlocal
exit /b 0
:fehler
popd
endlocal
exit /b 1

@echo off
rem Erzeugt Test-Arbeitsplan\ (alles fuer den Test in der Firma) frisch aus dem aktuellen Stand des Quellcodes:
rem Release-exe, ini, LIESMICH, leerer Datenordner mit Testpaket, Anleitung, IT-Unterlage, VERSION.txt (Commit + Pruefsumme).
rem Laeuft automatisch nach jedem Merge/Pull/Commit auf main (.githooks), damit Testpaket und Demo nie hinterherhinken.
rem Der Ordner wird jedes Mal komplett neu erzeugt: dort nichts von Hand ablegen.
setlocal
pushd "%~dp0.."
set T=Test-Arbeitsplan
set L=%T%\1_Aufs-Laufwerk\Arbeitsplan_Test
set LOG=%TEMP%\arbeitsplan_build.txt

if exist %T% if not exist %T%\VERSION.txt (echo %T% wurde nicht von diesem Skript erzeugt, bitte erst selbst pruefen. & goto fehler)

if exist release\Arbeitsplan.exe del release\Arbeitsplan.exe
call .\build_release.cmd > "%LOG%" 2>&1
if not exist release\Arbeitsplan.exe (type "%LOG%" & echo Release-Build fehlgeschlagen. & goto fehler)

if exist %T% rmdir /s /q %T%
mkdir "%L%\Programm" "%L%\daten\Pakete" "%L%\daten\sicherung" "%T%\2_Fuer-die-IT" "%T%\3_Zum-Ausdrucken"
copy /y release\Arbeitsplan.exe "%L%\Programm\" >nul
copy /y config\Arbeitsplan.ini "%L%\Programm\" >nul
copy /y config\LIESMICH.txt "%L%\" >nul
rem Testpaket liegt fest im Repo, damit hier keine exe gestartet wird (Smart App Control blockiert unsignierte Builds).
rem Neu erzeugen, wenn sich Package.SampleCampaign aendert: bin\Arbeitsplan.exe --makepkg config\Testprodukt.apkg
copy /y config\Testprodukt.apkg "%L%\daten\Pakete\" >nul
copy /y config\Test-Arbeitsplan-ANLEITUNG.txt "%T%\0_ANLEITUNG.txt" >nul
copy /y docs\IT-Freigabe.md "%T%\2_Fuer-die-IT\IT-Freigabe.txt" >nul
copy /y config\LIESMICH.txt "%T%\2_Fuer-die-IT\" >nul
rem Uebersicht "Was liegt wo" als PDF (Edge drucken lassen), ohne Edge als HTML
set EDGE=%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe
set PDF=%CD%\%T%\3_Zum-Ausdrucken\Uebersicht.pdf
if exist "%EDGE%" start "" /wait "%EDGE%" --headless --disable-gpu --no-pdf-header-footer "--user-data-dir=%TEMP%\arbeitsplan_edge" "--print-to-pdf=%PDF%" "%CD%\config\Uebersicht.html"
if not exist "%PDF%" copy /y config\Uebersicht.html "%T%\3_Zum-Ausdrucken\" >nul
if exist "%PDF%" copy /y "%PDF%" "%T%\2_Fuer-die-IT\" >nul
rem nur lokal vorhanden (gitignored)
if exist _privat\1-Seiter-Vorgesetzter.pdf copy /y _privat\1-Seiter-Vorgesetzter.pdf "%T%\3_Zum-Ausdrucken\" >nul

> "%T%\VERSION.txt" (
  echo Arbeitsplan Testpaket - automatisch erzeugt, nicht von Hand aendern.
  for /f "delims=" %%a in ('git rev-parse --abbrev-ref HEAD') do echo Branch:  %%a
  for /f "delims=" %%a in ('git log -1 "--format=%%h %%ci %%s"') do echo Commit:  %%a
  echo Erzeugt: %DATE% %TIME:~0,5%
  for /f "delims=" %%a in ('git status --porcelain -- src config') do echo ACHTUNG: nicht committete Aenderung: %%a
  echo.
  echo SHA-256 von Programm\Arbeitsplan.exe ^(zum Pruefen nach dem Kopieren: certutil -hashfile Arbeitsplan.exe SHA256^):
  certutil -hashfile "%L%\Programm\Arbeitsplan.exe" SHA256 | findstr /v ":"
)
echo OK %T%
popd
endlocal
exit /b 0
:fehler
popd
endlocal
exit /b 1

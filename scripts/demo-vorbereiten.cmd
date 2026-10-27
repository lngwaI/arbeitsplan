@echo off
rem Richtet einen Demo-Datenordner ein (nur erfundene Testdaten): baut die exe, Ersteinrichtung, Testpaket.
rem Aufruf: scripts\demo-vorbereiten.cmd [ordner]   (Standard C:\APDemo)
setlocal
set D=%~1
if "%D%"=="" set D=C:\APDemo
pushd "%~dp0.."
call .\build.cmd
if not exist bin\Arbeitsplan.exe (echo exe fehlt & goto ende)

if exist "%D%\arbeitsplan.apdb" (echo %D% ist schon eingerichtet.) else (
  if not exist "%D%" mkdir "%D%"
  echo.
  echo Ersteinrichtung: "Neuen Arbeitsplan einrichten?" mit Ja beantworten.
  echo Danach Einstellungen - Nutzerverwaltung - "+": Login pwerk, Kuerzel WEPA, Paket Produktion.
  echo Dann das Fenster schliessen, das Skript macht weiter.
  start "" /wait bin\Arbeitsplan.exe --data "%D%" --user mmuster
)

if not exist "%D%\Pakete" mkdir "%D%\Pakete"
if not exist "%D%\Pakete\Testprodukt.apkg" bin\Arbeitsplan.exe --makepkg "%D%\Pakete\Testprodukt.apkg"
if exist "%D%\Pakete\Testprodukt.apkg" (echo Testpaket: %D%\Pakete\Testprodukt.apkg) else (echo Testpaket FEHLT)

echo.
echo Noch von Hand (scripts\demo-starten.cmd 1): ein paar erfundene Eintraege in dieser und
echo naechster Woche, dann Pakete - Kampagne starten - Testprodukt.
:ende
popd
endlocal

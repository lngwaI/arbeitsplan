@echo off
rem Baut und startet die Vorfuehr-Version (bin_demo\ArbeitsplanDemo.exe, nur erfundene Testdaten, eigener Datenordner
rem %LOCALAPPDATA%\ArbeitsplanDemo). Hat nichts mit scripts\demo-starten.cmd (normale exe, C:\APDemo) zu tun. Siehe docs\Demo.md.
rem Aufruf: scripts\demo-vorfuehrung.cmd [admin|produktion|leser]   (ohne Angabe: Startdialog "Rolle vorfuehren")
setlocal
pushd "%~dp0.."
tasklist /fi "imagename eq ArbeitsplanDemo.exe" | find /i "ArbeitsplanDemo.exe" >nul && (echo Die Vorfuehr-Version laeuft noch: erst alle Fenster schliessen. & goto ende)
call .\build_demo.cmd | find "BUILD FEHLER" >nul && (echo Build fehlgeschlagen. & goto ende)
if "%~1"=="" (start "" bin_demo\ArbeitsplanDemo.exe) else (start "" bin_demo\ArbeitsplanDemo.exe --role %~1)
:ende
popd
endlocal

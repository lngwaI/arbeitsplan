@echo off
rem Startet die Demo auf dem Demo-Datenordner (scripts\demo-vorbereiten.cmd vorher einmal ausfuehren).
rem Aufruf: scripts\demo-starten.cmd [1|2|system] [ordner]
rem   1      = nur Fenster 1 (mmuster, Admin)
rem   2      = Fenster 1 + Fenster 2 (pwerk, Produktion) wie zwei PCs; mit Win+Links / Win+Rechts nebeneinander
rem   system = pwerk ohne Speichern, wie ein Systemrechner
setlocal
set M=%~1
if "%M%"=="" set M=2
set D=%~2
if "%D%"=="" set D=C:\APDemo
pushd "%~dp0.."
if not exist "%D%\arbeitsplan.apdb" (echo Erst scripts\demo-vorbereiten.cmd ausfuehren. & goto ende)
if /i "%M%"=="system" (start "" bin\Arbeitsplan.exe --data "%D%" --user pwerk --nosave & goto ende)
start "" bin\Arbeitsplan.exe --data "%D%" --user mmuster
if "%M%"=="2" start "" bin\Arbeitsplan.exe --data "%D%" --user pwerk
:ende
popd
endlocal

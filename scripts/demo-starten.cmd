@echo off
rem Startet die Demo auf dem Demo-Datenordner (scripts\demo-vorbereiten.cmd vorher einmal ausfuehren).
rem Holt vorher den abgenommenen Stand (main) und baut neu: die Demo zeigt immer genau das, was abgenommen ist.
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

for /f "delims=" %%a in ('git rev-parse --abbrev-ref HEAD') do set BR=%%a
if /i "%BR%"=="main" (
  git pull --ff-only -q || echo Hinweis: kein Abgleich mit GitHub moeglich, es wird der lokale Stand gezeigt.
) else (
  echo ACHTUNG: Branch %BR% statt main - die Demo zeigt einen noch nicht abgenommenen Stand.
)
tasklist /fi "imagename eq Arbeitsplan.exe" | find /i "Arbeitsplan.exe" >nul && (echo Arbeitsplan laeuft noch: erst alle Fenster schliessen. & goto ende)
call .\build.cmd | find "BUILD FEHLER" >nul && (echo Build fehlgeschlagen, Demo nicht gestartet. & goto ende)
for /f "delims=" %%a in ('git log -1 "--format=%%h vom %%cs: %%s"') do echo Stand: %%a

if /i "%M%"=="system" (start "" bin\Arbeitsplan.exe --data "%D%" --user pwerk --nosave & goto ende)
start "" bin\Arbeitsplan.exe --data "%D%" --user mmuster
if "%M%"=="2" start "" bin\Arbeitsplan.exe --data "%D%" --user pwerk
:ende
popd
endlocal

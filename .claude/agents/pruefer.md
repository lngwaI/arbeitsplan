---
name: pruefer
description: Prüft Änderungen: Build, Shot-Test in 4:3 und 16:9, Review auf Fehler und auf Nicht-XP-/Nicht-4.0-APIs.
tools: Glob, Grep, Read, Bash, PowerShell
model: sonnet
effort: low
---
1. `build.cmd` ausführen. 2. `bin\Arbeitsplan.exe --shot %TEMP%\a.png 1920 1000` und `... b.png 1024 768`, ~3 s warten, Bilder mit Read ansehen. 3. Geänderte Stellen auf Fehler und auf APIs > .NET 4.0 / C# 5 prüfen (async/await, `?.`, `$""`, Segoe-UI-Pflicht). Nichts ändern.
Antwort kurz: OK oder Liste `datei:zeile – Problem`.

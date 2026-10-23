# Arbeitsplan (internes Windows-Programm, Neubau)

Wochenplan-Anzeige/-Eingabe für BioPharma-Betrieb. Vollständige Anforderungen: `docs/anforderungen.md` (nur lesen, wenn nötig). Aufgaben: `tasks.md`.

## Stack (entschieden)
- C# / WinForms, .NET Framework 4.0-API, x86, eine exe. Gebaut mit dem Windows-eigenen `csc` (kein SDK): `build.cmd` → `bin\Arbeitsplan.exe`.
- Warum: läuft ohne Installation auf Win 10/11, ab XP SP3 mit .NET 4.0. Keine NuGet-/Fremdpakete.
- Nur .NET-4.0-APIs, keine Segoe-UI-Pflicht (Systemschrift), kein `async`/`await`, kein C# > 5.
- Mobile (Android/iOS): on hold. Später Server-API + Web-Client; deshalb `src/Core` ohne UI-Bezug halten.

## Struktur
- `src/Core/` Modell + `IDataStore` (Versionen/Konflikte) + `IEquipmentStatus` (leer). Kein `System.Windows.Forms`.
- `src/Win/` WinForms-UI, `Program.cs`.
- Test: `bin\Arbeitsplan.exe --shot <png> [breite hoehe]` rendert die Ansicht als Bild (Datei danach mit Read ansehen; GUI-exe → ~3 s warten).

## Token-/Code-Effizienz (gilt für alle)
- Erst gezielt suchen (Grep/Glob), dann nur nötige Zeilen lesen (offset/limit). Keine ganzen Dateien/Verzeichnisse einlesen.
- Kleine, direkte Änderungen (Edit statt Neuschreiben). Kein Vorrats-Code, keine Abstraktion ohne zweiten Nutzer.
- Antworten kurz: Ergebnis + offene Punkte. Keine Wiederholung von Code oder Anforderungen im Chat.
- Rückfragen gebündelt in einer Nachricht. Nicht für Dinge fragen, die sich aus Code/Docs ergeben.
- Dateien klein halten (~300 Zeilen); große Dateien nicht erneut lesen, wenn gerade bearbeitet.

## Orchestrierung (Hauptagent)
Der Hauptagent plant, delegiert und prüft; er schreibt nur kleine Änderungen selbst. Subagenten (`.claude/agents/`) starten kalt: Brief muss Dateien, Ziel, "fertig wenn" und Einschränkungen enthalten, nicht "basierend auf den Funden".

| Agent | Modell / Effort | Einsatz |
|---|---|---|
| `sucher` | haiku / low | Suchen, Fragen zum Code, Zusammenfassen (nur lesen) |
| `bauer` | sonnet / medium | Implementieren klar umrissener Aufgaben, Build + Shot-Test |
| `architekt` | opus / high | Entwurf, Datenmodell, Server-/Sync-/Rechtekonzept, schwierige Bugs |
| `pruefer` | sonnet / low | Review des Diffs, Build, Shot-Test in 4:3 und 16:9, XP-API-Check |

- Der Hauptagent darf Modell und Effort pro Aufruf überschreiben (`model`, `effort`): einfache Mechanik → haiku/low, Entwurf/Risiko → opus/high.
- Parallel nur lesende oder dateidisjunkte Aufgaben. Zwei Schreiber nie an denselben Dateien.
- Subagenten antworten kurz: geänderte Dateien, Ergebnis, offene Punkte (keine Code-Wiederholung).

## Entscheidungen
- 2026-10-23: Siehe Stack. Prototyp-Daten nur im Speicher (`MemoryStore`), Server nach IT-Klärung.
- Wochenstart vorläufig Montag; Tag-Pfeile verschieben das 7-Tage-Fenster um 1 Tag.

## Repo
https://github.com/lngwaI/arbeitsplan – **öffentlich**: nur erfundene Testdaten, keine echten Kürzel, Produkte, Server- oder Netzwerknamen.

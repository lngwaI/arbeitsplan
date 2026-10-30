# Arbeitsplan (internes Windows-Programm, Neubau)

Wochenplan-Anzeige/-Eingabe für BioPharma-Betrieb. Anforderungen: `docs/anforderungen.md` (nur lesen, wenn nötig). Aufgaben: `tasks.md`.
Alle Details (Dateiliste, Entscheidungen, Testhilfen): `docs/Projekt-Details.md` (Inhaltsverzeichnis am Anfang, bei Bedarf gezielt lesen).

## Stack (zwingend)
- C# / WinForms, .NET Framework 4.0-API, x86, eine exe. Nur .NET-4.0-APIs, kein `async`/`await`, kein C# > 5 (csc 4.x). Keine NuGet-/Fremdpakete, Systemschrift.
- Gebaut mit dem Windows-eigenen `csc` (kein SDK). Mindestsystem Windows 10. Oberfläche nur über `src/Win/Theme.cs` (`Theme.Current`).
- `src/Core` ohne UI-Bezug (`System.Windows.Forms` verboten; später Server-API + Web-Client).
- Typname `Right` nicht verwenden (kollidiert mit `Control.Right`) → `Perm`.

## Build/Test
- Bauen: PowerShell `& .\build.cmd` → `bin\Arbeitsplan.exe`. Release nur über `make_dist.cmd`. Demo: `build_demo.cmd` (`bin_demo\`, nie ausliefern).
- Selbsttest: `bin\Arbeitsplan.exe --shot s.png --selftest st.txt` (nur zusammen mit `--shot`, ~2–3 Min, 0 "FEHLER"). Testbeschriftungen nie "Fehler" nennen (Workflow zählt Treffer). Testfenster nie ohne `f.dirty = false` schließen. Gestartete Instanzen danach beenden.
- UI-Änderung: `--shot <png> [--size BxH] ...` und Bild mit Read ansehen (weitere Schalter: Projekt-Details, Abschnitt Struktur).
- CI-Ergebnis: `bash scripts/ci-ergebnis.sh <sha> <branch> <zielordner>`. Smart App Control ist beim Nutzer aus: lokal bauen/testen geht.

## Dateien/Struktur
- Dateien ≤ ~300 Zeilen, als `partial` nach Thema: Präfixe `Backup.*`, `Packages.*`, `PlanView.*`, `MainForm.*`, `PackageForm.*`.
- Datenformat: `Backup.FormatVersion` aktuell 5 (Datei `version`); neuere Datei → OFFLINE, nie zurückschreiben; bei inkompatibler Änderung erhöhen. Paketformat 7/8.
- Historie ohne Obergrenze: nie alte Wochen löschen. Fachregeln/Preise nie eigenmächtig ändern.

## Themen (Details: docs/Projekt-Details.md#<Abschnitt>)
- Daten/FileStore, Ansicht, Bearbeitung, Textformat, Export/Pakete, Soft-Lock, Start/Verteilung/Domäne, Rechte/Rollen, Equipment, Katalog, Protokoll, Sicherung: Abschnitt "Entscheidungen und Annahmen".
- Pflichtpunkte, Rundgänge, Rollen, Plan teilen, Wochenansicht 2: Abschnitt "Nachtschicht 2026-10-27/28" (`Core/Duties.cs`).
- Paket-Editor v4, Puffer-Liste, Notizen, Soft-Lock-Übernahme: Abschnitt "Paket-Editor v4 ..." (Entwurf `docs/paket-editor-v4.md`).
- Freeze: `docs/Freeze-Entwurf.md`. Demo: `docs/Demo.md`. Beispiel-Umgebung: `beispiel/README.md`. Statusdatei: `docs/Equipment-Status.md`. UX-Hilfen: `docs/UX-Hilfen.md`. Aktuellster Stand: Abschnitt "Stand 2026-10-30".

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

- Modell/Effort pro Aufruf überschreibbar (`model`, `effort`): einfache Mechanik → haiku/low, Entwurf/Risiko → opus/high.
- Parallel nur lesende oder dateidisjunkte Aufgaben. Zwei Schreiber nie an denselben Dateien.
- Subagenten antworten kurz: geänderte Dateien, Ergebnis, offene Punkte (keine Code-Wiederholung).

## Repo
https://github.com/lngwaI/arbeitsplan – **öffentlich**: nur erfundene Testdaten, keine echten Kürzel, Produkte, Equipment-/Server-/Netzwerknamen. Beispiele des Nutzers (echte Equipment-Nummern) nicht übernehmen, nur das Schema.

## Vertraulichkeit (zwingend, Repo ist öffentlich)
- Vertrauliches Material (Original-exe, Screenshots, Texte des Nutzers) liegt nur in `_privat/` (gitignored, pre-commit-/pre-push-Hook sperren es). Nie committen, pushen, in Artifacts/Docs/Web/Vault/Memory/Issues/PRs/CI-Bilder/`tasks.md`/`HANDOFF.md` oder Commit-Texte übernehmen.
- Erkenntnisse daraus nur abstrahiert und mit erfundenen Beispielen festhalten (Schema statt echter Namen/Nummern/Struktur-Details); Subagenten-Briefs dürfen keine Originalinhalte enthalten, nur den Pfad in `_privat/`.
- `_privat/`-Bilder nur lokal mit Read ansehen; die exe nicht in den CI-Workflow geben.

## Account-Kennung
- Das Label (welcher Claude-Account) steht zu Sessionbeginn im Kontext. Falls nicht: `node scripts/claude-account.js` ausführen.
- Branches heißen `<slug>/<kurzbeschreibung>` (Slug per `node scripts/claude-account.js --slug`).
- Commits bekommen automatisch die Zeile `Claude-Account: <Label>` (Hook `.githooks/prepare-commit-msg`, `core.hooksPath` = `.githooks`).
- In tasks.md hat jede Aufgabe Label, Status, Zeitstempel, z. B. `- [ ] Login-Seite (GMX / Haupt-Account, in Arbeit, 2026-10-25 01:40)`.
- Jeder Eintrag in docs/HANDOFF.md beginnt mit Datum, Uhrzeit und Label, z. B. `## 2026-10-25 01:40 – GMX / Haupt-Account`.
- Vor dem Arbeiten prüfen, ob eine Aufgabe schon von einem anderen Account beansprucht ist; dann nicht parallel daran arbeiten.
- Ist das Label "Unbekannt", erst beim Nutzer nachfragen.

## Git
- Kein `git add -A`, solange Subagenten schreiben (gezielt Dateien adden). Nur auf Wunsch committen/pushen.

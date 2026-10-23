# Arbeitsplan (internes Windows-Programm, Neubau)

Wochenplan-Anzeige/-Eingabe für BioPharma-Betrieb. Vollständige Anforderungen: `docs/anforderungen.md` (nur lesen, wenn nötig). Aufgaben: `tasks.md`.

## Stack (entschieden)
- C# / WinForms, .NET Framework 4.0-API, x86, eine exe. Gebaut mit dem Windows-eigenen `csc` (kein SDK): `build.cmd` → `bin\Arbeitsplan.exe`.
- Warum: läuft ohne Installation auf Win 10/11, ab XP SP3 mit .NET 4.0. Keine NuGet-/Fremdpakete.
- Nur .NET-4.0-APIs, keine Segoe-UI-Pflicht (Systemschrift), kein `async`/`await`, kein C# > 5.
- Mobile (Android/iOS): on hold. Später Server-API + Web-Client; deshalb `src/Core` ohne UI-Bezug halten.

## Struktur
- `src/Core/` ohne `System.Windows.Forms`: `Model.cs` (Entry, `IDataStore`, `MemoryStore`+Testdaten), `Users.cs` (`Perm`-Rechte, User, Rechtepakete, `UserDb`, `ILockService`), `Equipment.cs` (`IEquipmentStatusProvider`+Mock, `EquipmentMatcher`, `StatusColors`).
- `src/Win/`: `MainForm.cs` (Wochenansicht, Bearbeitungsmodus), `UserAdminForm.cs`, `Program.cs`. `config/statuscolors.ini` wird von `build.cmd` nach `bin\` kopiert.
- Aufruf/Test: `bin\Arbeitsplan.exe [--user <login>] [--nosave]`; Testbild: `--shot <png> [--size BxH] [--edit spalte,zeile] [--users]` (echter Bildschirmabzug, ~4 s warten, dann mit Read ansehen; DrawToBitmap zeichnet RichTextBox nicht).
- Testnutzer: `mmuster` Admin, `ebeispiel` Leser, `pwerk` Produktion. Mock-Lock: Di, Raum A "wird von Paula Werk bearbeitet" (mit `--user mmuster` testen). `--nosave` simuliert System-PC ohne Speichern.
- Typname `Right` nicht verwenden (kollidiert mit `Control.Right`) → `Perm`.

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

## Entscheidungen und Annahmen (Stand 2026-10-23)
- Siehe Stack. Prototyp-Daten nur im Speicher (`MemoryStore`), Server nach IT-Klärung.
- Wochenstart vorläufig Montag; "Heute" springt auf den Montag der Woche, Tag-Pfeile verschieben das Fenster um 1 Tag.
- Layout: Kopf `KWxx (TT.MM.JJJJ - TT.MM.JJJJ)`, darunter Datum je Spalte (`Mo, 23.10.2026`), dann Blöcke. Block-Überschrift = Raumbezeichnung (frei editierbar, leer erlaubt). Eigene Layout-Berechnung in `Reflow()`: Zeilenhöhe = höchster Block; Einteilungsfeld mind. 3 Zeilen (F/S/N), wächst per Textmessung mit (kein Abschneiden).
- Bearbeitung: Doppelklick auf Block = Bearbeitungsmodus (oranger Rahmen + Notizblock-Symbol), sonst nur lesbar. Ende: Strg+S (speichern), Esc (verwerfen, mit Rückfrage), anderer Block, Navigation, Schließen. Je Block eine Version; Konflikt → Ja (überschreiben) / Nein (im Editor bleiben).
- Soft-Lock: nur Hinweis-Popup "Das Fenster <Überschrift> wird gerade von <Nutzer> bearbeitet.", danach trotzdem bearbeiten/speichern. Sperre verfällt nach 30 min (abgestürzte Clients). Anzeigename: Vor-/Nachname aus `UserDb` (Windows-Login), sonst Windows-Nutzername. Raum wird nicht ermittelt.
- Speichern fehlgeschlagen (Exception): klare Meldung, Block bleibt im Bearbeitungsmodus, Navigation/Schließen werden blockiert bzw. fragen vor Verwerfen → nichts geht verloren.
- Annahme (Prototyp): unbekannter Windows-Nutzer erhält Paket "Produktion". Später: nach IT-Antwort (Leser/abweisen).
- Rechte: Lesen, Text ändern/löschen/hinzufügen, Farben/Textformat, Nutzer anlegen/bearbeiten/löschen. Pakete: Leser, Produktion, Admin. Paket gilt als "ausgewählt" nur bei **exakt** gleichen Rechten. Erzwungen sind bisher: Bearbeiten (ändern, oder hinzufügen bei leerem Block), "+ Zeile", Nutzerverwaltung. "Löschen" und "Format" noch nicht erzwungen/umgesetzt.
- Equipment: Erkennung per Regex `[A-Z0-9]{2}-[A-Z]{1,2}-\d{3,5}-\d{2}` + Abgleich mit `IEquipmentStatusProvider` (Mock). Nur existierende Nummern werden eingefärbt, **nur die Nummer**, restlicher Text bleibt wie formatiert (Statusfarbe überschreibt manuelle Farbe nur dort). Farben konfigurierbar über `statuscolors.ini` (Standard: nicht gereinigt/ohne Timer schwarz, gereinigt violett, regeneriert dunkelblau, sterilisiert rot, in Prozess grün, Freigabe hellblau, Quarantäne dunkelrot). MES-System-8-/MES-Anbindung später als weiterer Provider, Schnittstelle unbekannt.

## Repo
https://github.com/lngwaI/arbeitsplan – **öffentlich**: nur erfundene Testdaten, keine echten Kürzel, Produkte, Server- oder Netzwerknamen.

# Arbeitsplan (internes Windows-Programm, Neubau)

Wochenplan-Anzeige/-Eingabe für BioPharma-Betrieb. Vollständige Anforderungen: `docs/anforderungen.md` (nur lesen, wenn nötig). Aufgaben: `tasks.md`.

## Stack (entschieden)
- C# / WinForms, .NET Framework 4.0-API, x86, eine exe. Gebaut mit dem Windows-eigenen `csc` (kein SDK): `build.cmd` → `bin\Arbeitsplan.exe`.
- Warum: läuft ohne Installation auf Win 10/11, ab XP SP3 mit .NET 4.0. Keine NuGet-/Fremdpakete.
- Nur .NET-4.0-APIs, Systemschrift, kein `async`/`await`, kein C# > 5 (csc 4.x lehnt neuere Syntax ab).
- Mobile (Android/iOS): on hold. Später Server-API + Web-Client; deshalb `src/Core` ohne UI-Bezug halten.

## Struktur
- `src/Core/` ohne `System.Windows.Forms`: `Model.cs` (`Entry` Inhalt+Farbbereiche+Kürzel je Schicht, `RowInfo` Id/Name/F-S-N/Mindesthöhe je Zeile, `IDataStore` inkl. Zeilen/Sortieren/`Find`, `MemoryStore`+Testdaten), `Users.cs` (`Perm`-Rechte, User, Rechtepakete, `UserDb`, `ILockService`), `Equipment.cs` (`IEquipmentStatusProvider`+Mock, `EquipmentMatcher`, `StatusColors`).
- `src/Win/`: `PlanView.cs` (selbst gezeichnete Wochenansicht: Layout, Farben, Markierung, Maus), `TextLayout.cs` (Zeilenumbruch mehrfarbiger Texte), `MainForm.cs` (Leisten, Navigation, Suche, Bearbeitungsmodus), `UserAdminForm.cs`, `Program.cs`. `config/statuscolors.ini` wird von `build.cmd` nach `bin\` kopiert.
- Aufruf: `bin\Arbeitsplan.exe [--user <login>] [--nosave]`. Testhilfen (GUI-exe, ~4–6 s warten): `--shot <png> [--size BxH] [--edit spalte,zeile] [--search text] [--users]` (echter Bildschirmabzug, Bild mit Read ansehen), `--bench <txt>` (ms je Wochenwechsel), `--selftest <txt>` (Admin: Bearbeiten/Farbe/Speichern/Zeile/Sortieren).
- Testnutzer: `mmuster` Admin (Kürzel MUMA), `ebeispiel` Leser (BEER), `pwerk` Produktion (WEPA). Mock-Lock: Di, Zeile "Suite 1" "wird von Paula Werk bearbeitet" (mit `--user mmuster` testen). `--nosave` simuliert System-PC ohne Speichern.
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

## Entscheidungen und Annahmen (Stand 2026-10-24)
- Prototyp-Daten nur im Speicher (`MemoryStore`), Server nach IT-Klärung. Wochenstart vorläufig Montag; "Heute" springt auf den Montag, Tag-Pfeile verschieben das Fenster um 1 Tag.
- **Ansicht selbst gezeichnet** (`PlanView`), nicht als Steuerelemente: Wochenwechsel ~40 ms statt Aufbau von hunderten Controls. Nur der aktive Block bekommt Eingabefelder (`MainForm.edXxx`), sie werden nach jedem Layout über den Block gelegt (`PositionEditors`). `MeasureText` addiert einen festen Rand, `TextBlock` rechnet ihn heraus.
- Layout: Kopf `KWxx (TT.MM.JJJJ - TT.MM.JJJJ)`, Datum je Spalte, **10 Standardzeilen** (Suite 1, Mix / Split, Suite 2, Puffer, Freeze / Thaw, Einwaage, CIP / SIP, Raum CC + 2 leere). Blockname, F/S/N-Bezeichnung und Mindesthöhe gelten je Zeile für alle Tage (`RowInfo`). Je Zeile sind Inhaltsfeld und jedes Schichtfeld in allen 7 Spalten gleich hoch. **Schichtfeld fest mind. 3 Textzeilen** (3 Reihen 4-stelliger Kürzel mit ", "), wächst nur bei mehr (kein Abschneiden). Schichten farbig (F gelb, S orange, N blau).
- Markierung: eigenes Kürzel (`User.Kuerzel`, aus Nutzer-DB) grün; **Suchfeld** (oben rechts, Strg+F) markiert Treffer gelb in Inhalt/Equipment/Kürzel (nicht Raumnamen) und zählt sie für die Woche; Enter springt zum nächsten Treffer (`IDataStore.Find`, auch in anderen Wochen).
- Bearbeitung: Doppelklick = Bearbeitungsmodus (oranger Rahmen + Notizblock-Symbol). Ende: Strg+S, Esc (verwerfen mit Rückfrage), anderer Block, Navigation, Schließen. Versionsprüfung je Block; Konflikt → Ja (überschreiben) / Nein (im Editor bleiben).
- Soft-Lock: nur Popup "Das Fenster <Überschrift> wird gerade von <Nutzer> bearbeitet.", danach trotzdem bearbeiten/speichern; Sperre verfällt nach 30 min. Anzeigename: Vor-/Nachname aus `UserDb` (Windows-Login), sonst Windows-Nutzername; Raum wird nicht ermittelt.
- Speichern fehlgeschlagen: klare Meldung, Block bleibt im Bearbeitungsmodus, Navigation/Schließen blockiert bzw. Rückfrage → nichts geht verloren.
- Unbekannter Windows-Nutzer erhält im Prototyp Paket "Produktion" (später nach IT-Antwort: Leser/abweisen).
- Rechte: Lesen, Text ändern/löschen/hinzufügen, **Farben/Textformat** (Schriftfarbe im Inhalt, Leiste "Schriftfarbe" im Bearbeitungsmodus), **Blockname/F-S-N ändern + Zeile hinzufügen** (`EditNames`), **Blockhöhe** (`EditHeight`, +/- im Bearbeitungsmodus), **Zeilen sortieren** (`SortRows`, Hoch/Runter für die angeklickte Zeile; nur "hohe" Rechte = Admin), Nutzer anlegen/bearbeiten/löschen. Pakete: Leser, Produktion (ohne EditNames/EditHeight/SortRows), Admin. Paket "ausgewählt" nur bei **exakt** gleichen Rechten. Zeilen **löschen: nein**. "Text löschen" noch nicht erzwungen.
- Schriftfarbe: Farbbereiche (`Entry.Spans`, Start/Länge/RGB) nur im Inhaltstext; Standard (schwarz) = kein Bereich. Im Editor werden Statusfarben **nicht** angezeigt (nur manuelle Farben), damit beim Speichern nichts vermischt wird.
- Equipment: Schema `H<1|2>-<T|F|X|P>-<4 Ziffern>` oder `…-<2 Ziffern>` (Regex in `EquipmentMatcher`) + Abgleich mit `IEquipmentStatusProvider` (Mock). Nur existierende Nummern, **nur die Nummer** wird in Statusfarbe gezeigt und **überschreibt** dort die manuelle Farbe. Der Status wird **nie als Text** angezeigt (kein Tooltip, keine Beschriftung), nur die Nummer ist eingefärbt. Farben in `statuscolors.ini` (Standard: nicht gereinigt/ohne Timer schwarz, gereinigt violett, regeneriert dunkelblau, sterilisiert rot, in Prozess grün, Freigabe hellblau, Quarantäne dunkelrot). ein MES-System später als weiterer Provider.

## Repo
https://github.com/lngwaI/arbeitsplan – **öffentlich**: nur erfundene Testdaten, keine echten Kürzel, Produkte, Equipment-/Server-/Netzwerknamen. Beispiele des Nutzers (echte Equipment-Nummern) nicht übernehmen, nur das Schema.

# Paket-Editor v4 und Notizzeilen – Entwurf, Stand 2026-10-29

Status: **Entwurf, nicht umgesetzt** (Architekt Opus/high, Hauptagent hat die Datei geschrieben). Baut auf `docs/paket-editor-v3.md` auf; dort entfallen **Hold-Zeiten** und **Wartungszeitraum**. Nutzerentscheidungen aus `tasks.md` (Plan 2026-10-29, A3, D, E) sind verbindlich. Erst die offenen Fragen am Ende klären, dann E1 starten.

## Ist-Stand (Branch gmail/paket-editor-v4)
- Paketformat 6 (`Packages.Xml.cs`), `Backup.FormatVersion` = 4 (`Backup.cs:41`, noch nicht verteilt laut `tasks.md`).
- Schritte haben eigene Wiederholung (`RepeatMode`, `Count`, `Interval`, `Shift` je Lauf); Bezug „Tag 1 = Startdatum“. Den Hauptlauf errät `Schedule()` (Lauf-Schritt mit den meisten Wiederholungen). `BuildTargets` nutzt ohne `schedule` `s.Runs()` = zweite Rechenquelle.
- Kennung: Komma-Liste je Schritt (`Ident`). Notiz: Zeile „Notiz: …“.
- `RunNumbers` wertet „Lauf <8 Ziffern>“ und jede Zeile, die auf 8 Ziffern endet.

## Grundentscheidungen

### Zeitmodell (Datum statt „Tag 1“)
- **Gewählt:** Tag-Raster bleibt intern (1-basierte Paket-Tage, frühester Schritt = Tag 1), Anzeige relativ zu Lauf 1. `Package.Lauf1Day` = erster Tag des Hauptlaufs. Rutscht ein Schritt vor Tag 1, verschiebt `Normalize()` alle Schritte um +k (Wochentag von Tag 1 aus „Lauf 1 am“ neu). Oberfläche zeigt nie Tagesnummern/Minus-Tage.
  - Vorteil: `Days()`, `MoveAll`, `MoveRun`, `StepOrder`, XML-Attribute bleiben; ältere exe lesen Tage weiter. Nachteil: `Normalize()` bei jeder Verschiebung vor Tag 1 (Golden-Test).
- Verworfen: Versatz relativ zu Lauf 1 mit Minuswerten (ändert alle Tag-Rechnungen). Verworfen: absolute Daten im Paket (Pakete sind Vorlagen).

### Notizzeilen (A3)
- **Gewählt:** Merker an `FormatSpan` (`Note`), immer über ganze Zeilen und zusätzlich `Italic`.
  - `Conflict.Merge` arbeitet schon zeilenweise samt Spans; Sicherung, Plan-Datei, Paket, Protokoll speichern Spans bereits; ältere Leser sehen die Notiz kursiv.
  - Nachteil: RichTextBox braucht sichtbares Ersatzmerkmal (A3b: eigene Hintergrundfarbe `SelectionBackColor`, `ExtractSpans` erkennt sie).
- Verworfen: `Entry.NoteLines` (Zeilennummern verrutschen, eigene diff3-Logik). Verworfen: Präfixzeichen im Text (verschmutzt Suche/Export/Laufnummern).

## Datenmodell

### Core/Model.cs (A3)
- `FormatSpan.Note` (bool); `Key()` nimmt `Note` auf.
- Neue Datei `Core/Notes.cs` (statische Klasse `Notes`): `NoteLines(Entry)`, `ContentText(Entry)` (ohne Notizzeilen), `Normalize(Entry)` (Spans auf volle Zeilen, `Italic`, leere Zeilen ohne Note, Überlappungen zusammenfassen), `SetNote(Entry, first, last, on)`.
- Leer-Regeln zusammen mit A2 an einer Stelle: `HasText` = `ContentText(e).Trim().Length > 0`; `IsEmpty`/`IsBlank` nutzen `HasText`. Block nur mit Notizen = leer (Zuklappen, oranger Punkt, LKZ, offene Pflichten, Paketmodus „nur leere füllen“).
- `RemovesText` zählt Notiztext weiter als Text (Notiz löschen = `DeleteText`); Markieren/Aufheben = `EditText`.

### XML
- Datendatei/Sicherung (`Backup.WriteEntry/ReadEntry`): `<Fmt … i="1" n="1"/>`, fehlendes `n` = keine Notiz. Paket: `<Format … kursiv="1" notiz="1"/>`.
- `Backup.FormatVersion`: Notizen + neue Baustein-Felder in **4**, falls v4 noch nirgends verteilt (auch nicht als Test in der Firma), sonst **5** (Frage 12). Ältere exe → OFFLINE, kein Zurückschreiben; alle PCs gleichzeitig aktualisieren.

### Merge, Konflikt, Protokoll
- `Conflict.Merge`: nach `Join` `Notes.Normalize(r)`. Markierung gegen Textänderung derselben Zeile = Konflikt an „Inhalt“.
- `Package.Merge`: FillEmpty + Block nur mit Notizen → Paket-Text zuerst, Notizen mit Leerzeile angehängt; Append unverändert; Overwrite: Frage 8.
- Protokoll: `Audit.cs:66` zeigt „(nur Notizen) …“ statt „(leer)“; Verlauf zeigt „Notiz markiert/aufgehoben“.
- `RunNumbers.Find(Entry)` (neue Überladung) überspringt Notizzeilen; `Package.PastRuns` nutzt sie.

### Paket (Paketformat 7)
`Package`:
- `Type`: `PackageType { Kampagne, Einzel, Ereignis }`, XML `typ`. Fehlt: mit Lauf-Schritt = Kampagne, sonst Ereignis (TFF Flush, Zwischenreinigung).
- `Lauf1` (DateTime, optional), XML `lauf1`: Vorschau + Vorschlag beim Kampagnenstart (Frage 6).
- `RunCount`: liest/setzt `Count` des Hauptlauf-Schritts (setzt `Until` = 0).
- `Head`: der eine Hauptlauf-Schritt (`Kind == Run`, `Bind == Frei`) mit Laufplan (`Repeat`, `Interval`, `WeekDays`, `DayList`, `Count`, `StartNo`, `Shift`). `Lauf1Day` = `Head.FirstDay`.

`PackageStep` (neu):
- `Bind`: `StepBind { Frei, JeLauf }`, XML `bezug="lauf"`. JeLauf: Vorkommen = Hauptlauf-Tag i + `RunOffset`, `Group = i`.
- `RunOffset` (−30..+30), XML `laufversatz` (Puffer −1 = „1 Tag vor jedem Lauf“).
- `RunEvery` (≥ 1, Standard 1), XML `jelaeufe`; 2 = „reicht für 2 Läufe“ (Läufe 1, 3, 5 …), nur Puffer, nur unter „Mehr Optionen“.
- `ManualNo` (8 Ziffern oder leer), XML `nr`: Packen-/Einzelpaket-Nummer von Hand, ohne Vorschlag. Plantext „<Titel> (Nr. 12340001)“; die Klammer verhindert, dass `RunNumbers` sie als Kampagnenlauf zählt (Frage 4).
- Kennung: `IdentStart` (XML `kennungstart`, z. B. „PA 40010001“, Endziffern +1 je stattfindendem Lauf, Stellenzahl bleibt); `IdentOver` (Lauf → Text, „-“ = aussetzen), XML `kennungje="3=PA 40010099;5=-"`; `IdentFor(run)`. Altes `kennung` wird aufgelöst zusätzlich ausgeschrieben.
- `Skip` (nur Hauptlauf), XML `ausfall="3,5"` (1-basiert): Lauf fällt aus, Nummer wird nicht vergeben (Lauf 1, 2, 4), JeLauf-Schritte dieses Laufs entfallen.
- `Move` (E5; Lauf → Tage, gilt ab diesem Lauf für alle folgenden), XML `versatzab`. Bestehendes `Shift` (`versatz`) heißt ab v4 „nur dieser Lauf“ (fest).
- `Note`: bleibt, gilt „bei jedem Lauf“; Ausgabe als Notizzeile(n) ohne Präfix „Notiz:“ (Frage 11).

`Baustein` (Katalog): `Variant` (XML `variante`), `NoteTemplate` (mehrzeilig, XML `notizvorlage`).
`StepKind` bleibt in der Datei; Oberfläche: `Normal` = „Sonstiges (ohne Laufnummer)“ (auch `Label()`).

### Eine Rechenquelle
`Package.Schedule(gaps)` wird einzige Quelle für Vorkommen (Schritt, Lauf, Tag, Group, Label, Row): 1. Laufplan Hauptlauf mit `Move`/`Shift`/`gaps`; 2. ohne `Skip`-Läufe; 3. JeLauf-Schritte aus den Hauptlauf-Tagen; 4. freie Schritte aus eigenem Raster. `BuildTargets` nutzt ebenfalls `Schedule()`. Vorschau, Kalender, `CampaignForm` lesen nur `Schedule()`.

### Migration Paketformat ≤ 6 → 7 (beim Laden)
1. Hauptlauf = Algorithmus des alten `Schedule()`.
2. Schritte mit `Kind != Normal` sowie Schritte, deren Tage genau Hauptlauf-Tage + konstanter Versatz sind (gleiche Anzahl, keine abweichenden `Shift`): `JeLauf`, `RunOffset` = Versatz.
3. Rest bleibt frei mit bisherigen Tagen.
4. Alte `kennung`-Liste: Folge +1 → `IdentStart`, sonst erster Wert + `IdentOver`.
5. `Normalize()`.
Golden-Test: `Schedule()` vor/nach Migration gleich für `SampleCampaign`, `config/Testprodukt.apkg`, alle Selbsttest-Pakete. Schreiben in v7 zusätzlich mit aufgelösten Attributen (`tag`, `anzahl`, `abstand`, `versatz`, `kennung`) für ältere exe; speichert eine ältere exe ein Paket, gehen v7-Bezüge verloren.

## Regeln und Prüfung (E4, `Core/Packages.Rules.cs`)
`List<PackageProblem> Check()` mit `Severity { Sperre, Hinweis }`; Hinweisliste und `CampaignForm` nutzen sie; `Apply` wirft bei Sperre.

Sperren: Kampagne ohne/mit mehr als einem Hauptlauf-Schritt · Laufanzahl außerhalb 1..99, Laufnummer > 9999 · **zwei Hauptläufe am selben Tag** (nach `Move`/`Shift`/Start; Unterlauf/Puffer am selben Tag erlaubt; global oder je Raum: Frage 3) · **Pufferläufe > Produktläufe** (freier Puffer-Schritt mit mehr Vorkommen als stattfindende Läufe) · **Unterläufe < Hauptläufe** („Lauf 4 ohne Unterlauf“) · Kennungsstart ohne Endziffern bei > 1 Lauf · Einzelpaket mit Lauf-/Unterlauf-Schritt, Ereignis mit `ManualNo`.
Hinweise: Packen ohne `ManualNo` (Pflicht erst beim Kampagnenstart) · Schritt > 60 Tage vor Lauf 1 · `RunEvery` > 1 bei Nicht-Puffer.
Bestehende Prüfungen bleiben (Back-Up, `CheckBelongs`, Kompatibilität).

## Oberfläche (Skizzen)
- **Kopf** (`PackageForm.Head.cs`): `Kampagne · Produkt [AB1] · Erste Laufnummer [12340001] · Läufe [5 ▲▼] · alle [3] Tage ▾ · Lauf 1 am [Fr 12.03.2027]`. Einzelpaket: `Name · Raum ▾ · Laufnummer: beim Übernehmen`. Ereignis: `Sonderereignis · Name`.
- **Wann** (`PackageForm.When.cs`): Bezug „zu jedem Lauf“ (`[1] Tag(e) vor/am/nach jedem Lauf`; Mehr Optionen: `reicht für [n] Läufe`, nur Puffer) oder „eigenes Datum“ (Datumsfeld, Beschriftung `Fr 12.03.2027 · 3 Tage vor Lauf 1`; Wiederholung wie bisher, Ende „nach n“/„bis Lauf k“). Termin-Liste/Kalender: `Fr 12.03.2027 · 3 Tage vor Lauf 1`, `Mo 15.03.2027 · Lauf 1`, `Di 16.03.2027 · 1 Tag nach Lauf 1`. Helfer `Package.RelText(day)`.
- **Details** (`PackageForm.Details.cs`): Art „Hauptlauf“, „Lauf-Schritt mit Laufnummer“, „Unterlauf“, „Sonstiges (ohne Laufnummer)“; Packen: `Laufnummer (von Hand)` ohne Vorschlag; Feld `Notiz – steht bei jedem Lauf (kursiv, zählt nicht als Inhalt)`; Puffer: Knopf `Materialvorlage einfügen` (`Baustein.NoteTemplate`, z. B. `Schläuche DN __ · Dichtungen DN __ · Tüllen DN __`).
- **Kennung je Lauf** (neu `PackageForm.Idents.cs`): `Kennung Start [PA 40010001] (+1 je Lauf)`; Tabelle `Lauf | Datum | Laufnummer | Kennung | fällt aus ☐`; überschrieben = orange/fett; Entf = aussetzen („—“); Rechtsklick „auf Automatik zurück“; ausgefallener Lauf grau, Nummer durchgestrichen, Folgeläufe behalten Nummern.
- **Schritt hinzufügen** (`PackageForm.Picker.cs`): neu Puffer mobil, Puffer stationär, Zwischenreinigung (Sonderereignis, nur von Hand). Alter Baustein „Puffer“ bleibt im Katalog, im Picker ausgeblendet.
- **Assistent** (`CampaignWizard.cs`): Kampagne | Einzelpaket (eigene Laufnummer, anderer Raum) | Sonderereignis (Frage 5).
- **Kampagnenstart** (`CampaignForm.cs`): `Lauf 1 am [Datum]` statt „Startdatum (Tag 1)“; `Läufe [n ▲▼]` ändert `RunCount` der Kopie; `Erste Laufnummer`; `Laufnummer Packen` (Pflicht); Tabelle mit Spalten „Bezug“ und „fällt aus“; zwei Hauptläufe am Tag = rote Zeile, Übernehmen gesperrt; Zeilen vor Lauf 1 auf belegten Blöcken: Hinweis „belegt“ + Modus aus Frage 7. Einzelpaket: einfacher Modus mit Datum, Raum, Laufnummer.
- **Vorschau (E5/E6)** (`PackageForm.Strip.cs`, `.Preview.cs`, `.Order.cs`): Zeilen in Planreihenfolge (`GetRows`), Spalten = Tage. Ziehen am Kopf verschiebt diesen und alle Folgeläufe (`Move`), mit Umschalt nur diesen (`Shift`), Tooltip erklärt das. Verschoben = fest (Schloss), Rechtsklick „auf Regel zurücksetzen“. Ziehen auf Tag mit anderem Hauptlauf abgelehnt. Leiste: Zoom −/+ (Tag, halbe Woche, Woche), ↶ ↷ (Strg+Z/Strg+Y), `nur Auswahl` mit ◀ ▶. Undo: Schnappschuss der Paket-XML, max. 50, nach jeder abgeschlossenen Änderung.
- **Notizen im Plan (A3b)**: Editor: Notizzeilen kursiv mit hellem Hintergrund (`Theme.Current.NoteBack`); `LoadEditors` setzt `SelectionBackColor`, `ExtractSpans` setzt `Note`, danach `Notes.Normalize`. Enter am Ende einer Notizzeile: neue Zeile ohne Notizformat; „Standardformat“ hebt keine Notiz auf. Kontextmenü: „Als Notiz markieren“ / „Notiz aufheben“. Ansicht: Rechtsklick auf Textzeile → gleiche Einträge, sofort gespeichert wie der LKZ-Haken (`Audit.Save`, Versionsprüfung, `ConflictForm`). Darstellung nur kursiv. Zugeklappt + nur Notizen: Frage 9.

## Bauschritte (je an `bauer`, danach Prüfung, Push)
- **A3a Notizen Core** (nach A2): `Core/Model.cs`, neu `Core/Notes.cs`, `Backup.cs`, `Packages.Xml.cs`, `Conflict.cs`, `Packages.cs` (Merge), `RunNumbers.cs`, `Packages.Runs.cs`, `Audit.cs`. Fertig: Block nur mit Notizen = leer, Rundreise Sicherung/Paket/Plan-Datei, Konflikt Markierung vs. Text, FillEmpty behält Notizen.
- **A3b Notizen Oberfläche**: `MainForm.Edit.cs`, `MainForm.Format.cs`, `PlanView*.cs`, `HistoryForm.cs`, `Theme.cs`. Fertig: Shot `--edit` zeigt Notizzeile, Rechtsklick speichert, Selbsttest grün.
- **E1a Zeitmodell Core**: `Packages.cs`, `Packages.Repeat.cs`, `Packages.Runs.cs`, `Packages.Xml.cs` (v7, Migration), neu `Packages.Rel.cs`. Fertig: Golden-Test grün.
- **E1b Kopf + Wann**: `PackageForm.Head/When/Sizing/Strip/Details/Picker`. Fertig: Shots ohne „Tag n“.
- **E1c Kampagnenstart**: `CampaignForm.cs`, `PackageForm.cs`. Fertig: `--form campaign2`, `--applypkg` mit geänderter Laufanzahl.
- **E2 Kennung, Notizen aus Paketen, Materialvorlage**: neu `PackageForm.Idents.cs`, `Packages.cs` (IdentFor, Compose), `Catalog.cs`, `CatalogForm.cs`, `PackageForm.Details.cs`. Fertig: Ausfall/Aussetzen/Überschreiben ergeben im Selbsttest die erwarteten Nummern.
- **E3 Puffer mobil/stationär, Pakettypen, Zwischenreinigung**: `Catalog.cs`, `PackageForm.Picker/When`, `CampaignWizard.cs`, `CampaignForm.cs`.
- **E4 Regelprüfung**: neu `Packages.Rules.cs`, `PackageForm.Preview.cs`, `CampaignForm.cs`, `Packages.cs`.
- **E5 Vorschau-Verschieben** (sonnet/high): `PackageForm.Strip/Order/Preview`, `Packages.Repeat.cs`.
- **E6 Zoom, Undo/Redo, nur Auswahl**: neu `PackageForm.Undo.cs`, `PackageForm.Strip.cs`, `PackageForm.cs`.
Zum Schluss: `SampleCampaign` auf v7, `config/Testprodukt.apkg` neu, Anleitung (`config/Test-Arbeitsplan-ANLEITUNG.txt`) und `CLAUDE.md` nachziehen.

## Risiken
- Migration kann Schritte falsch einsortieren → im Zweifel Frei, Golden-Test.
- `Normalize` und Wochentage: Selbsttest mit Wochentags-Wiederholung vor Lauf 1.
- Ältere exe lesen Paketformat 7 nur über aufgelöste Attribute; Speichern im gemeinsamen Paketordner verliert Bezüge → alle PCs gleichzeitig aktualisieren.
- RichTextBox: Notiz-Hintergrund vererbt sich beim Tippen; `ExtractSpans` liest eine Eigenschaft mehr (`--bench`); Einfügen bleibt Nur-Text.
- A2 und A3a ändern dieselben Leer-Regeln in `Model.cs`: nacheinander.
- `RunNumbers`: Packen-/Einzel-Nummern dürfen `LastRun`/`PastGaps` nicht verfälschen (Klammerformat + Test). Alte Einträge „Packen 12340001“ zählen weiter.
- Dateigröße: `PackageForm.cs` 459, `Packages.cs` 383 Zeilen → Neues in eigene partial-Dateien.
- FormatVersion: läuft in der Firma schon eine v4-Test-exe ohne Notizen, geht beim Speichern die Notizmarkierung verloren → dann v5.

## Entscheidungen des Nutzers (2026-10-29, Antworten auf die Fragen unten)
- Fragen 1, 3–10 und 11 (Grundsatz): Empfehlung gilt. Frage 7 ergänzt: Puffer von Produkt A und Produkt B nie am selben Tag, i. d. R. mindestens 1 Tag Abstand (Regel in E4: Hinweis/Sperre bei Puffer verschiedener Produkte am selben Tag; bei Kampagnenstart gegen den Plan prüfen).
- Frage 2 **anders**: Fällt ein Lauf aus oder wird er ausgesetzt, fällt auch die PA/Kennung dieses Laufs aus (Folgeläufe rücken nicht nach). Aber nicht vollautomatisch: Die Kennungstabelle extrapoliert (Startwert +1 je geplantem Lauf, ausgefallener Lauf = Lücke auch bei PA) und jede Zelle bleibt von Hand änderbar.
- Frage 11 ergänzt: Mehrere Notizen je Schritt möglich, getrennt per Enter, übersichtlich gestaltet (je Notiz eigene Zeile, kursiv, klar abgesetzt; Notizfeld im Editor mehrzeilig, Enter = nächste Notiz).
- Frage 12: Nichts verteilt, nur lokale Tests (Desktop/Laptop) → Notizen und Baustein-Felder kommen in `Backup.FormatVersion` 4, kein v5.
- Frage 13: Keine Notiz je Lauf im Paket-Editor; gilt eine Notiz nur für einen Lauf, trägt der Nutzer sie manuell im Plan ein. ("Kennungstabelle" = die Tabelle Lauf | Datum | Laufnummer | Kennung in E2.)

## Offene Fragen an den Nutzer (mit Empfehlung) – beantwortet, siehe oben
1. **Ausgefallener Lauf:** zählt er in der Laufanzahl, bleibt sein Termin frei? *Empfehlung:* Anzahl = geplante Läufe inkl. ausgefallener; Termin leer, Folgeläufe bleiben; Nachziehen per Verschieben.
2. **Kennung bei „aussetzen“/Ausfall:** verbraucht der Lauf eine PA-Nummer? *Empfehlung:* nein; nächster stattfindender Lauf bekommt die nächste Nummer; Lücke nur bei der Laufnummer.
3. **Zwei Hauptläufe am selben Tag:** global oder je Raum? Zählen Lauf-Schritte mit Laufnummer als Hauptlauf? *Empfehlung:* je Raum, nur der Hauptlauf-Schritt.
4. **Packen-Nummer:** „Packen (Nr. 12340001)“, Pflicht erst beim Kampagnenstart, zählt nicht für den Laufnummern-Vorschlag? *Empfehlung:* ja.
5. **Pakettypen im Assistenten:** Kampagne / Einzelpaket / Sonderereignis; ersetzt den bisherigen „Sonderlauf“? *Empfehlung:* ja.
6. **„Lauf 1 am“ im Paket speichern** (Vorschlag für Kampagnenstart)? *Empfehlung:* ja, optional.
7. **Vorkampagne:** Packen/Puffer vor Lauf 1 treffen belegte Blöcke. Modus? *Empfehlung:* standardmäßig „anhängen“ mit Hinweis „belegt“.
8. **Notizen bei „Überschreiben“:** behalten oder ersetzen? *Empfehlung:* behalten (ans Ende).
9. **Zugeklappt, Block nur mit Notizen:** kein Hinweis oder grauer Hohlpunkt? *Empfehlung:* grauer Hohlpunkt.
10. **Recht zum Markieren als Notiz**, Sofort-Speichern per Rechtsklick? *Empfehlung:* Recht „Text ändern“, sofort speichern.
11. **Präfix „Notiz:“** aus Paketen entfällt? *Empfehlung:* ja.
12. **FormatVersion:** wurde eine exe mit Datenformat 4 schon verteilt (auch Test in der Firma)? *Empfehlung:* falls ja, Notizen/Baustein-Felder als Version 5.
13. **Notiz nur für einen Lauf** (Spalte in Kennungstabelle)? *Empfehlung:* vorerst nein.

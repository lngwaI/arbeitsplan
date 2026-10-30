# UX-6: Puffer- und Produkt-Dialoge (Entwurf 2026-10-30, architekt opus/high)

Anlass: Nutzer-Feedback zu "Puffer für Produkt", "Pufferpakete – Produkt wählen", Pufferpaket-Dialog und "Produkte & Equipment" (nicht intuitiv, keine Rückmeldung, Umwege über Einstellungen).

Ursache der fehlenden Beispieltanks im Paket-Editor: Ohne Produktcode im Paket hieß der Knopf "Standardpuffer verwenden…" und bot nur den Standardpuffer (ohne Geräte) an; das Pufferpaket eines Produkts war dort nicht wählbar.

## A) Leitlinien (gelten für alle Dialoge)
1. Knöpfe mit konkretem Verb und Zahl ("2 Puffer übernehmen", "Neue Gruppe"), nie "OK" – https://learn.microsoft.com/en-us/windows/win32/uxguide/win-dialog-box
2. Einleitung als ganzer Satz über dem Steuerelement: was passiert, was danach noch geht – ebd. "Instructional text"
3. Mehrfachauswahl nur über Häkchen, nie Strg/Umschalt-Liste – https://learn.microsoft.com/en-us/windows/win32/uxguide/ctrl-list-views
4. Zähler unter Mehrfachlisten ("3 Geräte angehakt") – ebd.
5. Keine leeren Listen ohne Erklärung: Leerzustand sagt, was fehlt, und bietet die Aktion direkt an – https://www.nngroup.com/articles/empty-state-interface-design/
6. Wiedererkennen statt Erinnern: Vorlagen/Produkte/Geräte zur Auswahl zeigen, keine Verweise "siehe Einstellungen → …" – https://www.nngroup.com/articles/ten-usability-heuristics/ (#6)
7. Systemstatus sichtbar: jede Aktion gibt Rückmeldung (Statuszeile), ungespeicherte Änderungen sind erkennbar – ebd. (#1); https://learn.microsoft.com/en-us/windows/win32/uxguide/ctrl-status-bars
8. Viele Befehle: Knopf mit ▾-Menü (Ersatz für Split-Button) – https://learn.microsoft.com/en-us/windows/win32/uxguide/ctrl-command-buttons
9. Progressive Disclosure: Seltenes in Reiter/Menü, Häufiges sichtbar – https://learn.microsoft.com/en-us/windows/win32/uxguide/ctrl-progressive-disclosure-controls
10. Master-Detail: links Liste, rechts Details mit Überschrift des markierten Eintrags – https://learn.microsoft.com/en-us/windows/apps/design/controls/list-details
11. Umsortieren: ▲▼ je Zeile plus Alt+↑/↓, nicht nur Ziehen – https://www.nngroup.com/articles/drag-drop/
12. Gleiche Begriffe überall: Vorlage, Bezeichnung, Name im Plan, Pufferpaket – ten-usability-heuristics (#4)

## B) Entwürfe
- **B1 "Pufferpaket übernehmen"** (ersetzt "Puffer für Produkt"/"Standardpuffer verwenden"): Quelle wählbar ("Pufferpaket von: PRD-1 · Name (3 Puffer)" … "Standardpuffer (ohne Geräte)"), Vorauswahl = Produkt des Pakets; ohne Produktcode Hinweis "Paket hat noch keinen Produktcode – Produkt hier wählen". ListView mit Häkchen (Bezeichnung | Typ | Positionen und Geräte), unbekannte Geräte orange mit ⚠. Zähler, "Alle"/"Keine". Optionen "Bisherige n Puffer ersetzen" / "Hinten anhängen (noch k frei)". Knopf "2 Puffer übernehmen" (ausgegraut bei 0). Leerzustand mit "Pufferpaket für PRD-2 anlegen…". Rückmeldung im Editor mit Strg+Z-Hinweis.
- **B2 "+ Puffer ▾"** im Paket-Editor: Menü "Leerer Puffer (Positionen des Standardpuffers)", "Kopie von Puffer n: …", Überschrift "Vorlage aus Pufferpaket PRD-1:" mit je einer Vorlage, "Andere Vorlage…" (öffnet B1 mit "Hinten anhängen").
- **B3 Produktwahl der Pufferpakete**: ListView (Code | Name | Pufferpaket), "Neues Produkt…" direkt im Dialog (gemeinsamer Code/Name-Dialog `ProductAsk`), Leerzustand statt MessageBox.
- **B4 Pufferpaket-Dialog**: Felder "Bezeichnung (zur Auswahl)", "Name im Plan" (leer = Titel des Schritts), "Typ"; Zeilen mit ▲▼✕ und Alt+↑/↓; Geräteauswahl: erst die für das Produkt erlaubten, dann übrige bekannte Geräte der Position "(nicht für PRD-1 freigegeben)", freie Eingabe; unbekannt/nicht freigegeben orange; "+ Position ▾" mit allen Katalog-Positionen; Statuszeile mit "Zur Liste hinzufügen…".
- **B5 Produkte & Equipment**: Produktliste als ListView (Code | Name | Puffer); Code/Name-Felder unten links entfallen → "Neu…", "Kopieren…", "Bearbeiten…", "Entfernen", "Pufferpaket (n)…"; rechts Überschrift "PRD-1 · Name" und zwei Reiter "Geräte je Position" und "Austauschgruppen (n)" (Häkchenliste, Zähler, Leerzustand, Rückmeldung bei "Neu"); "Standardliste bearbeiten…" öffnet "Equipment & Bausteine" direkt; Statuszeile "Nicht gespeichert: …", Abbrechen fragt bei Änderungen.
- **B6 Puffer im Plan**: Name im Plan = "<Produktcode> - <Name>" (zentral `Package.PlanBufferName`, beim Übernehmen, kein doppelter Präfix, ohne Code unverändert). Kopfzeile "Puffer n: PRD-1 - Name" bleibt, bis das Foto des Nutzers da ist (Umschalten = eine Stelle `BufferItem.HeadText`). Zeilenbeschriftung ("WFI-Tank:") = Kurzform der Position im Katalog.
- **Tagesprobe** (Beispiel des Nutzers "kein mobiler Puffer an einem Werktag → Notiz tägliche WFI-Probe"): schon abgedeckt durch Pflichtpunkt `PROBE` (werktags, durch mobilen Puffer automatisch erfüllt, `Duties.cs`). Der Typ (mobil/stationär) bleibt die Kategorie im Hintergrund.

## C) Datenmodell
- `PufferTemplate.Art` (XML-Attribut `art` an `<Puffer>` im Produktprofil; ältere exe ignorieren es; leer = wie Name). `Backup.FormatVersion` bleibt 5.

## D) Freezetanks
Empfehlung jetzt (ohne Fachregel-Änderung): Datenmodell bleibt (FZ1, FZ2, FZ-E = Rollen im Plantext). In "Produkte & Equipment" erscheinen Positionen derselben Equipment-Gruppe (z. B. "Freezetanks") als ein Eintrag mit einer gemeinsamen Geräteliste; Speichern schreibt sie in alle Positionen der Gruppe. Alternative (eine Position "FZ" mit Anzahl + Ersatz) bräuchte neuen Plantext und Migration → nur nach Entscheidung des Nutzers.
Offen (Fachentscheidung): (a) FZ-E von Lauf n automatisch = FZ1 von Lauf n+1 vorschlagen/prüfen? (b) FZ2 je Lauf optional? (c) erlaubte Tanks für alle drei Rollen gleich?

## Offene Fragen an den Nutzer
1. Kopfzeile mit oder ohne "Puffer 1:"? 2. Reicht die Kurzform der Position als Zeilenbeschriftung? 3. Freezetanks (a)–(c). 4. Produktcode umbenennen: was passiert mit bestehenden Paketen? 5. Gemeinsame Zeilen ("gemeinsam") im Plan an ihrer Stelle oder wie heute am Ende? 6. Austauschgruppe mit < 2 Geräten: Hinweis oder Sperre?

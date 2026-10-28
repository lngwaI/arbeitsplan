# Eigenideen von Claude

Vorschläge, die nicht ausdrücklich bestellt waren. Jede Idee: was, warum, wo abschaltbar/änderbar.

## Änderungs-Hervorhebung (G3)
- Was: Übernimmt die Abfrage Daten anderer PCs, werden die geänderten Blöcke ca. 20 s mit 1-px-Rahmen und blauer Ecke markiert; Statusleiste "Geändert: Di Suite 1 von XXXX" (ab 4 Blöcken "n Blöcke"). Nur Ansicht, nichts wird gespeichert; eigene Speicherungen und der Bearbeitungsmodus bleiben unberührt.
- Warum: Bisher stand nur "Daten von anderem PC übernommen" in der Statusleiste; man sah nicht, *was* sich geändert hat.
- Abschaltbar/änderbar: Dauer `ChangeHighlight.Seconds` in `src/Core/ChangeHighlight.cs` (0 = keine Markierung, nur die Statuszeile); Farbe `Theme.Changed`; Aufruf in `MainForm.Storage.cs` (`NoteChanges`).

## Offene Pflichten (G4)
- Was: Woche → "Offene Pflichten dieser Woche…" (Lesen genügt): Liste offener LKZ, Tagesproben/WFI und Rundgänge der angezeigten KW (optional mit der nächsten), je Eintrag "Springe dorthin"; nichts offen = "Alles erledigt ✓".
- Warum: Prüfleiste und Rundgangsleiste zeigen den Stand nur dort, wo man gerade hinschaut; vor Schichtende/Wochenabschluss fehlt eine Gesamtsicht.
- Abschaltbar/änderbar: Menüpunkt in `MainForm.BuildMenu` (`MainForm.cs`); Logik `src/Core/OpenDuties.cs` (Zustände aus `CheckEval`), Dialog `src/Win/OpenDutiesForm.cs`.

## Puffer-Migration: "Nein" wird gemerkt (G1, Zusatz)
- Was: Die Startfrage "Standard-Pflichtpunkte für Puffer einrichten?" (nur Admins, alter Bestand) erscheint nach "Nein" auf diesem PC nicht mehr (Merker `%APPDATA%\Arbeitsplan\keine-puffer-standards`); der Knopf "Standard Puffer einrichten" in Pflichtpunkte & Feiertage bleibt.
- Warum: Sonst fragt das Programm bei jedem Start erneut.
- Abschaltbar/änderbar: Merker-Datei löschen = wieder fragen; Logik `MainForm.OfferPufferDefaults` (`MainForm.Checks.cs`).

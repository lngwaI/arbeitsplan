# Aufgaben (kurz halten; Details in docs/anforderungen.md, Annahmen in CLAUDE.md)

## Erledigt
- [x] Projektdateien, Build ohne SDK (`build.cmd`, x86, .NET-4.0-API), Subagenten, CLAUDE.md
- [x] Core/UI getrennt; `IDataStore` mit Version/Konflikt
- [x] Neues Layout: KW-Kopf, Datum je Spalte, 10 Standardzeilen, Schichtfelder je Zeile gleich hoch (kein Abschneiden), farbige F/S/N, Kürzel-Markierung
- [x] Blockname und F/S/N nur mit Recht änderbar; Blockhöhe je Zeile einstellbar (Recht)
- [x] Bearbeitungsmodus pro Block (Doppelklick, Notizblock-Symbol), Soft-Lock-Popup, Konfliktabfrage, Speicherfehler ohne Datenverlust
- [x] Nutzer/Rechte/Pakete (Modell + Verwaltungsdialog), Paket-Auto-Markierung
- [x] Equipment: `IEquipmentStatusProvider` + Mock, Regex-Erkennung, Statusfarben (ini)

## Als Nächstes
- [ ] Rechte erzwingen: Text löschen (Inhalt leeren), Format
- [ ] Zeile löschen/verschieben, Reihenfolge der Zeilen ändern (Recht EditNames)
- [ ] Optional: Kürzel-Liste der Nutzer als Auswahl/Vorschlag (bisher frei getippt)
- [ ] Textformat/Farbe pro Block (Schrift, Farbe, fett/kursiv) – RichTextBox ist vorbereitet; Statusfarbe hat Vorrang nur auf Equipment-Nummer
- [ ] Lokale Testdaten-Datei
- [ ] Hintergrund-Nachladen (Timer + Versionsvergleich, ohne Eingabe zu stören)
- [ ] DPI-Awareness (nur wenn XP-sicher: `SetProcessDPIAware` per Vista+-Check)
- [ ] Tastatur-Bedienung prüfen: Tab-Reihenfolge, Bearbeitungsmodus ohne Maus starten

## Nach IT-Klärung
- [ ] Architektur festlegen (Server+SQLite vs. Netzlaufwerk-Datei) → `architekt`
- [ ] Echter Lock-Dienst und Nutzer-DB zentral; Identifikation am System-PC (Kartenleser-Anmeldung)
- [ ] Audit Trail; Schutz gegen Nutzung außerhalb des Netzes
- [ ] Equipment-Provider für ein MES-System; Statuszeitstempel anzeigen (Tooltip)
- [ ] Übernahme alter Pläne (falls gewünscht)
- [ ] Test auf System-PC, normalem PC und XP (falls relevant)

## On hold
- [ ] Android/iOS: Server-HTTP-API + Web-Client (PWA); erst nach Serverentscheidung

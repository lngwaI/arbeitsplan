# Aufgaben (kurz halten; Details in docs/anforderungen.md, Annahmen in CLAUDE.md)

## Erledigt
- [x] Projektdateien, Build ohne SDK (`build.cmd`, x86, .NET-4.0-API), Subagenten, CLAUDE.md
- [x] Core/UI getrennt; `IDataStore` mit Version/Konflikt, Zeilen-IDs, Suche
- [x] Layout: KW-Kopf, Datum je Spalte, 10 Standardzeilen, Inhalts-/Schichtfelder je Zeile in allen Spalten gleich hoch, Schichtfeld fest 3 Zeilen
- [x] Bearbeitungsmodus (Doppelklick), Soft-Lock-Popup, Konfliktabfrage, Speicherfehler ohne Datenverlust
- [x] Blockname/F-S-N nur mit Recht; Blockhöhe je Zeile (Recht); Zeilen sortieren (Recht, nur Admin); Zeile hinzufügen (Recht)
- [x] Nutzer/Rechte/Pakete + Kürzel (Verwaltungsdialog), Paket-Auto-Markierung
- [x] Equipment-Schema H1/H2-T/F/X/P, Provider-Interface + Mock, Statusfarben (ini)
- [x] Schneller Wechsel: selbst gezeichnete Ansicht (~40 ms je Wochenwechsel)
- [x] Suchfeld (Inhalt/Equipment/Kürzel, nicht Raumnamen), Treffer markiert, Enter = nächster Treffer auch wochenübergreifend
- [x] Schriftfarbe im Inhaltstext (Recht Format), eigenes Kürzel grün

## Als Nächstes
- [ ] Rechte erzwingen: Text löschen (Inhalt leeren)
- [ ] Weitere Textformate (fett/kursiv, Schriftart), falls gewünscht
- [ ] Mausrad/Tastatur: Bearbeitungsmodus ohne Maus, Tab-Reihenfolge
- [ ] Lokale Testdaten-Datei
- [ ] Hintergrund-Nachladen (Timer + Versionsvergleich, ohne Eingabe zu stören)
- [ ] DPI-Awareness (nur wenn XP-sicher: `SetProcessDPIAware` per Vista+-Check)
- [ ] Kürzel-Vorschläge aus Nutzer-DB beim Tippen (optional)

## Nach IT-Klärung
- [ ] Architektur festlegen (Server+SQLite vs. Netzlaufwerk-Datei) → `architekt`
- [ ] Echter Lock-Dienst und Nutzer-DB zentral; Identifikation am System-PC (Kartenleser-Anmeldung)
- [ ] Audit Trail; Schutz gegen Nutzung außerhalb des Netzes
- [ ] Equipment-Provider für ein MES-System; Statuszeitstempel anzeigen (Tooltip)
- [ ] Übernahme alter Pläne (falls gewünscht)
- [ ] Test auf System-PC, normalem PC und XP (falls relevant)

## On hold
- [ ] Android/iOS: Server-HTTP-API + Web-Client (PWA); erst nach Serverentscheidung

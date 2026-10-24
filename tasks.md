# Aufgaben (kurz halten; Details in docs/anforderungen.md, Annahmen in CLAUDE.md)

## Erledigt
- [x] Projektdateien, Build ohne SDK (`build.cmd`, x86, .NET-4.0-API), Subagenten, CLAUDE.md
- [x] Core/UI getrennt; `IDataStore` mit Version/Konflikt, Zeilen-IDs, Struktur, Suche
- [x] Selbst gezeichnete Wochenansicht (~40 ms je Wechsel); Layout KW-Kopf, Datum je Spalte, Inhalts-/Schichtfelder je Zeile gleich hoch, Schichtfeld fest 3 Zeilen
- [x] Bearbeitungsmodus, Soft-Lock-Popup, Konfliktabfrage, Speicherfehler ohne Datenverlust
- [x] Blockstruktur nur über Einstellungen (Reihenfolge, Anzahl, Namen, F/S/N, Höhe, LKZ-Feld); 12 Standardzeilen inkl. Lyo
- [x] LKZ-gemessen-Feld mit Haken (grün), Klick speichert sofort
- [x] Zu-/Aufklappen je Zeile (lokal, nicht synchronisiert; leere Zeilen standardmäßig zu)
- [x] Textformat: Schriftart, Größe, fett/kursiv, Farbe (Leiste + Rechtsklick-Menü), Nur-Text-Einfügen
- [x] Suche (Inhalt/Equipment/Kürzel), Kürzel-Markierung, Enter = nächster Treffer auch wochenübergreifend
- [x] Nutzer/Rechte/Pakete (+ Kürzel), neue Rechte Export/ImportPackage/SortRows/EditNames/EditHeight
- [x] Equipment-Schema H1/H2-T/F/X/P, Provider-Interface + Mock, Statusfarben (ini), unbekannt = schwarz
- [x] Export Woche (alle Zeilen): PNG, PDF, Zwischenablage; Pakete (.apkg): Wochen-Export, Paket-Editor (Schritte mit Equipment-Auswahl/Ausweich/Notiz), Übernehmen in 3 Modi

## Als Nächstes
- [ ] Rechte erzwingen: Text löschen (Inhalt leeren)
- [ ] Paket-Editor: Schritt duplizieren/verschieben, Vorlagenordner, Läufe wiederholen (z. B. "alle 3 Tage")
- [ ] PDF: mehrere Seiten statt einer langen Seite (falls gedruckt werden soll)
- [ ] Tastatur-Bedienung: Bearbeitungsmodus ohne Maus, Tab-Reihenfolge, Zeile per Tastatur ein-/ausklappen
- [ ] Lokale Testdaten-Datei
- [ ] Hintergrund-Nachladen (Timer + Versionsvergleich, ohne Eingabe zu stören)
- [ ] DPI-Awareness (nur wenn XP-sicher: `SetProcessDPIAware` per Vista+-Check)
- [ ] Kürzel-Vorschläge aus Nutzer-DB beim Tippen (optional)

## Nach IT-Klärung
- [ ] Architektur festlegen (Server+SQLite vs. Netzlaufwerk-Datei) → `architekt`
- [ ] Echter Lock-Dienst und Nutzer-DB zentral; Identifikation am System-PC (Kartenleser-Anmeldung)
- [ ] Audit Trail; Schutz gegen Nutzung außerhalb des Netzes
- [ ] Equipment-Provider für ein MES-System
- [ ] Übernahme alter Pläne (falls gewünscht)
- [ ] Test auf System-PC, normalem PC und XP (falls relevant)

## On hold
- [ ] Android/iOS: Server-HTTP-API + Web-Client (PWA); erst nach Serverentscheidung

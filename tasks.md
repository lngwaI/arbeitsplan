# Aufgabenliste

## Erledigt
- [x] CLAUDE.md, tasks.md, build.cmd, .gitignore
- [x] Prototyp Wochenansicht: 7 Spalten, Zeilen mit Überschrift/Inhalt/Einteilung, Heute, Pfeile (Tag/Woche), "+ Zeile", lokale Testdaten, 4:3 und 16:9 geprüft

## Prototyp – nächste Schritte
- [ ] Lange Texte: Scrollbalken bzw. Zeilenhöhe wächst mit Inhalt
- [ ] Lokale Speicherung der Testdaten (Datei) zum Ausprobieren
- [ ] Zeile löschen/verschieben
- [ ] Textformat pro Zelle (Schrift, Farbe, fett/kursiv, Hintergrundfarbe)
- [ ] KW-Berechnung an Jahresgrenzen prüfen (ISO 8601)

## Nach IT-Klärung
- [ ] Architektur festlegen (Server-Dienst + SQLite vs. Netzlaufwerk-Datei)
- [ ] Datenmodell mit Version pro Eintrag, Konfliktmeldung, Hintergrund-Nachladen
- [ ] Benutzerverwaltung (+), Einzelrechte, Rechtepaket, Identifikation am System-PC
- [ ] Audit Trail (wer/wann/was)
- [ ] Schutz gegen Nutzung außerhalb des Firmennetzes (Server-Erreichbarkeit/Token)
- [ ] Schnittstelle Equipment-Status (zunächst leer) + Farbdarstellung
- [ ] Übernahme alter Wochenpläne (falls gewünscht)
- [ ] Test auf System-PC (Kartenleser-Anmeldung) und normalem PC, Start vom Server ohne Admin

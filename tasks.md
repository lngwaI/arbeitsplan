# Aufgaben (kurz halten; Details in docs/anforderungen.md)

## Erledigt
- [x] Projektdateien, Build ohne SDK (`build.cmd`, x86, .NET-4.0-API)
- [x] Prototyp Wochenansicht (7 Spalten, Zeilen, Heute, Tag-/Wochenpfeile, + Zeile), 4:3 und 16:9 geprüft
- [x] Core/UI getrennt; `IDataStore` mit Version/Konflikt; `IEquipmentStatus` (leer)
- [x] Subagenten (`.claude/agents/`) + token-effiziente CLAUDE.md

## Prototyp, nächste Schritte
- [ ] Lange Texte (Scrollbalken oder mitwachsende Zeilen)
- [ ] Lokale Testdaten-Datei (nur Prototyp)
- [ ] Textformat/Farbe pro Zelle
- [ ] Zeile löschen/verschieben
- [ ] Konfliktmeldung in UI bei `Save() == false`

## Nach IT-Klärung
- [ ] Architektur festlegen (Server+SQLite vs. Netzlaufwerk-Datei) → `architekt`
- [ ] Hintergrund-Nachladen, Benutzer/Rechte/Rechtepakete, Identifikation am System-PC
- [ ] Audit Trail, Schutz gegen Nutzung außerhalb des Netzes
- [ ] Equipment-Status + Farben
- [ ] Übernahme alter Pläne (falls gewünscht)
- [ ] Test auf System-PC (Kartenleser-Anmeldung), normalem PC und XP (falls relevant)

## On hold
- [ ] Android/iOS: Server-HTTP-API + Web-Client (PWA); erst nach Serverentscheidung

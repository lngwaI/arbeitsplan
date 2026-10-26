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
- [x] Paket-Editor v2: Bausteine (Packen vorgegeben, Lauf, Splitten, Mischen, TFF Flush), Equipment-Positionen mit validierten Geräten, Wiederholen/abwechselnd, Ausweich-Equipment, Platzhalter optional, Vorschau; Katalog einrichten (Recht PackageAdmin)
- [x] Zeilenkopf: Überschrift in jeder Spalte, Pfeil rechts; schlichtes Rechtsklick-Menü in Schichtfeldern; Umlaute/Sonderzeichen überall

- [x] UI-Modernisierung (Theme), (LKZ)-Kennzeichnung, Tastatur Esc/Strg+S/Umschalt+Enter, Release-Build ohne Testschalter, Härtung, PWA-Vorschau `web/`, Doku + Sicherheitsdossier (Branch gmail/ui-modern, gepusht)
- [x] Oberflächenmodelle xp / win7 / win10 (`--theme`), zusammengeführt mit UI-Umbau (Branch integration/alles): win10 = heller Umbau-Look, win7 dezent bläulich, xp = Original; `theme.ini` entfällt

- [x] Gesamtprüfung (Fehler, Sicherheit, Performance, Doku) und Umsetzung in 4 Runden (GMX / Haupt-Account, erledigt, 2026-10-26 18:36)
- [x] FileStore: Hash-Prüfung unter Sperre, Speichern+Protokoll in einem Schreibvorgang, Paket-Übernahme als Batch, Merge nach Konflikt (GMX / Haupt-Account, erledigt, 2026-10-26 18:36)
- [x] Sicherheit: Admins= nur Ersteinrichtung, Test-Build mit eigenem Schlüssel, Rechtevergabe begrenzt, Text-löschen-Umgehung, Nutzerliste per Merge (GMX / Haupt-Account, erledigt, 2026-10-26 18:36)
- [x] Repo bereinigt: nur erfundene Nummern/Kürzel, neutrale Firmenbegriffe, .gitignore für Daten; MainForm in Teildateien (GMX / Haupt-Account, erledigt, 2026-10-26 18:36)

## Als Nächstes
- [ ] Paket-Editor: Schritte per Ziehen umsortieren, Vorlagenordner, Muster über mehrere Schritte wiederholen (z. B. Lauf A + Lauf B im Wechsel)
- [ ] PDF: mehrere Seiten statt einer langen Seite (falls gedruckt werden soll)
- [ ] Tastatur-Bedienung: Bearbeitungsmodus ohne Maus, Tab-Reihenfolge, Zeile per Tastatur ein-/ausklappen
- [ ] DPI-Awareness (nur wenn XP-sicher: `SetProcessDPIAware` per Vista+-Check)
- [ ] Kürzel-Vorschläge aus Nutzer-DB beim Tippen (optional)

## Nach IT-Klärung
- [ ] Architektur festlegen (Server+SQLite vs. Netzlaufwerk-Datei) → `architekt`
- [ ] Echter Lock-Dienst und Nutzer-DB zentral; Identifikation am System-PC (Kartenleser-Anmeldung)
- [ ] Schutz gegen Nutzung außerhalb des Netzes
- [ ] Equipment-Provider für ein MES-System
- [ ] Übernahme alter Pläne (falls gewünscht)
- [ ] Test auf System-PC, normalem PC und XP (falls relevant)
- [ ] exe signieren oder durch IT freigeben (Smart App Control / Application Control blockt unsignierte exe gelegentlich)

## On hold
- [ ] Android/iOS: Server-HTTP-API + Web-Client (PWA); erst nach Serverentscheidung

## Offen aus der Gesamtprüfung (GMX / Haupt-Account, offen, 2026-10-26 18:36)
- [ ] Release-Schlüssel aus dem öffentlichen Repo nehmen und umschlüsseln; .apbak verschlüsseln und signieren (GMX / Haupt-Account, offen, 2026-10-26 18:36)
- [ ] Login mit Domäne/SID statt nur Windows-Nutzername (Nutzer-DB-Migration nötig) (GMX / Haupt-Account, offen, 2026-10-26 18:36)
- [ ] Git-Historie enthält alte echte Werte (bewusst nicht bereinigt, Entscheidung 2026-10-26) (GMX / Haupt-Account, offen, 2026-10-26 18:36)
- [ ] Tests: SaveManyWithAudit, alte eingerückte Datendatei, 200-MB-Grenze, Hintergrund-Timer, Tippen im Editor (GMX / Haupt-Account, offen, 2026-10-26 18:36)
- [ ] Splitten-Blöcke (nur Unterlauf-Zeilen) ohne Laufnummer-Markierung: "(aus <Hauptlauf>)" werten? (GMX / Haupt-Account, offen, 2026-10-26 18:36)
- [ ] Nutzer-Ids nach Löschen nicht wiederverwenden; Katalog: Doppelnamen ablehnen, Verweise beim Umbenennen mitziehen; Datum ohne Jahr am Jahreswechsel; Raum-Prüfung vor Kampagnenstart (GMX / Haupt-Account, offen, 2026-10-26 18:36)
- [ ] Protokoll in Monatsdateien auslagern (wächst unbegrenzt); Tageswechsel am Dauerbetrieb-Rechner (GMX / Haupt-Account, offen, 2026-10-26 18:36)

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

## Nächste Session (Start mit Opus 5.5, bei schwierigen Problemen Opus; Entscheidungen des Nutzers vom 2026-10-26 19:06, GMX / Haupt-Account)
Reihenfolge = Priorität. Entscheidungen stehen jeweils dabei, nicht erneut nachfragen.

- [x] Git-Historie bereinigt: alle Altwerte aus allen 13 Branches ersetzt, Force-Push erledigt, lokales Repo umgestellt; Backup (alter Stand, enthält die Altwerte!) in `../Arbeitsplan-backup.git` nach Prüfung löschen; andere Rechner neu klonen (GMX / Haupt-Account, erledigt, 2026-10-26 19:40). Hinweis: Schlüssel-Array in FileStore.cs steht jetzt als Nullen → Release-Schlüssel neu regeln (nächste Aufgabe).
- [ ] **Keine Geheimnisse im Repo und nie in der exe**: Der Schlüssel zum Verschlüsseln darf weder im öffentlichen Repo noch in der exe stehen, damit eine kopierte exe nichts öffnen kann. Konzept entwerfen (`architekt`): z. B. Schlüsseldatei/Geheimnis nur im produktiven Datenordner mit NTFS-Rechten (nur Berechtigte) bzw. pro Installation erzeugt; exe ohne diese Datei kann die Daten nicht lesen; lokal zum Testen eigener Test-Schlüssel (nicht eingecheckt, .gitignore). Release-Daten müssen einmalig umgeschlüsselt werden; der Release-Build darf nicht vom eingecheckten Schlüssel abhängen. Echte Prozessdaten nur lokal zum Testen bzw. später auf dem Laufwerk, nie in GitHub. (GMX / Haupt-Account, offen, 2026-10-26 19:06)
- [ ] **Pakete und Sicherungen getrennt auf dem Laufwerk**: Eigene Ordner im Datenordner: `Pakete` (gemeinsam; jeder Berechtigte mit Recht ImportPackage kann importieren/übernehmen; ohne Recht keine Übernahme; Paket-Editor speichert dorthin mit passendem Recht) und `sicherung` (getrennt, nur PackageAdmin/Admin). Menü: Paketliste aus dem Ordner statt Dateidialog; .apbak-Sicherungen verschlüsseln und signieren, nicht unverschlüsselt im Datenordner. (GMX / Haupt-Account, offen, 2026-10-26 19:06)
- [ ] **Paket-Editor wirklich benutzerfreundlich machen** (Nutzerwunsch: "echt geil"): Schritte per Ziehen umsortieren, Vorlagenordner, Muster über mehrere Schritte (Lauf A + B im Wechsel), klare Vorschau, Prüfhinweise (Anzahl Kennungen/Unterlaufnummern vs. Wiederholungen; frei eingetippte Wechselgeräte/Ausweich werden geprüft), leere Schritte nicht still verwerfen, Nummernüberlauf (9999) melden. Große Kampagnen haben 2–15 Läufe, daher lohnt sich der Aufwand. (GMX / Haupt-Account, offen, 2026-10-26 19:06)
- [ ] **Unterläufe/Splitten korrekt**: Hauptlauf und Unterläufe (Sublauf je Teilprodukt) müssen überall richtig funktionieren (Plantext "(aus <Hauptlauf>)", Nummernvergabe, Umnummerieren, Folgeläufe, Marker/Tooltip in der Ansicht, Kampagnenstart, Raumwechsel). Aktuell bekommen Blöcke mit nur Unterlauf-Zeilen keine Laufnummer-Markierung (gemeinsamer Parser ignoriert Unterlauf) → so lösen, dass "(aus <Hauptlauf>)" als Laufbezug gewertet wird; Selbsttests dafür ergänzen. Hauptnummer des Unterlaufs aus dem Lauf-Schritt (RunDate.Group) statt aus der eigenen StartNo. (GMX / Haupt-Account, offen, 2026-10-26 19:06)
- [ ] **Datum immer TT.MM.JJJJ (GMP-Vorschrift)**: Im Kampagnenstart-Dialog keine Eingabe ohne Jahr annehmen (sonst Sprung am Jahreswechsel); Vollformat verlangen, Fehlermeldung bei unvollständig; Untergrenze (Tag < 1) warnen. Überall Anzeige/Eingabe im Format TT.MM.JJJJ. (GMX / Haupt-Account, offen, 2026-10-26 19:06)
- [ ] **Raumprüfung vor Kampagnenstart**: Nicht jeder Raum wird in jeder Kampagne benutzt (z. B. Suite 2 selten, nur kurz für Aufwärmen/Filtrieren/Einfrieren) – "Raum nicht belegt" ist daher normal. Prüfen/warnen nur, wenn ein Schritt einen Blocknamen nennt, den es in der Blockstruktur gar nicht gibt (Schritt würde sonst stillschweigend verworfen). (GMX / Haupt-Account, offen, 2026-10-26 19:06)
- [ ] **Blockwechsel mit ungespeicherten Änderungen**: Es ist nur ein Block gleichzeitig offen. Beim Doppelklick auf einen anderen Block mit Änderungen Rückfrage "Änderungen speichern?" (Ja = Standard, per Leertaste/Enter; Nein; Abbrechen), statt still zu speichern oder zu verwerfen. Zusätzlich in BeginEdit die Rechteprüfung des Zielblocks vor dem Beenden des offenen Blocks. (GMX / Haupt-Account, offen, 2026-10-26 19:06)
- [ ] **Strg+Z nach dem Speichern**: Rückgängig darf einen bestätigten (gespeicherten) Stand nicht zurücknehmen; Undo-Puffer beim Speichern/Start der Bearbeitung leeren. Das Geändert-Flag danach korrekt zurücksetzen (bleibt sonst nach Strg+Z/Format-Wechsel gesetzt). (GMX / Haupt-Account, offen, 2026-10-26 19:06)
- [ ] **Tests**: SaveManyWithAudit (Paket-Übernahme über FileStore mit Konflikt im zweiten Durchgang), alte eingerückte Datendatei laden, 200-MB-Entpackgrenze. Abfrage-Timer lokal testen (zwei Programminstanzen auf demselben Testordner, Änderung in A erscheint in B; Netzausfall simulieren) – keine API nötig; der Nutzer erlaubt ausdrücklich "rumtesten". (GMX / Haupt-Account, offen, 2026-10-26 19:06)
- [ ] **Login/Domäne (niedrige Priorität, erst untersuchen)**: Firmendomäne (es gibt mehrere Domänen/Regionen). Kürzel stehen nicht im Verzeichnisdienst, nur in der Nutzerliste (Windows-Login + Kürzel sind zusammen eindeutig). Prüfen, ob DOMÄNE\login oder SID sinnvoll/migrierbar ist; bis dahin bleibt der Windows-Login-Name. Nutzerverwaltung: Eindeutigkeit von Login und Kürzel prüfen. (GMX / Haupt-Account, offen, 2026-10-26 19:06)
- [ ] Protokoll in Monatsdateien auslagern (wächst unbegrenzt); Rechner im Dauerbetrieb: Tageswechsel um Mitternacht erkennen (Systemrechner Vollkonti). (GMX / Haupt-Account, offen, 2026-10-26 19:06)
- [ ] Katalog: Doppelnamen (Kurzform/Baustein) ablehnen, Verweise beim Umbenennen mitziehen. (GMX / Haupt-Account, offen, 2026-10-26 19:06)

## Vom Nutzer entschieden: NICHT ändern
- Nutzer-IDs bleiben wie sie sind (Nutzer wird mit "+" angelegt: Kürzel, Windows-Name, Vor-/Nachname, Rechte; Login und Kürzel sind eindeutig).
- Rechte "Text ändern/löschen": Wer keine Bearbeitungsrechte hat, kommt nicht in den Editor. Wer sie hat, darf frei bearbeiten/löschen/formatieren; Kontrolle über das Änderungsprotokoll. Kein weiteres Verschärfen (Wort-für-Wort-Ersetzen ist akzeptiert).
- Abfrage-Timer: ohne API/Server nur lokal testbar (siehe Tests).

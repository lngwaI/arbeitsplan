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

- [x] **Laufwerk-Start und Mehrplatz** (Nutzerauftrag 2026-10-26 nachts): exe zentral vom Laufwerk, Desktop-Verknüpfung (UNC, OneDrive), Start nur im Firmennetz (Datenordner/Ersteinrichtung/Domäne), unbekannte Nutzer → Meldung, `NurLesenFuer=` für Systemrechner, Dreiwege-Zusammenführung bei gleichzeitiger Änderung, Formatversion, Update-Hinweis, IT-Unterlage. Branch `gmail/laufwerk-mehrplatz`. (G-Mail / 2. Account, erledigt, 2026-10-26 22:10)
- [x] Testanleitung für Laptop und Test vor Ort: `docs/Testanleitung.md` (holen/klonen, bauen, Mehrplatz mit zwei Fenstern, Release-Checks, Checkliste Firma) (G-Mail / 2. Account, erledigt, 2026-10-27 05:20)

## Nächste Schritte (Stand 2026-10-28, nach Nachtschicht; alles bis hier ist in main)
- [x] Plan Teil B: Baustein "Packen (Back-Up)" (`Packages.Backup.cs`, `PackageStep.BackupOf`, Paketformat 6, Regel in Load/Apply/Editor, folgt Packen im Editor und Kampagnenstart) und Teil A: Kampagnen-Assistent `CampaignWizard` (Pakete → "Neue Kampagne…", `PackagePicker.Product` mit freien Codes + Produktfilter); Selbsttests `SelfTestBackup`, Testschalter `--form wizard|wizard3` (Sonnet-Agent, erledigt, 2026-10-28 17:00). Offen: Teil C/D des Plans, Back-Up-Equipment wird nur beim Anlegen aus dem Packen-Ausweich übernommen (danach frei)
- [x] **Equipment-Übersicht (Plan Teil C)**: Tabelle je Geräteart/Gesamtliste (`EquipTableForm`, Link-Felder = Feldname mit "(Link)"), Matrix Art×Art "passt zu" mit Notiz je Paar + Produkt×Gerät (`EquipMatrixForm`), "Passt zu" auf Art-/Gruppenebene (Regelseite `@Art`/`#Gruppe`), Warnung (`EquipMaster.CheckCompat` in `Catalog.CheckText`/`CheckPackageWarn`, Hinweiszeile im Paket-Editor), Serienanlage, Duplizieren, Vorbelegung der ID (Subagent bauer, Label unbekannt, erledigt, 2026-10-28)
- [x] **Manueller Equipment-Status (Plan Teil D)**: Rechtsklick auf erkannte Nummer → "Status setzen" (Recht `Perm.SetStatus`, Produktion/Admin), `ManualStatusProvider` (Store `GetStatuses/SetStatus`, Audit RowId -2), Nummer in Statusfarbe; Übergangslogik `EquipMaster.IsLegal` nur im Hintergrund (ini `Uebergangspruefung=1`, Standard aus, nur Vermerk "!" im Protokoll, keine Oberfläche) (Subagent bauer, erledigt, 2026-10-28)
- [ ] **Datenformat v4 (`Backup.FormatVersion` 4)** vor Verteilung: alte exe lesen v4 nicht mehr (OFFLINE, kein Zurückschreiben) → alle PCs gleichzeitig aktualisieren (Annahme laut Auftrag)
- [ ] Equipment-Übersicht: Pflege-UI für Statusübergänge (erst wenn Regeln je Gerät geklärt), Link-Spalte "zugelassene Produkte" in der Tabelle, "Passt zu" beim Duplizieren mitkopieren (offen)
- [ ] **Überschreiben bei Folgeläufen**: Liegen Läufe derselben Kampagne an Tag x und x+2 und wird beim Übernehmen 'überschreiben' gewählt, soll auch das vorherige (Lauf an x bzw. x+2) mit entfernt/ersetzt werden. Nutzer: 'muss man gucken' → erst Verhalten von `Package.Apply` (Modi, `PastGaps`, `LastRun`) prüfen, dann mit dem Nutzer klären (G-Mail / 2. Account, offen, 2026-10-28)
- [ ] **Textbausteine** zum Vorschreiben (Nutzerwunsch; Umfang noch unklar: wiederkehrende Textblöcke je Zeile/Raum) (offen, 2026-10-28)
- [ ] Rückmeldung des Nutzers zum Absturz 'Klick ins weiße Feld der Prüfhinweise' (Fix in main, Nutzer testet; Ausnahmeprotokoll im Test-Build: %TEMP%rbeitsplan_ausnahme.txt)
- [ ] Entscheiden: `Backup.FormatVersion` erhöhen wegen Puffer-Anzahl `mobil:2` (alte exe liest unbekannte Variante) – erst vor Verteilung an mehrere PCs nötig
- [ ] Paket-Editor v3-Entwurf `docs/paket-editor-v3.md` (Pakettypen, Phasen, Hold-Zeiten): vor dem Bauen gegen den jetzigen Stand abgleichen; offene Fragen stehen im Vault (Projekte/Arbeitsplan.md)
- [ ] Wochenansicht: Tooltip/Hervorhebung fremder Änderungen (20 s) im Betrieb bewerten; Hervorhebung erscheint auch im Export, wenn dieser binnen 20 s gemacht wird
- [ ] Puffer-Standards für bestehende Datenbestände: Admin wird einmal gefragt (`PufferSetup`); alte Kurznamen 'Probe'/'WFI' bleiben
- [ ] Test vor Ort nach `docs/Testanleitung.md` Teil 6 (weiter offen)

## Als Nächstes
- [ ] **Test vor Ort** nach `docs/Testanleitung.md` Teil 6; Ergebnisse (UNC-Pfad, Domäne, Konto der Systemrechner, Wortlaut von Blockiermeldungen, Umbenennen der laufenden exe) hier eintragen (Nutzer, offen, 2026-10-27 05:20)
- [ ] Review-Befunde niedriger Priorität: FormatSpans nach dem Zusammenführen normalisieren; Domänenliste bei gleichzeitiger Admin-Änderung; nach "Vorschlag übernehmen" kann ohne Recht "Text löschen" gekürzt werden (G-Mail / 2. Account, offen, 2026-10-27 05:20)
- [ ] Paket-Editor: Schritte per Ziehen umsortieren, Vorlagenordner, Muster über mehrere Schritte wiederholen (z. B. Lauf A + Lauf B im Wechsel)
- [ ] PDF: mehrere Seiten statt einer langen Seite (falls gedruckt werden soll)
- [ ] Tastatur-Bedienung: Bearbeitungsmodus ohne Maus, Tab-Reihenfolge, Zeile per Tastatur ein-/ausklappen
- [ ] DPI-Awareness (nur wenn XP-sicher: `SetProcessDPIAware` per Vista+-Check)
- [ ] Kürzel-Vorschläge aus Nutzer-DB beim Tippen (optional)

## Nach IT-Klärung
- [ ] Architektur festlegen (Server+SQLite vs. Netzlaufwerk-Datei) → `architekt`
- [ ] Echter Lock-Dienst und Nutzer-DB zentral; Identifikation am System-PC (Kartenleser-Anmeldung)
- [x] Schutz gegen Nutzung außerhalb des Netzes: kein Datenordner → Ende, Ersteinrichtung nur Admins=, Domänenbindung in der Datendatei (G-Mail / 2. Account, erledigt, 2026-10-26 22:10)
- [ ] Equipment-Provider für ein MES-System
- [ ] Übernahme alter Pläne (falls gewünscht)
- [ ] Test auf System-PC, normalem PC und XP (falls relevant)
- [ ] exe signieren oder durch IT freigeben (Smart App Control / Application Control blockt unsignierte exe gelegentlich). Vorbereitet: docs/IT-Freigabe.md, Signatur per `ARBEITSPLAN_SIGNATUR` in make_dist.cmd; Entscheidung der IT offen (G-Mail / 2. Account, 2026-10-26 22:10)

## On hold
- [ ] Android/iOS: Server-HTTP-API + Web-Client (PWA); erst nach Serverentscheidung

## Nächste Session (Start mit Opus 5.5, bei schwierigen Problemen Opus; Entscheidungen des Nutzers vom 2026-10-26 19:06, GMX / Haupt-Account)
Reihenfolge = Priorität. Entscheidungen stehen jeweils dabei, nicht erneut nachfragen.

- [x] Git-Historie bereinigt: alle Altwerte aus allen 13 Branches ersetzt, Force-Push erledigt, lokales Repo umgestellt; Backup (alter Stand, enthält die Altwerte!) in `../Arbeitsplan-backup.git` nach Prüfung löschen; andere Rechner neu klonen (GMX / Haupt-Account, erledigt, 2026-10-26 19:40). Hinweis: Schlüssel-Array in FileStore.cs steht jetzt als Nullen → Release-Schlüssel neu regeln (nächste Aufgabe).
- [x] **Keine Geheimnisse im Repo und nie in der exe**: Schlüssel = versteckte `arbeitsplan.key` im Datenordner (Erststart erzeugt sie), Cache per DPAPI mit Ablauf (`CacheTage`), Selbsttests grün im GitHub-Workflow `Selbsttest` (lokal blockt Smart App Control die exe). Konzept: docs/Schluesselkonzept.md. Offen daraus: Ordner Pakete/sicherung im Datenordner nutzen (nächster Punkt), Schlüssel-Backup-Hinweis im Menü. (GMX / Haupt-Account, erledigt, 2026-10-26 20:40)
- [x] **Pakete und Sicherungen**: Paketordner `<Datenordner>\Pakete` mit Auswahlliste (Dateidialog nur für PackageAdmin als "Andere Datei…"); Sicherung `.apbak` jetzt APBAK2 verschlüsselt mit dem Datenschlüssel (APBAK1 weiter lesbar); Selbsttests grün im Workflow. Hinweis: APBAK2 lässt sich nur mit derselben `arbeitsplan.key` wiederherstellen. (GMX / Haupt-Account, erledigt, 2026-10-26 21:10)
- [ ] **Paket-Editor wirklich benutzerfreundlich machen** (Nutzerwunsch: "echt geil"): Schritte per Ziehen umsortieren, Vorlagenordner, Muster über mehrere Schritte (Lauf A + B im Wechsel), klare Vorschau, Prüfhinweise (Anzahl Kennungen/Unterlaufnummern vs. Wiederholungen; frei eingetippte Wechselgeräte/Ausweich werden geprüft), leere Schritte nicht still verwerfen, Nummernüberlauf (9999) melden. Große Kampagnen haben 2–15 Läufe, daher lohnt sich der Aufwand. (GMX / Haupt-Account, offen, 2026-10-26 19:06)
  - Erledigt: Prüfhinweise (Kennungen/Unterläufe je Lauf, Nummer > 9999, frei eingetippte Wechsel-/Ausweichgeräte), Raum in "Was? Wo?" (Equipment auf 1024x768 sichtbar), Menü Pakete > "Kampagne starten…" direkt ohne Editor, unbekannter Raum stoppt die Übernahme. Offen: Schritte ziehen, Muster Lauf A + B im Wechsel. (G-Mail / 2. Account, teilweise, 2026-10-26 22:10)
- [ ] **Unterläufe/Splitten korrekt**: Hauptlauf und Unterläufe (Sublauf je Teilprodukt) müssen überall richtig funktionieren (Plantext "(aus <Hauptlauf>)", Nummernvergabe, Umnummerieren, Folgeläufe, Marker/Tooltip in der Ansicht, Kampagnenstart, Raumwechsel). Aktuell bekommen Blöcke mit nur Unterlauf-Zeilen keine Laufnummer-Markierung (gemeinsamer Parser ignoriert Unterlauf) → so lösen, dass "(aus <Hauptlauf>)" als Laufbezug gewertet wird; Selbsttests dafür ergänzen. Hauptnummer des Unterlaufs aus dem Lauf-Schritt (RunDate.Group) statt aus der eigenen StartNo. (GMX / Haupt-Account, offen, 2026-10-26 19:06)
- [ ] **Datum immer TT.MM.JJJJ (GMP-Vorschrift)**: Im Kampagnenstart-Dialog keine Eingabe ohne Jahr annehmen (sonst Sprung am Jahreswechsel); Vollformat verlangen, Fehlermeldung bei unvollständig; Untergrenze (Tag < 1) warnen. Überall Anzeige/Eingabe im Format TT.MM.JJJJ. (GMX / Haupt-Account, offen, 2026-10-26 19:06) → Kampagnenstart: Startdatum fest TT.MM.JJJJ, Eingabe nur vollständig, Hinweis vor Startdatum (G-Mail / 2. Account, erledigt, 2026-10-26 22:10)
- [ ] **Raumprüfung vor Kampagnenstart**: Nicht jeder Raum wird in jeder Kampagne benutzt (z. B. Suite 2 selten, nur kurz für Aufwärmen/Filtrieren/Einfrieren) – "Raum nicht belegt" ist daher normal. Prüfen/warnen nur, wenn ein Schritt einen Blocknamen nennt, den es in der Blockstruktur gar nicht gibt (Schritt würde sonst stillschweigend verworfen). (GMX / Haupt-Account, offen, 2026-10-26 19:06) → umgesetzt (DoApply/Ready) (G-Mail / 2. Account, erledigt, 2026-10-26 22:10)
- [ ] **Blockwechsel mit ungespeicherten Änderungen**: Es ist nur ein Block gleichzeitig offen. Beim Doppelklick auf einen anderen Block mit Änderungen Rückfrage "Änderungen speichern?" (Ja = Standard, per Leertaste/Enter; Nein; Abbrechen), statt still zu speichern oder zu verwerfen. Zusätzlich in BeginEdit die Rechteprüfung des Zielblocks vor dem Beenden des offenen Blocks. (GMX / Haupt-Account, offen, 2026-10-26 19:06)
- [ ] **Strg+Z nach dem Speichern**: Rückgängig darf einen bestätigten (gespeicherten) Stand nicht zurücknehmen; Undo-Puffer beim Speichern/Start der Bearbeitung leeren. Das Geändert-Flag danach korrekt zurücksetzen (bleibt sonst nach Strg+Z/Format-Wechsel gesetzt). (GMX / Haupt-Account, offen, 2026-10-26 19:06)
- [ ] **Tests**: SaveManyWithAudit (Paket-Übernahme über FileStore mit Konflikt im zweiten Durchgang), alte eingerückte Datendatei laden, 200-MB-Entpackgrenze. Abfrage-Timer lokal testen (zwei Programminstanzen auf demselben Testordner, Änderung in A erscheint in B; Netzausfall simulieren) – keine API nötig; der Nutzer erlaubt ausdrücklich "rumtesten". (GMX / Haupt-Account, offen, 2026-10-26 19:06) → Lasttest zwei Prozesse im Workflow (40 Durchgänge je Prozess, echte Konflikte, nichts verloren) (G-Mail / 2. Account, teilweise, 2026-10-26 22:10)
- [ ] **Login/Domäne (niedrige Priorität, erst untersuchen)**: Firmendomäne (es gibt mehrere Domänen/Regionen). Kürzel stehen nicht im Verzeichnisdienst, nur in der Nutzerliste (Windows-Login + Kürzel sind zusammen eindeutig). Prüfen, ob DOMÄNE\login oder SID sinnvoll/migrierbar ist; bis dahin bleibt der Windows-Login-Name. Nutzerverwaltung: Eindeutigkeit von Login und Kürzel prüfen. (GMX / Haupt-Account, offen, 2026-10-26 19:06)
- [ ] Protokoll in Monatsdateien auslagern (wächst unbegrenzt); Rechner im Dauerbetrieb: Tageswechsel um Mitternacht erkennen (Systemrechner Vollkonti). (GMX / Haupt-Account, offen, 2026-10-26 19:06)
- [ ] Katalog: Doppelnamen (Kurzform/Baustein) ablehnen, Verweise beim Umbenennen mitziehen. (GMX / Haupt-Account, offen, 2026-10-26 19:06)

## Vom Nutzer entschieden: NICHT ändern
- Nutzer-IDs bleiben wie sie sind (Nutzer wird mit "+" angelegt: Kürzel, Windows-Name, Vor-/Nachname, Rechte; Login und Kürzel sind eindeutig).
- Rechte "Text ändern/löschen": Wer keine Bearbeitungsrechte hat, kommt nicht in den Editor. Wer sie hat, darf frei bearbeiten/löschen/formatieren; Kontrolle über das Änderungsprotokoll. Kein weiteres Verschärfen (Wort-für-Wort-Ersetzen ist akzeptiert).
- Abfrage-Timer: ohne API/Server nur lokal testbar (siehe Tests).
- [x] Equipment-Stammdaten (Klassen, Reihen, Geräte, Verwendbar-mit, Ersatz) + Windows 10 als Minimum, Themes xp/win7 entfernt (G-Mail / 2. Account, fertig, 2026-10-27; CI-Selbsttest steht aus)

## Nachtschicht 2026-10-27 (G-Mail / 2. Account; Sicherung: Tag `sicherung-vor-nachtschicht-2026-10-27`, Branch `sicherung/vor-nachtschicht`; Branch `gmail/nachtschicht`)
- [x] A Statusleiste (Name/Rolle/Kürzel), Titel nur "Arbeitsplan (online)", Verknüpfung erkennen, Datenordner-Adresse bei fehlendem Laufwerk (G-Mail / 2. Account, in Arbeit, 2026-10-27)
- [x] B Blockstruktur entschlacken (FSN-Modus: F/S/N, F/S, Tagesaufgabe), LKZ-Wochenlogik, Puffer-Proben (täglich/WFI wöchentlich)
- [x] C Rundgänge je Kalenderwoche (GMP 1/2, Kühlraum, EHS, WFI-Probenahme)
- [x] D Rollen umbenennen/anlegen/beenden, Rechte je Rolle, Einzelrechte + "Rolle +/-"-Kennzeichnung
- [x] E Equipment je Produkt, Stammdaten-UI verständlicher (Klasse/Reihe/Gerät, Zugehörigkeit Filter→Skid/Tank)
- [x] F Paket-Editor-Ruckeln/Ablauf, Plan als eigene Datei teilen, automatische Sicherung nach Zeitplan
- [x] G Equipment einpflegen: Schnellerfassung (Text/Excel, Schema-Vorschlag), Nicht zugeordnet (Mehrfachzuordnung), Passt zu per Mehrfachauswahl + Vorschlag nach Gruppe, Eigenschaftsfelder je Art verwalten (Sonnet / Agent, erledigt, 2026-10-28 19:45)

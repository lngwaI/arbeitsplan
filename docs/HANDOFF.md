# Handoff

Jeder Eintrag beginnt mit `## JJJJ-MM-TT HH:MM – <Label>`.

## 2026-10-25 01:53 – G-Mail / 2. Account
- Branch `gmail/ui-modern` gepusht (16 Commits): moderner Look (Theme/DayLabel, Schalter `UseModernTheme`), "(LKZ)" im Zeilenkopf, Esc/Strg+S/Umschalt+Enter, Release-Build ohne Testschalter (`build_release.cmd`), Härtung (XML, Fehlerdialog, `Protected`), PWA-Vorschau `web/`, Doku (`docs/Anleitung_Windows.md`, `docs/Sicherheitsdossier.md`), Opus-Review-Korrekturen.
- Branch `gmail/account-kennung` gepusht: nur Account-Kennung (Skript, Hook, CLAUDE.md-Abschnitt).
- Offen: PBKDF2-Feinschliff in `Protected.cs`, dpiAware, Code-Signatur (IT), Export im modernen Look, PWA im Browser prüfen, Test auf Win 11/XP.

## 2026-10-25 11:17 – G-Mail / 2. Account
- `gmail/account-kennung` in `gmail/ui-modern` zusammengeführt: dieser Branch enthält jetzt alles (UI, Release, PWA, Doku, Account-Kennung). Auf dem anderen Rechner: `git checkout gmail/ui-modern`, `git pull`, `build.cmd`.

## 2026-10-26 18:36 – GMX / Haupt-Account
- Gesamtprüfung (3 Reviews) und Umsetzung in 4 Runden auf `main`: Datenverlust-Risiken, Sicherheit, Repo-Bereinigung, Performance/Aufräumen (Commits 2ebb9b0 … c5f56cd).
- Der Schalter `UseModernTheme` (Eintrag vom 2026-10-25) ist ersetzt durch `--theme xp|win7|win10`.
- Offen: siehe tasks.md, Abschnitt "Nächste Session" (Reihenfolge = Priorität). Die Git-Historie enthält noch alte echte Werte; die Bereinigung ist vom Nutzer beauftragt und steht dort an erster Stelle (Mapping nur lokal in docs/historie-mapping.local.md).
- Test-Hinweis: unsignierte exe wird von Application Control zeitweise blockiert; nicht umgehen, IT-Ausnahme/Signatur nötig.

## 2026-10-26 19:40 – GMX / Haupt-Account
- Git-Historie mit filter-repo bereinigt und alle 13 Branches force-gepusht (Commit-IDs neu, andere Rechner neu klonen). Schlüssel-Array in FileStore.cs ist in der Historie Nullen; Release-Schlüssel gehört in die Aufgabe "Keine Geheimnisse im Repo".

## 2026-10-26 20:40 – GMX / Haupt-Account
- Schlüsseldatei-Umbau auf main. Lokal blockt Smart App Control (Status erzwingen, bewusst nicht abgeschaltet) jede frisch gebaute exe; Tests laufen stattdessen im GitHub-Workflow `.github/workflows/selftest.yml` (Push = Build + `--selftest`, Ergebnis grün/rot in Actions).

## 2026-10-26 21:50 – GMX / Haupt-Account
- Verteilung umgestellt: make_dist.cmd baut dist/Arbeitsplan/{Programm,daten}; exe allein lauffähig, Datenordner automatisch; Workflow lädt das Paket als Artefakt Arbeitsplan-Release (GitHub Actions, Login nötig). Pakete-Ordner und APBAK2 sind auf main.

## 2026-10-26 22:15 – G-Mail / 2. Account
- Branch `gmail/laufwerk-mehrplatz` (auf main aufgebaut): Start zentral vom Laufwerk ohne Kopie, Desktop-Verknüpfung (UNC), Start nur im Firmennetz (`Umgebung.Open`: kein Datenordner → Ende, Ersteinrichtung nur `Admins=`, Domänenbindung `UserDb.Domains`), unbekannte Nutzer → Meldung+Ende, `NurLesenFuer=`, Dreiwege-Zusammenführung bei Konflikt (`Conflict.Merge`, `ConflictForm`), Formatversion, Update-Hinweis, Paket-Menü "Kampagne starten…", Prüfhinweise, Datum TT.MM.JJJJ. Doku: `docs/IT-Freigabe.md`, `config/LIESMICH.txt`, `config/Arbeitsplan.ini` (Pilot=0).
- Tests ohne Login: `bash scripts/ci-ergebnis.sh <sha> <branch> <ordner>` (Annotationen + Branch `bilder/<branch>` mit selftest.txt und Testbildern). Lasttest: zwei Prozesse gleichzeitig im Workflow.
- Lokal: `dist\Arbeitsplan` enthält Altlasten früherer Aufbauten (exe/ini/Vorlagen im Hauptordner, alte ini mit Pilot=1 in Programm\); vor dem Verteilen frisch erzeugen (Ordner vorher selbst wegräumen).

## 2026-10-27 05:20 – G-Mail / 2. Account
- `gmail/laufwerk-mehrplatz` ist in `main` (CI grün). Neu: `docs/Testanleitung.md` (Laptop holen/klonen, bauen, Mehrplatz mit zwei Fenstern `--data C:\APTest`, Release-Checks, Checkliste vor Ort).
- Laptop auf denselben Stand: neu klonen (Historie am 26.10. neu geschrieben), danach `git config core.hooksPath .githooks`. Alles Versionierte ist auf GitHub; alle lokalen Branches sind in `main` enthalten.
- Nur lokal auf dem Desktop-PC (bewusst nicht im Repo): `docs/historie-mapping.local.md` (echte Altwerte, nie veröffentlichen), erzeugbare Ordner `bin`, `release`, `dist`, `shots`, `testpakete`; `../Arbeitsplan-backup.git` (alter Stand mit Altwerten, nicht auf den Laptop kopieren). Auf diesem PC war `core.hooksPath` nicht gesetzt (Commits bis 18881dc ohne Account-Zeile); jetzt gesetzt.
- Offen: Test vor Ort (Nutzer), Review-Befunde niedriger Priorität (tasks.md).

## 2026-10-28 14:30 – G-Mail / 2. Account
- Nachtschicht komplett in `main` (a4d6c36, CI grün 101 Zeilen). Inhalt: Statusleiste/Titel, Pflichtpunkte (LKZ je ISO-KW, Puffer Tagesprobe/WFI, mobil/stationär mit Anzahl), Rundgänge je KW, Schichtmodus, Rollen (`Core/Roles.cs`), Equipment je Produkt + Stammdaten-UI, Paket-Editor 3 (freier Produktcode, 8-stellige Laufnummer, Wiederholmodi, Kalender-Ziehen, Ansatz Flasche), Plan-Datei `.applan`, Sicherung nach Zeitplan, Eigenideen (`docs/Eigenideen.md`). Details: Abschnitt "Nachtschicht" in CLAUDE.md.
- Smart App Control ist beim Nutzer AUS: lokal bauen (csc nach %TEMP%, nicht bin\ solange der Nutzer eine Instanz offen hat) und `Arbeitsplan.exe --shot shot.png --selftest st.txt` laufen. Subagenten nie die echte Maus/Tastatur benutzen lassen (Nutzer arbeitet parallel). Gestartete Instanzen immer beenden.
- Sicherung vor der Nachtschicht: Tag `sicherung-vor-nachtschicht-2026-10-27`, Branch `sicherung/vor-nachtschicht`.
- Offen (siehe tasks.md "Nächste Schritte"): Textbausteine, "Überschreiben" bei Folgeläufen derselben Kampagne, Rückmeldung zum Hinweis-Klick-Absturz (Fix drin, Nutzer prüft), Formatversion bei Puffer-Anzahl, `docs/paket-editor-v3.md` (anderer Entwurf, vor dem Bauen gegen main abgleichen).

## 2026-10-29 Nachtlauf – G-Mail / 2. Account
- Branch `gmail/paket-editor-v4`, alles gepusht. Fertig (je Build + Selbsttest grün, Review durch Ersatz-Prüfer, da Agent `pruefer` fehlt): **A1** (Editor-Umbruch wie Ansicht, `WordBreak.cs`, `TextLayout.Hanging`), **A2** (`Entry.HasText`, Leerraum = leer), **A4** (Ausweich ohne Hauptgerät), **C** (Wochenprobe WFI: stationär = erledigt, sonst manuell abhakbar), **B** (`FileStatusProvider`, `docs/Equipment-Status.md`, ini `Statusdatei=`/`StatusdateiMaxAlter=`/`ManuellTage=`), **F** (Menü Woche → Equipment-Liste exportieren, Tag/Woche/Zeitraum), **G** (Soft-Lock: Datum, "Bearbeitung übernehmen" mit Kommentar, `Perm.TakeOver`, Benachrichtigungen als Dateien `<Datenordner>
achrichten\<login>`, Audit RowId -3).
- **D**: Entwurf `docs/paket-editor-v4.md` mit 13 offenen Fragen (mit Empfehlung). E und A3 bewusst NICHT gebaut.
- Annahmen: G nutzt Dateien statt apdb (keine FormatVersion-Änderung); Recht TakeOver für Produktion/Admin; Rechte-Attribut `rechte` 3→4 mit Migration (alte exe verliert TakeOver beim Zurückschreiben, alle PCs gleichzeitig updaten). B: Gerät ohne Eintrag = "nicht gereinigt" (schwarz); Dateizeit-Alter statt Inhaltsalter; CSV `equipstatus.csv` im Datenordner. C: alte Bestände behalten alte Namen der Pflichtpunkte. F: Kampagnen-Umfang fehlt (braucht D). 
- Nicht geprüft: A1 visuell/Lasttest, Prüfleisten-Aussehen (C), G-Dialogablauf von Hand; CI-Lauf nicht abgewartet.
- Offene Fragen: die 13 Fragen in `docs/paket-editor-v4.md` (v. a. 12: wurde Datenformat 4 schon verteilt?). Dazu: Pfad/Format der echten MES-Statusdatei (B); soll G auch die Sperrdatei um Login erweitern (derzeit Zuordnung über Anzeigename)?

## 2026-10-29 Nachtlauf 2 – G-Mail / 2. Account (Wochenlimit erreicht)
- Fertig, gepusht (je Build + Selbsttest 0 FEHLER, Golden 58 Pakete): **A1b** (Editor Zeilenhöhe/Rand/Blockhöhe wie Ansicht), **P** Puffer-Liste (P1 Core `Core/Buffers.cs`, P2 Oberfläche `+ Puffer`, `BufferForm`), **A3** Notizzeilen, **E1a** Zeitmodell (`Schedule()`, Paketformat 7, Migration), **E1b/W/T** Kopf/Wann/Schritt-Tabelle, **E1c** Kampagnenstart, **E2** Kennungstabelle + Materialvorlage, **E3** Puffer-Bausteine/Pakettypen, **E4** Regelprüfung.
- **Noch offen (nicht gebaut):** **V** (Vorschau & Prüfung: Höhe verstellbar, Kalender in Planreihenfolge, Vorschau blättert) – bauer wurde vom Limit abgebrochen, in `PackageForm.Strip.cs` lag nur eine angefangene, ungeprüfte Änderung (als Patch in %TEMP%_wip.patch gesichert, im Arbeitsbaum zurückgesetzt); **E5** (Ziehen/Verschieben, sonnet/high), **E6** (Zoom, Undo/Redo, nur Auswahl), Abschluss-Doku (`SampleCampaign` v7, `config/Testprodukt.apkg`, Anleitung, CLAUDE.md), **F2** (Export Kampagne), **G2** (Sperrdatei Login). B2 bleibt zurückgestellt.
- Neustart: Branch `gmail/paket-editor-v4`, `git pull`, Plan "Plan Folge-Session" in `tasks.md` ab Punkt 2b (V) weiter.
- Nicht geprüft: Prüfer-Agent fehlte (nur Selbsttest + Shots der bauer); Pakettyp-Seite des Assistenten nicht per Shot; Eigen-Kürzel-Hinterlegung im Editor fehlt (A1b); Puffer im Plan ohne Produkt-Merkmal (E4-Regel unsicher).
- Annahmen/Entscheidungen dieses Laufs: siehe `docs/paket-editor-v4.md` (Entscheidungen des Nutzers, Anhang P, Umsetzung E4). Puffer-Name = Schritt-Titel; alte `mobil:2`-Werte migrieren zu namenlosen Puffern; Kennung/PA fällt mit ausgefallenem Lauf aus (überschreibbar); Puffer verschiedener Produkte am selben Tag = Sperre, Vor-/Folgetag = Hinweis.
- Offene Fragen an den Nutzer: (1) Soll `BufferItem` ein Produkt-Feld bekommen, damit die Puffer-Produkt-Regel sicher greift? (2) Eigen-Kürzel-Hinterlegung im Editor (größerer Umbau) gewünscht? (3) Abschnitt-Höhe in V: Merken pro Benutzer in %APPDATA% ok? (4) Einzelpaket-Dialog: leere Fläche unter der Zusammenfassung – gewünschtes Aussehen? (5) Wochentags-Muster: "Lauf 1 am" muss auf einen Muster-Wochentag fallen – Warnung oder automatisch anpassen?
- Seit dem Limit: Nutzer-Mail/Nachrichten nicht gelesen; exe im Ordner `bin\` ist der Stand nach E4 (Build vom letzten Teilpaket).

## 2026-10-29 Nachtlauf 2, Ende – G-Mail / 2. Account
- Nach dem Limit-Reset fertiggestellt und gepusht: **V** (Teiler mit gemerkter Höhe, Mini-Plan-Kalender, blätternde Vorschau), **E5/E6** (Ziehen Folgeläufe/Umschalt, feste Läufe, Zoom, Undo/Redo, nur Auswahl), **Abschluss** (SampleCampaign, Testprodukt.apkg Format 7, Anleitung, CLAUDE.md), **F2** (Export Kampagne, Vorlauf einstellbar), **G2** (Login in `<Sperre>.lock.login`), **D2** (Vorführ-exe, `docs/Demo.md`). Der Commit b796be5 enthielt versehentlich ungeprüfte V-Teile (Strip/StripMouse); der Folgecommit hat V abgeschlossen.
- Offene Punkte/Annahmen aus den Berichten: Maus-Ziehen, Teiler, Pfeiltasten, Rollenwechsel/Demo-Reset nicht von Hand geklickt; Strg+Mausrad-Zoom fehlt; Undo max. 49 Schritte (`MaxUndo`); feste Läufe halten ihren Tag nur im gewählten Schritt; Puffer vor Lauf 1 im Export über Vorlauf (Standard 2 Tage); Pakettyp-Seite des Assistenten nicht per Shot gesehen; `CatalogForm` kann `Baustein.Variant` nicht bearbeiten; Eigen-Kürzel-Hinterlegung im Editor fehlt; Puffer im Plan ohne Produkt-Merkmal; kein unabhängiger Prüfer (Agent `pruefer` fehlt), CI-Lauf nicht abgewartet.
- Fragen an den Nutzer: siehe Eintrag "Wochenlimit" weiter oben (5 Fragen) – weiterhin gültig.

## 2026-10-29 Nachtlauf 2, QS – G-Mail / 2. Account
- Zusätzlich gebaut: **PB** (Puffer: Standard-Anzahl Equipment je Position im Katalog, mehrere Puffer am Tag, Namen, Pufferpaket als Knopf) und **QS** (3 Code-Reviews Core/Hauptansicht/Paket-Editor; Fixes Teil 1+2 umgesetzt, Build, Demo-Build und Selbsttests grün). `st.txt`/`shot.png.txt` sind jetzt gitignored.
- QS bewusst offen: Dateien > 300 Zeilen aufteilen; manuelles Kursiv geht beim Notiz-Aufheben verloren (bräuchte Formatfeld); BufferForm-Radiobuttons ohne Umbruch; Dialog-Warteschlange beim Start; `Backup.FormatVersion` steht auf 4 (Nutzer: nichts verteilt) – vor der ersten Verteilung auf 5 erhöhen, da Puffer/Notizen/Katalogfelder alte exe unbemerkt beschädigen würden; Paketordner: v7-Pakete von alter exe lesbar, aber Bezüge gehen beim Zurückschreiben verloren (alle PCs gleichzeitig aktualisieren).
- Annahmen PB: Katalog-Standard-Anzahl bleibt 1 bis der Admin sie setzt; "Unterpaket" = Pufferpaket (normales .apkg mit nur Puffer-Schritten, Knopf "Pufferpaket ▾"). Frage: stimmt "Unterpaket" so?
- Kein CI-Lauf abgewartet; keine Handtests (Maus, Teiler, Rollenwechsel, Demo-Reset, Pakettyp-Seite).

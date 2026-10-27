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

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

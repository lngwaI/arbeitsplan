# Arbeitsplan – Unterlagen für die IT-Sicherheitsprüfung

Stand: Prototyp, Version 0.9.0.0. Quellcode liegt vollständig bei und ist die Prüfgrundlage.

## Funktion
Anzeige und Eingabe eines Wochenplans (WinForms). Pilot: Daten in einer verschlüsselten Datei im Datenordner (lokal oder Netzlaufwerk), siehe unten. Test-Build: Testdaten im Arbeitsspeicher.

## Technische Eckdaten
- C#, .NET Framework 4.0 (Windows-eigen), x86, eine exe, keine Fremdbibliotheken/NuGet.
- Build reproduzierbar mit `build_release.cmd` (Windows-`csc`, `/optimize+ /debug-`, Manifest `asInvoker`, kein Admin).
- Keine Installation, keine Dienste, keine Autostart-Einträge, keine Registry-Zugriffe.

## Zugriffe
| Bereich | Verhalten |
|---|---|
| Netzwerk | kein Socket/HTTP-Code; Dateizugriff auf den in `Arbeitsplan.ini` eingetragenen Datenordner (auch UNC/Netzlaufwerk, SMB über Windows) |
| Prozesse | keine Aufrufe anderer Programme |
| Dateien lesen | `Arbeitsplan.ini`, `statuscolors.ini` neben der exe (fehlen = Standardwerte); Datenordner: `arbeitsplan.apdb`, `sperren\*.lock`; `%LOCALAPPDATA%\Arbeitsplan\cache.apdb`; Paketdateien `.apkg` nur nach Dialogauswahl |
| Dateien schreiben | Datenordner (Standard `daten\` neben der exe, sonst laut ini; nicht beschreibbar → `%LOCALAPPDATA%\Arbeitsplan\daten`): `arbeitsplan.apdb` (+ `.tmp`, `.bak1..3`, `arbeitsplan.lock`), `sperren\*.lock` (Name des Bearbeiters, 30 min); Sicherung `<Datenordner>\sicherung\*.apbak` (täglich beim Beenden); lokaler Cache `%LOCALAPPDATA%\Arbeitsplan\cache.apdb`; Export/Paket nur auf Dialogauswahl. `NurLesen=1`: keine Schreibzugriffe auf die Daten |
| Benutzer | Windows-Anmeldename; im Release nicht übersteuerbar (Testschalter nur im Test-Build) |

## Eingaben/Härtung
- Paketdatei (XML): DTD verboten, kein externer Resolver, Größenlimit 10 MB; Fehler führen zu einer Meldung, nicht zum Absturz.
- ini-Dateien: fehlerhafte/unlesbare Zeilen werden ignoriert, es gelten Standardwerte.
- Testschalter (`--user`, `--shot`, `--selftest`, …) sind im Release per Compiler-Schalter nicht enthalten (nur im Test-Build `bin\`).

## Datendatei (Pilot, `Core/FileStore.cs`)
- Alle Daten (Pläne mit Format/Version, Blockstruktur, Katalog, Nutzer/Rechte, Änderungsprotokoll) in EINER Datei `arbeitsplan.apdb`: GZip(XML), verschlüsselt mit `Protected` (AES-256-CBC + HMAC-SHA256, PBKDF2 2.000 Runden). Nicht mit Editor/Excel lesbar; jede Veränderung wird erkannt (Datei wird abgelehnt → Meldung, OFFLINE mit letztem lokalen Stand, nur Lesen).
- **Grenze:** Die Passphrase ist im Programm eingebettet (nur verschleiert, im Quellcode nachvollziehbar). Schutz gegen Neugier und versehentliche Änderung, **nicht** gegen gezielten Angriff. Wer Schreibrecht auf den Datenordner hat, kann die Datei löschen/ersetzen (`.bak1..3` und Sicherung helfen). Rechte werden nur im Programm geprüft. Echter Schutz/Audit-Trail im GMP-Sinn erst mit Server/DB der IT.
- Mehrbenutzer: Sperrdatei (exklusiv geöffnet) während jedes Schreibens, vorher neu einlesen, Versionsprüfung je Block, atomares Ersetzen (`File.Replace`), letzte 3 Stände als `.bak1..3`. Abfrage alle `Abfrage` Sekunden (Standard 30). Nicht geeignet: Datenordner per OneDrive-Synchronisierung teilen.
- Sicherungen (`.apbak`) sind gepackt und mit SHA-256 gegen Beschädigung geprüft, aber nicht verschlüsselt (enthalten keine Nutzerliste).
- Benutzer: `Admins=` in der ini erhalten Admin-Rechte; unbekannte Nutzer `Pilot=1` Produktion, sonst Leser; `NurLesen=1` alle Leser.

## Geschützte Einstellungen
`Core/Protected.cs`: AES-256-CBC, Schlüssel aus Passwort (PBKDF2, 100.000 Runden, zufälliges Salz), HMAC-SHA256 (Encrypt-then-MAC, zeitkonstanter Vergleich). Das Passwort steht nicht im Programm. Ohne Passwort ist die Datei weder lesbar noch unbemerkt änderbar. Ergänzend sollten NTFS-Rechte gesetzt werden (nur Admin schreibt).

## Bekannte Grenzen (offen)
- Release: keine Testnutzer, keine Testdaten; Nutzer-DB in der Datendatei (siehe oben), Rechte nur clientseitig geprüft.
- Keine Authentifizierung über den Windows-Namen hinaus; Änderungsprotokoll je Block in der Datendatei (nicht manipulationssicher gegen Personen mit Schreibrecht auf den Ordner).
- `Protected` nutzt `AesManaged`/`HMACSHA256`: bei aktivierter FIPS-Gruppenrichtlinie nicht verfügbar → Programm startet OFFLINE (Meldung). Ggf. auf `AesCryptoServiceProvider` umstellen.
- Programmstart kann durch Application Control/AppLocker/Smart App Control blockiert werden: Freigabe/Signatur durch die IT nötig, nicht umgehen.
- Code-Signatur der exe: durch die IT mit dem Firmenzertifikat.

## Prüfsumme
SHA-256 der `release\Arbeitsplan.exe` ermitteln: `Get-FileHash release\Arbeitsplan.exe -Algorithm SHA256`

## Entstehung
Die Software wurde unter Einsatz eines KI-Assistenten entwickelt und vom Auftraggeber geprüft; der gesamte Quellcode ist einsehbar.

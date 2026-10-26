# Arbeitsplan – Unterlagen für die IT-Sicherheitsprüfung

Stand: Prototyp, Version 0.9.0.0. Quellcode liegt vollständig bei und ist die Prüfgrundlage.

## Funktion
Anzeige und Eingabe eines Wochenplans (WinForms). Prototyp: Daten nur im Arbeitsspeicher, Testdaten.

## Technische Eckdaten
- C#, .NET Framework 4.0 (Windows-eigen), x86, eine exe, keine Fremdbibliotheken/NuGet.
- Build reproduzierbar mit `build_release.cmd` (Windows-`csc`, `/optimize+ /debug-`, Manifest `asInvoker`, kein Admin).
- Keine Installation, keine Dienste, keine Autostart-Einträge, keine Registry-Zugriffe.

## Zugriffe
| Bereich | Verhalten |
|---|---|
| Netzwerk | keine (kein Socket/HTTP-Code im Programm) |
| Prozesse | keine Aufrufe anderer Programme |
| Dateien lesen | optional `statuscolors.ini` neben der exe (Standardwerte im Programm); Paketdateien `.apkg` nur nach Dialogauswahl |
| Dateien schreiben | nur auf Dialogauswahl des Nutzers: PNG/PDF-Export, Paket speichern |
| Benutzer | Windows-Anmeldename; im Release nicht übersteuerbar (Testschalter nur im Test-Build) |

## Eingaben/Härtung
- Paketdatei (XML): DTD verboten, kein externer Resolver, Größenlimit 10 MB; Fehler führen zu einer Meldung, nicht zum Absturz.
- ini-Dateien: fehlerhafte/unlesbare Zeilen werden ignoriert, es gelten Standardwerte.
- Testschalter (`--user`, `--shot`, `--selftest`, …) sind im Release per Compiler-Schalter nicht enthalten (nur im Test-Build `bin\`).

## Geschützte Einstellungen (vorbereitet, noch ohne Verbraucher)
`Core/Protected.cs`: AES-256-CBC, Schlüssel aus Passwort (PBKDF2, 100.000 Runden, zufälliges Salz), HMAC-SHA256 (Encrypt-then-MAC, zeitkonstanter Vergleich). Das Passwort steht nicht im Programm. Ohne Passwort ist die Datei weder lesbar noch unbemerkt änderbar. Ergänzend sollten NTFS-Rechte gesetzt werden (nur Admin schreibt).

## Bekannte Grenzen (offen)
- Release: keine Testnutzer, keine Mock-Sperre; unbekannte Windows-Nutzer sind Leser (nur Ansehen/Export). Nutzer-DB und Rechte zentral folgen nach IT-Klärung.
- Keine Authentifizierung über den Windows-Namen hinaus; kein Audit-Trail; Server/Sperren zentral folgen.
- Code-Signatur der exe: durch die IT mit dem Firmenzertifikat.

## Prüfsumme
SHA-256 der `release\Arbeitsplan.exe` ermitteln: `Get-FileHash release\Arbeitsplan.exe -Algorithm SHA256`

## Entstehung
Die Software wurde unter Einsatz eines KI-Assistenten entwickelt und vom Auftraggeber geprüft; der gesamte Quellcode ist einsehbar.

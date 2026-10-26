# Arbeitsplan – Unterlage für die IT (Freigabe und Betrieb)

Stand 2026-10-27. Kurz: eine einzelne, unsignierte .NET-Framework-exe ohne Installation, die vom Netzlaufwerk gestartet wird und ihre Daten verschlüsselt in einem Ordner auf demselben Laufwerk ablegt. Kein Dienst, kein Server, kein Internet.

## Was das Programm tut
| Punkt | Verhalten |
|---|---|
| Laufzeit | .NET Framework 4.x (in Windows 10/11 enthalten), x86, Windows Forms, eine Datei `Arbeitsplan.exe` |
| Rechte | `asInvoker`, keine Adminrechte, keine Installation, keine Dienste, keine Registry-Änderungen |
| Netzwerk | nur Dateizugriff (SMB) auf den Datenordner; keine Internet- oder sonstigen Verbindungen |
| Anmeldung | Windows-Anmeldename (`Environment.UserName`); Rechte aus der Nutzerliste in der Datendatei |
| Schreibt auf dem Laufwerk | `daten\arbeitsplan.apdb` (+ `.bak1..3`, `.tmp`, `arbeitsplan.lock`), `daten\sperren\*.lock`, `daten\sicherung\*.apbak`, `daten\Pakete\*.apkg` |
| Schreibt lokal | `%LOCALAPPDATA%\Arbeitsplan\cache.apdb` + `cache.key` (Offline-Stand, per DPAPI nur für diesen Nutzer lesbar, verfällt nach 7 Tagen); `%APPDATA%\Arbeitsplan\keine-verknuepfung` (Merker); auf Wunsch `Arbeitsplan.lnk` auf dem Desktop (OneDrive-Desktop) |
| Verschlüsselung | AES-256 + HMAC-SHA256, Schlüssel = Zufallsdatei `daten\arbeitsplan.key` (versteckt); nicht in der exe, nicht im Quellcode |
| Quellcode | öffentlich: https://github.com/lngwaI/arbeitsplan (nur erfundene Testdaten), Build mit dem Windows-eigenen `csc.exe` |

## Ablage auf dem Laufwerk
```
...\Formulierung\Arbeitsplan\
  LIESMICH.txt
  Programm\   Arbeitsplan.exe, Arbeitsplan.ini
  daten\      (versteckt) arbeitsplan.apdb, arbeitsplan.key (versteckt+System), sperren\, Pakete\, sicherung\
```
Mitarbeiter starten `Programm\Arbeitsplan.exe` direkt vom Laufwerk (Verknüpfung auf dem OneDrive-Desktop mit UNC-Ziel, legt das Programm auf Wunsch selbst an). Es wird nichts lokal kopiert.

## Bitte an die IT
1. **Ausführung erlauben** (nur falls App Control for Business/WDAC, AppLocker oder SmartScreen blockieren), eine Variante:
   - **Empfohlen:** exe mit dem Firmen-Codesignatur-Zertifikat signieren und eine Herausgeber-Regel anlegen. `make_dist.cmd` signiert automatisch, wenn die Umgebungsvariable `ARBEITSPLAN_SIGNATUR` den Fingerabdruck (Thumbprint) eines Zertifikats im Benutzerspeicher enthält (`Set-AuthenticodeSignature`, optional Zeitstempel über `ARBEITSPLAN_ZEITSTEMPEL`).
   - Pfad-Regel für `\\<Server>\<Freigabe>\...\Arbeitsplan\Programm\` – nur sinnvoll, wenn Mitarbeiter dort **nicht schreiben** dürfen.
   - Hash-Regel je Version (muss bei jedem Update erneuert werden).
2. **Smart App Control**: ist auf verwalteten Firmengeräten laut Microsoft abgeschaltet; dort gilt App Control for Business. Auf Privat- oder nicht verwalteten Geräten akzeptiert Smart App Control nur Code mit einer Signatur aus dem Microsoft Trusted Root Program oder mit Cloud-Reputation – ein internes Firmenzertifikat reicht dafür nicht. Das Programm soll außerhalb der Firma ohnehin nicht laufen.
3. **Intranet-Zone**: den Dateiserver in die Zone „Lokales Intranet“ aufnehmen (GPO „Liste der Site-zu-Zonenzuweisungen“), sonst erscheint beim Start vom Laufwerk die Sicherheitswarnung „Herausgeber konnte nicht verifiziert werden“.
4. **Ordnerrechte** (Vorschlag; vor der Einführung mit einem Testnutzer prüfen. Gruppe der Mitarbeiter = `AP-Nutzer`, Admins = `AP-Admins`):

   | Ordner | AP-Nutzer | AP-Admins |
   |---|---|---|
   | `Arbeitsplan\`, `Programm\` | Lesen, Ausführen | Ändern |
   | `daten\` (nur dieser Ordner) | Ordner durchsuchen, Attribute lesen, Dateien erstellen, Ordner erstellen, Unterordner und Dateien löschen – **ohne „Ordner auflisten“** | Vollzugriff |
   | `daten\` (nur Dateien) | Lesen, Schreiben, Löschen | Vollzugriff |
   | `daten\sperren\` | Ändern | Vollzugriff |
   | `daten\Pakete\` | Lesen (wer Pakete speichert: Ändern) | Vollzugriff |
   | `daten\sicherung\` | Lesen, Dateien erstellen (ohne Löschen: ältere Sicherungen bleiben dann liegen) | Vollzugriff |

   Ohne „Ordner auflisten“ sehen Mitarbeiter im Explorer keine Dateien in `daten` (das Programm kennt die Namen und arbeitet trotzdem). Löschen/Ersetzen einzelner Dateien braucht das Programm zum atomaren Speichern (`.tmp` → `arbeitsplan.apdb`, `.bak1..3`). Ganz verhindern lässt sich Kopieren/Löschen bei direktem Dateizugriff nicht; dafür bräuchte es einen Server-Dienst mit eigenem Dienstkonto (siehe unten). Schutzschichten heute: Verschlüsselung, Manipulationserkennung, `.bak1..3`, tägliche Sicherung (14 Stück), Änderungsprotokoll je Block.
5. **Schlüssel sichern**: eine Kopie von `daten\arbeitsplan.key` getrennt aufbewahren (z. B. IT-Passworttresor). Ohne sie sind Daten und Sicherungen unlesbar.
6. **Datensicherung**: der Ordner `daten` gehört in die normale Server-Sicherung.

## Bindung an die Firmenumgebung
- Ohne erreichbaren Datenordner startet die exe nicht (Meldung, Ende). Sie legt außerhalb des vorgesehenen, bereits vorhandenen Ordners nie Daten an; die Ersteinrichtung ist nur für in `Arbeitsplan.ini` unter `Admins=` genannte Nutzer und nach Rückfrage möglich.
- Die Datendatei enthält (verschlüsselt) eine Liste erlaubter Windows-Domänen bzw. Rechner; der erste Admin trägt seine Domäne automatisch ein, Admins können sie in der Nutzerverwaltung ändern. Kopierte Daten öffnen sich außerhalb dieser Domänen nicht.
- Nicht angelegte Windows-Nutzer bekommen eine Meldung („Vorgesetzten/Admin fragen“), das Programm endet. Systemrechner/Anzeige-PCs (z. B. Konto „View-Only“) per `NurLesenFuer=` in der ini: nur Lesen, ohne Nutzerkonto.
- Grenze: Eine .NET-exe lässt sich dekompilieren; die Prüfungen schützen gegen versehentliche oder neugierige Nutzung, nicht gegen einen gezielten Angriff mit Dateizugriff. Echter Schutz: Ordnerrechte (oben) bzw. später ein Server.

## Mehrere Nutzer gleichzeitig
- Jedes Speichern läuft unter einer Sperrdatei: neu einlesen → Versionsprüfung je Block → atomar ersetzen. Gleichzeitige Änderungen am selben Block werden zusammengeführt; nur bei derselben Stelle wird gefragt. Nichts wird still überschrieben.
- Andere PCs sehen Änderungen nach höchstens `Abfrage=` Sekunden (Standard 30), beim Blättern sofort.
- Getestet im GitHub-Workflow mit zwei gleichzeitig speichernden Programmen (kein verlorener Speichervorgang).

## Updates
`Programm\Arbeitsplan.exe` umbenennen (z. B. `Arbeitsplan_alt.exe`), neue exe hineinlegen. Laufende Programme zeigen „NEUE VERSION: bitte neu starten“. Ältere Versionen schreiben nie über Daten einer neueren Version (Formatnummer in der Datei).

## Offene Fragen an die IT
- Welche Ausführungssteuerung ist aktiv (App Control for Business, AppLocker, keine)? Gibt es ein Firmen-Codesignatur-Zertifikat?
- Wer setzt die Ordnerrechte (Tabelle oben)? Gibt es eine AD-Gruppe für die Abteilung?
- Wie melden sich Systemrechner an (gemeinsames Konto „View-Only“, Kartenleser)? → Eintrag für `NurLesenFuer=`.
- Gibt es mehrere Domänen/Regionen, deren Nutzer zugreifen sollen? → Liste in der Nutzerverwaltung.
- Später: Server (Dienst mit eigenem Konto, Nutzer ohne direkten Dateizugriff) für Mobilgeräte und stärkeren Schutz?

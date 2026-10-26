# Speicherkonzept (Entwurf, vor IT-Klärung)

Ziel: Programm als portabler Ordner (OneDrive/Desktop/Laufwerk), alle Daten für alle Nutzer und Systemrechner synchron, Historie ≥ 1 Jahr, Datenbank nur für Admin.

## Optionen zentrale Daten
| Option | Pro | Contra |
|---|---|---|
| A SQL Server der IT (`System.Data.SqlClient`, Windows-Konto, nur Stored Procedures) | Rechte wirklich erzwungen, IT sichert | IT muss Instanz stellen, Port nötig |
| B Kleiner Dienst mit HTTP-API | Basis für Web/Mobile | IT muss Host/Betrieb tragen, eigener Servercode |
| C Datei-DB auf Netzlaufwerk (Jet `.mdb` via OleDb; SQLite scheidet aus: Fremdpaket, SMB-Locking) | Prototyp ohne IT | Jeder braucht Schreibrecht → "nur Admin" nicht erzwingbar, Korruption bei Netzabbruch |
| D Vorhandene Fremd-DB | von IT betrieben | Zweck unklar (vermutlich Equipment-Status lesen), Fremd-DLL, in validiertes System nie schreiben → später nur als `IEquipmentStatusProvider` lesend |
| E DB-Datei per OneDrive synchronisieren | – | Konfliktkopien, keine Sperren: **ungeeignet** |

Empfehlung: A, sonst B. Prototyp: Jet-`.mdb` hinter `IDataStore` (gleiches Schema später auf SQL Server, nur Store-Klasse tauschen). Echte Daten erst mit A oder B.

## Datenquelle finden
- `Arbeitsplan.ini` neben der exe (`Datenquelle=…`, `Abfrage=30`); kein fester Pfad im Code (Repo öffentlich).
- Nicht erreichbar: Meldung + Stand von <Zeit> nur lesen, Titel "OFFLINE", Bearbeiten gesperrt.
- Cache/Logs in `%LOCALAPPDATA%\Arbeitsplan\` (nicht neben der exe: OneDrive-Konflikte). Cache = Woche ±4.
- `MinClientVersion` in der DB; zentral abgelegte exe mit Verknüpfungen ist besser als kopierte Ordner.

## Nebenläufigkeit
- Versionsprüfung je Block bleibt; Soft-Locks als DB-Tabelle (30 min).
- Abfrage statt Push: alle 30 s (Systemrechner 60 s) `MAX(Seq)`; bei Änderung sichtbare Woche neu laden; Block im Bearbeitungsmodus wird nie überschrieben.

## Rechte
- Nur Server-Prüfung (A) ist wirklich sicher. Prototyp mit Datei: Nutzer-/Rechtetabelle vom Admin mit RSA signiert (privater Schlüssel nur Admin, öffentlicher in der exe); ungültig ⇒ alle Leser. Plandaten/Protokoll bleiben bei C manipulierbar (im Dossier vermerken).

## Ordner
- Programm: `Arbeitsplan.exe`, `Arbeitsplan.ini`, `statuscolors.ini`, Anleitung, optional `Vorlagen\*.apkg`.
- Daten (nur Admin/IT): DB-Datei, `Sicherung\` (täglich 30 Tage, Monatsstände 1 Jahr).
- Lokal: `cache\`, `log\`.

## Historie
- Append-only `EntryVersion` (Tag, Zeile, Version, Inhalt/Format/Kürzel/LKZ, Login, Zeit, Aktion); ca. 20 MB/Jahr. Wiederherstellen = neue Version.

## Umsetzungsschritte
1. `IDataStore` erweitern (ChangeSeq, History, Pakete, Nutzer, Locks) zuerst im MemoryStore. 2. ini + Offline-Meldung. 3. `JetStore` + `DbLockService`. 4. Abfrage-Timer, Titel OFFLINE. 5. Lokaler Cache. 6. Verlauf-Dialog. 7. RSA-Signatur Nutzer-DB. 8. Dossier/Anleitung nachziehen. Danach `SqlStore`.

## Fragen an IT / Nutzer
1. Wozu dient die Fremd-DLL (Name/Version, welches Makro, Server/Schema, nur lesend)? Wo speichert Excel heute den Plan?
2. Gibt es einen SQL Server bzw. Host für einen kleinen Dienst?
3. Dürfen exe aus OneDrive/Desktop/Laufwerk laufen (AppLocker, Mark-of-the-Web, Signatur)? Was ist "Vertrauensstufe (Whitelist)"?
4. Wer darf den Datenordner ändern (versteckte Freigabe nur für Gruppe)?
5. Systemrechner: eigenes oder gemeinsames Konto, Netz immer da, Autostart?
6. GMP-relevant (Annex 11/Part 11, Aufbewahrung > 1 Jahr)?
7. Sicherungsrhythmus, Wiederherstellung durch wen?
8. Endgültiger Wochenstart?

## Risiken
Datei-DB über SMB (Korruption, keine Zugriffstrennung); Jet abgekündigt; AppLocker kann portablen Ansatz kippen; veraltete Kopien (MinClientVersion); in Fremd/validierte Systeme nie schreiben; Dossier braucht neue Sicherheitsprüfung (Netz-/Dateizugriff).

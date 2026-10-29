# Equipment-Status aus Datei

Weg: MES (oder Export-Skript) schreibt eine CSV, der Arbeitsplan liest sie. Keine Direktverbindung zum MES. Der Status erscheint nie als Text, nur als Farbe der Equipment-Nummer (`statuscolors.ini`). Im Test-Build ohne `--data` bleibt der Mock die Basisquelle.

## Datei
- Standardname `equipstatus.csv` im Datenordner (ini `Statusdatei=`, relativer Name = im Datenordner, sonst voller Pfad). Wird nie vom Programm angelegt oder geschrieben.
- Zeilen: `ID;Status;Zeitstempel;gueltig_bis`, getrennt mit `;`, UTF-8 (BOM ok). Kopfzeile, Leerzeilen und ungültige Zeilen werden übersprungen.
- Zeit: `yyyy-MM-dd HH:mm[:ss]` oder `dd.MM.yyyy HH:mm[:ss]`; `gueltig_bis` darf leer sein. Zeitstempel leer = Dateizeit. Ungültige Zeit = Zeile übersprungen.
- Status (Groß/Klein, Leerzeichen egal): `NichtGereinigt`, `NichtGereinigtOhneTimer`, `Gereinigt`, `Regeneriert`, `Sterilisiert`, `InProzess`, `Freigabe`, `Quarantaene` (auch "Nicht gereinigt", "In Prozess", "Quarantäne"). Unbekannter Status = Zeile übersprungen (Gerät zeigt Standard).
- Mehrere Zeilen je ID: der neueste Zeitstempel gilt.

```
ID;Status;Zeitstempel;gueltig_bis
H1-T-0001;Gereinigt;2026-10-29 06:00;2026-10-31 06:00
H1-T-0002;Sterilisiert;29.10.2026 06:30;
```

## Regeln
- Standard ist "nicht gereinigt" (ohne Ablauf): Gerät ohne Eintrag, Eintrag mit `gueltig_bis` in der Vergangenheit, Datei fehlt/unlesbar, Datei älter als `StatusdateiMaxAlter` (Stunden, Dateizeit, Standard 24) gilt dann für alle Geräte.
- Eintrag ohne `gueltig_bis` läuft nicht ab.
- Von Hand gesetzter Status (Rechtsklick "Status setzen") gilt vor der Datei, solange er höchstens `ManuellTage` (Standard 14) alt ist; danach gilt die Datei.
- Neu laden: höchstens alle 60 s im Hintergrund und nur bei geänderter Dateizeit/-größe; Fehler werden geschluckt (Standardstatus).

## ini (`Arbeitsplan.ini`)
`Statusdatei=equipstatus.csv`, `StatusdateiMaxAlter=24`, `ManuellTage=14`.

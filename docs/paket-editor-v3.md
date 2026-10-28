# Paket-Editor v3 (Einzelpaket und Kampagne) – Entwurf, Stand 2026-10-28

Status: **Entwurf, nicht umgesetzt.** Erst die offenen Fragen unten mit dem Nutzer klären, dann bauen. Erstellt von G-Mail / 2. Account (Architekt-Subagent, Opus).

> **Achtung Abgleich:** Der Entwurf wurde gegen den Stand von `main` vom 27.10. erstellt. Andere Sessions haben parallel an `PackageForm*`, `Packages.cs` (Pflichtpunkte/Rundgänge, Format v3, Equipment je Produkt, `PackageForm.Sizing.cs`) gearbeitet (Branches `gmail/nachtschicht*`). Vor dem Bauen gegen den aktuellen `main` neu prüfen; die Format-Nummer (hier "v4") ggf. anpassen.

## Fachlicher Hintergrund
- Läuft ein Produkt und gibt es mindestens 1 Lauf, ist es eine **Kampagne**. Manche Produkte sind keine Kampagne (z. B. einmal filtrieren und wieder einfrieren) und brauchen ein schlankes **Einzelpaket**.
- Kampagnen brauchen Phasen: Vorbereitung (Rüsten/Packen), Läufe, Zwischenreinigung, Abschluss. Dazu Hold-Zeiten (Dirty-/Clean-Hold) und Wartung/Ersatz von Equipment.

## Ist-Stand (src/Core/Packages.cs)
- Flache Schrittliste, jeder Schritt mit eigenem `Count`/`Interval`; `Schedule()` rät den Hauptlauf (Lauf-Schritt mit meisten Wiederholungen), `Group` ergibt sich aus dem Tag.
- Anzahl Läufe ändern = jeden Schritt einzeln anfassen. Mehrtägige Schritte nur doppelt angelegt. Keine Phasen, keine Hold-Zeiten, kein Pakettyp. Wartung nur als Merker `EquipDevice.Active` ohne Zeitraum.

## Datenmodell
- `Package.Type` = Einzel | Kampagne (XML `typ`). Fehlt `typ`: Lauf-Schritt vorhanden = Kampagne, sonst Einzel.
- Kampagnenkopf im Paket: `RunCount`, `RunInterval`, `FirstRunDay` (`StartNo`, `ProductCode` gibt es schon).
- Je Schritt neu: `Phase` (Vorbereitung | JeLauf | Zwischen | Abschluss | Frei), `Offset` (Tage relativ zum Anker der Phase; Vorbereitung negativ zum ersten Lauf, JeLauf ab Laufbeginn, Zwischen ab Ende Lauf i, Abschluss ab Ende des letzten Laufs), `Duration` (Tage → "Tag 1/2, 2/2"), `RunFilter` (leer = alle, "1,3" oder "jeder 2."), `HoldMaxDays` (Bezug: vorheriger Schritt dieses Laufs).
- Eine zentrale `Package.Expand(schedule)` → Vorkommen (Schritt, Lauf, Tag, Block, Text, Kennung, `Group` explizit). Vorschau, PlanStrip, CampaignForm, `BuildTargets` nutzen nur sie.
- XML v4 abwärtskompatibel: v3 lesen (Hauptlauf liefert Kampagnenkopf; Schritte mit gleicher Anzahl/Abstand ab Hauptlauf-Tag → JeLauf mit Offset, einmalige davor → Vorbereitung, danach → Abschluss, Rest → Frei). Schreiben: alte Attribute `tag`/`anzahl`/`abstand` zusätzlich.

## Editor
- "Neu" → einseitiger Assistent: Kampagne (Produktcode, Läufe, Abstand, Standardraum, Vorlage) | Einzelpaket (Name, Raum, optional "Bezug") | aus Baustein/Sonderereignis.
- Kampagne: Kopf links, Ablaufliste in 4 Gruppen (Vorbereitung einmal / Je Lauf ×N / Zwischen Läufen / Abschluss), Details mit Versatz, Dauer, "nur in Läufen…", Hold; PlanStrip zeigt Läufe als Bänder.
- Einzelpaket: flache Liste ohne Laufnummern/Phasen, Wiederholen bleibt.
- "In Kampagne umwandeln" und zurück (solange keine JeLauf-Schritte).
- Bausteine bekommen Standardphase; neue Standardbausteine (erfundene Texte): Zwischenreinigung, Line Clearance, Probenahme, Kampagnenabschluss.

## Equipment-Stammdaten (siehe EquipMaster)
- Gerät in Wartung im Lauf → rot, Ersatz aus `SubstitutesFor` mit Unterschieds-Notiz anbieten.
- "Abwechselnd": Gerät in Wartung → beim Kampagnenstart nur dieser Lauf auf Ersatz, nach Rückfrage.
- Verwendbar-mit: nicht verknüpfte Geräte im selben Schritt → Hinweis (Helfer in `EquipMaster`).

## Kampagnenstart / Apply / Merge
- `CampaignForm` baut Tabelle aus `Expand`; "Folgende mitverschieben" nimmt Zwischen-Schritte mit; Hold überschritten = orange + Tooltip, keine Sperre; Läufe −/+; Einzelpaket: einfacher Modus ohne Nummernspalte.
- `Apply`/`Merge`/Audit unverändert, nur `BuildTargets` liest aus `Expand`. `PastGaps`/`History` bleiben, Rückfall `RunInterval`.

## Arbeitspakete (bauer)
1. Core allein: `Package.Type`, Phasenfelder, `Expand`, Migration, XML v4 (XML/Migration nach `src/Core/PackageXml.cs`, partial; Packages.cs hat 654 Zeilen). Fertig: Golden-Test v3-`SampleCampaign` → `Expand` = altes `Schedule`/`BuildTargets`.
2. Katalog (parallel zu 1): `Baustein.Phase`, neue Bausteine, `CatalogForm`, Serialisierung (Backup/FileStore).
3. Nach 1, parallel/dateidisjunkt: 3a `PackageWizard.cs`; 3b `CampaignForm.cs`; 3c Prüf-Helfer `EquipMaster.cs` + `PackageForm.Equip.cs`.
4. Nach 1, ein bauer nacheinander: `PackageForm.cs`, `.Details.cs`, `.Parts.cs`.
5. Zum Schluss: Selbsttests, `SampleCampaign` v4, Shots `--form package|campaign2`, dann pruefer.

## Risiken
- Migration alter Pakete kann falsch einsortieren (Gegenmittel: Phase "Frei", Golden-Test). Ältere exe liest v4 nur über doppelte Attribute.
- Hold-Prüfung ist nur Hinweis ("maßgeblich sind SOP/MES"). Plan nur in Tagen, Holds < 24 h nicht darstellbar. PackageForm wächst → Dateien ~300 Zeilen halten.

## Offene Fragen an den Nutzer (alle noch unbeantwortet)
1. **Zwischenreinigung:** nach jedem Lauf, nur jeden n-ten oder nur bei Produktwechsel? Empfehlung: `RunFilter` "jeder n.", Standard jeder.
2. **Hold-Zeiten:** nur warnen oder Verschieben sperren? Tagesgenauigkeit ausreichend? Empfehlung: nur warnen, in Tagen.
3. **Wartung mit Zeitraum (von–bis)** am Gerät, damit der Kampagnenstart Überschneidungen erkennt (Änderung an den Equipment-Stammdaten)? Empfehlung: ja, optional.
4. **Anzahl Läufe:** feste Paketvorgabe oder beim Kampagnenstart änderbar? Empfehlung: Vorgabe im Paket, beim Start änderbar.
5. **Einzelpakete:** Produkt-/Chargenbezug mit Laufnummer nötig? Empfehlung: nur freies Feld "Bezug", zählt nicht als Kampagnenlauf.

Quellen der Recherche: pharmtech.com "Considering changeovers in multiproduct biologics facilities"; biopharminternational.com "Strategizing for rapid changeovers"; ifactoryapp.com Cleaning Validation and Scheduling; gmpsop.com VAL-120 (Muster).

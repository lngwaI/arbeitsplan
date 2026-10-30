# N4 Freeze-Modul – Entwurf (architekt Opus/medium, 2026-10-30)

Status: Entwurf, Bau F1/F2 in der Nachtschicht mit den Empfehlungen unten; F3 (Paket-Editor) nach Rückmeldung.

## Entscheidung
Jeder Block bekommt eine strukturierte Liste `Entry.Freezes` (`List<FreezeItem>`, Muster `Entry.Buffers`). Ein FreezeItem = ein Freeze-Vorgang (Tank-Freeze oder eine ganze Bag-Serie), gespeichert im Block am Tag von Cycle 1. Gespeichert werden nur Eingaben (Starts Cycle 1–3, Dauer, Abstand, Anzahl) und korrigierte tatsächliche Starts; alle anderen Zeiten werden neu berechnet. Folgetage zeigen die Cycles als abgeleitete Fortsetzung (Klick öffnet den Vorgang im Ursprungsblock). Hauptlauf↔Unterlauf: gespeichert wird nur Unterlauf → Hauptlauf (`MainRef`); "gesplittet in …, Tanks …" am Hauptlauf entsteht per Rückwärts-Index (`SplitIndex`, keine Doppelspeicherung).
Verworfen: Cycles je Tag einzeln speichern (mehrere Blöcke pro Korrektur, Konflikte); nur erzeugter Plantext (nicht rückrechenbar); Split-Info zusätzlich am Hauptlauf speichern (läuft auseinander).

## Datenmodell (`Core/Freeze.cs`)
`FreezeItem { Id (f1…), Art Tank/Bag, Anlage, Kreislauf (Tank: GWK-Anlage 1..2, Kreislauf 1..2; Bag: Anlage folgt aus Cycle), Start1to3[] (Bag Cycle 1..3; Tank [0]), DauerMin=360, AbstandMin=420, Cycles=14, Ist{Cycle→Start}, MainRef, SubNo (8 Stellen oder leer), Tanks (Komma-Liste), Note }`.
`FreezeCalc`: Anlage(n) = ((n−1) % 3) + 1; E(n) = Ist[n] falls gesetzt, sonst n ≤ 3: Start[n], sonst E(n−3) + Abstand; Plan-Start P(n) ohne Ist; Verzögerung = E(n) − P(n); Ende = E(n) + Dauer. Korrektur von Cycle n zieht nur die Folge-Cycles derselben Anlage (n+3, n+6, …) nach (Beispiel: C1 12:00, Abstand 7 h → C4 19:00, C7 02:00 Folgetag; C4 real Sa 02:00 → C7 Sa 09:00, C5/C6 unverändert). Ein Cycle gehört zum Kalendertag seines Starts. Zeit = Ortszeit ohne Zone, `yyyy-MM-ddTHH:mm`; Zeitumstellung nicht korrigiert. Prüfungen (nur Hinweise): Abstand < Dauer; Ist-Start vor Ende des vorigen Cycles derselben Anlage.
`RowInfo.Freeze` (bool, XML `freeze="1"`; alte Strukturen: Name enthält "Freeze" → an). `Catalog.FreezeDefaults` (GWK-Anlagen 2, Kreisläufe 2, Freeze-Anlagen 3, Dauer 6 h, Abstand 7 h, Cycles 14).

## Speicherung
XML unter dem Block (Backup.Xml/Read, Plan-Datei, Paket-Blöcke, Audit): `<freeze id="f1" art="bag" dauer="360" abstand="420" cycles="14" haupt="…" unter="…" tanks="…"><s c="1" t="…"/><ist c="4" t="…"/>Notiz</freeze>`. Alte Dateien ohne `<freeze>` = leere Liste. `Backup.FormatVersion` bleibt 5. Achtung: eine exe ohne N4 verliert Freeze-Daten beim Speichern des Blocks → alle PCs gleichzeitig aktualisieren (oder FormatVersion 6).

## Berechnung in Ansicht und Index
`FreezeSet.CyclesOn(entry, day)`; Fortsetzungen: je Tag bis 7 Tage zurück schauen (Konstante); `SplitIndex.Build(store, von−14 T, bis+14 T)` aus `FreezeItem.MainRef/SubNo/Tanks` und den Plantext-Zeilen "Unterlauf X (aus Y)" (wie `RunNumbers`). Am Hauptlauf-Block abgeleitete graue Zeile "gesplittet in X1, X2 · Tanks …".

## Eingabe (Dialog "Freeze planen", `Win/FreezeForm.cs`)
Öffnen: Freeze-Zeile "Freeze hinzufügen…" oder Klick auf Vorgang/Fortsetzung. Felder: Art; Tank: GWK-Anlage, Kreislauf, Start, Dauer; Bag: Start C1–C3, Dauer, "Zeit zwischen zwei Cycles (je Anlage)", Anzahl Cycles; Hauptlauf (Vorschlag aus Läufen im Plan ±3 Tage), Unterlauf-Nr., Tanks, Notiz. Tabelle: Cycle | Anlage | geplant | tatsächlicher Start (editierbar, leer = berechnet) | Ende | Verzögerung; Folge-Cycles derselben Anlage ändern sich sofort; Farbe nach Anlage, Verzögerung orange "+5 h", "Ist zurücksetzen". Speichern sofort über den `Change(...)`-Weg (auch ohne Bearbeitungsmodus, Konfliktabfrage wie sonst).

## Anzeige im Plan (`PlanView.Freeze.cs`)
Kopf: Bag "Bag-Freeze · Unterlauf X (aus Y) · Tanks …"; Tank "Tank-Freeze · GWK1 K2 · 12:00–18:00 · Tanks …". Cycles kompakt je Anlage: "C1 · FA1 · 12:00   C4 · FA1 · 19:00", Verzögerung orange "(+5 h)", Fortsetzungen kursiv grau "(aus Mo)". Suche über `FreezeSet.Matches`; Equipment-Export zählt Tanks; ein Vorgang zählt als Inhalt (`IsBlank`).

## Merge/Konflikt/Protokoll/Rechte
`FreezeSet.Merge` dreiwege je Id; `Ist` je Cycle zusammenführen (verschiedene Cycles still); gleicher Cycle mit verschiedenem Wert oder Parameter beidseitig verschieden → Konflikt "Freeze" (gespeicherte Fassung bleibt, keine Kopie als zweite Serie). Audit: `DisplayLines`, z. B. "C4 Ist 02:00 (+7 h)". Rechte (`FreezeSet.Diff`): Neu = AddText, Ändern inkl. Ist = EditText, Entfernen = DeleteText. Paket-Übernahme: Block mit Freezes gilt als belegt.

## Bauschritte
F1 Core (`Freeze.cs`, `Freeze.Set.cs`, `Splits.cs`, Model/Backup/Conflict/Audit/Find/EquipExport/Apply, Testdaten, Selbsttest `SelfTestFreeze`) → F2 Win (`FreezeForm(.Grid).cs`, `PlanView.Freeze.cs`, `MainForm.Freeze.cs`, `--form freeze`, Anleitung) → F3 Paket (optional: Baustein `FreezeArt`, `PackageStep.Freeze` Vorlage, Apply setzt MainRef/SubNo/Start; Paketformat 7 nur zusätzliche Attribute).

## Empfehlungen zu den offenen Fragen (Bau läuft mit diesen Annahmen)
1. Cycle 1 Pflicht; leere Starts C2/C3 = C1 + 1 h bzw. + 2 h, als "geschätzt" markiert.
2. Cycles je Vorgang 1–14 (Standard 14), ein gemeinsamer Abstand für alle Anlagen.
3. Thaw vorerst als normaler Text; F3 (Paket) erst nach Rückmeldung.
4. Tank-Freeze mit Start + Dauer (Standard 6 h, editierbar); FormatVersion bleibt 5 (nichts verteilt).

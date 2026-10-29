# Vorführ-Version (Demo)

Eigene exe mit **erfundenen Testdaten** für Vorführungen. Hat nichts mit den echten Daten zu tun und wird **nicht ausgeliefert**.

## Bauen und starten
- `build_demo.cmd` → `bin_demo\ArbeitsplanDemo.exe` (csc ohne SDK, Defines `TESTHOOKS;DEMO`; Ordner `bin_demo\` ist gitignored).
  Nicht in `make_dist.cmd`, den Release-Workflow oder `scripts\test-arbeitsplan.cmd` aufnehmen.
- `scripts\demo-vorfuehrung.cmd [admin|produktion|leser]` baut und startet (ohne Angabe: Startdialog).
- Start fragt: **Rolle vorführen** (Leser / Produktion / Admin / Schichtleitung). Kein Passwort, kein Windows-Login.
  Wechsel im Betrieb: Hilfe → Rolle wechseln. Zwei Fenster mit verschiedenen Rollen sind möglich (Sperren, Benachrichtigungen, Übernahme wie echt).
- Orangefarbenes Banner + Titel/Statusleiste "VORFÜHRUNG – Testdaten".

## Zurücksetzen
- Hilfe → Demo zurücksetzen (mit Rückfrage; vorher alle anderen Demo-Fenster schließen): Datenordner und Sicherungen werden gelöscht und neu erzeugt, die Demo startet mit der gleichen Rolle neu.
- Die Ausgangsdaten gelten für die **Woche des Erzeugens** (Plan relativ zu "heute"). Beim Start einer späteren Kalenderwoche (kein anderes Demo-Fenster offen) werden sie automatisch neu erzeugt.
- Inhalt (`Core/DemoData.cs`): Plan aus `MemoryStore.Sample` (Puffer, Notizen, Rundgang) plus zwei Kampagnendurchläufe (`Package.SampleCampaign`: früher Läufe 1-4, in zwei Wochen Läufe 5-8), eigener Equipment-Satz (Geräte, Gruppe, Ersatz, Gerät in Wartung, Produktprofil 1234), manuelle Gerätestatus, zwei Pakete im Paketordner, vier Nutzer (mmuster Admin, ebeispiel Leser, pwerk Produktion, sleiter Rolle "Schichtleitung").

## Trennung von den echten Daten
- Datenordner `%LOCALAPPDATA%\ArbeitsplanDemo\daten` (Daten, Schlüssel, Sperren, Nachrichten, Pakete), Sicherungen `…\ArbeitsplanDemo\sicherung`, Merker und Einstellungen unter `%APPDATA%\ArbeitsplanDemo`. Nie `Arbeitsplan.ini`, Offline-Cache, `%APPDATA%\Arbeitsplan`, Verknüpfung, Domänenbindung (`Umgebung.*`) oder Windows-Nutzererkennung.
- Schutz (`DemoEnv.CheckFolder`): geöffnet wird nur ein Ordner unterhalb eines Ordners namens `ArbeitsplanDemo`, und nur wenn er leer ist oder die Markierung `demo.marker` enthält. `--data` gilt in der Demo nur dafür; Lasttest (`--stress`) ist abgeschaltet. Sicherungsordner außerhalb des Demo-Pfads werden ignoriert.
- Umgekehrt: die normale exe darf einen Demo-Ordner nie als Produktivordner verwenden (nie `Datenordner=` auf `…\ArbeitsplanDemo\…` setzen).
- Testläufe (`--shot`/`--selftest`) nutzen `%TEMP%\ArbeitsplanDemo\Test` (jedes Mal neu), nie die Vorführdaten.

## Test
`bin_demo\ArbeitsplanDemo.exe --shot demo.png --selftest demo.txt` → `SelfTestDemo` (Pfade, Produktivordner abgelehnt, Ausgangsdaten, Zurücksetzen, Rollen/Rechte). Startdialog als Bild: `--form demorole`. Rolle ohne Dialog: `--role leser|produktion|admin|r1`.

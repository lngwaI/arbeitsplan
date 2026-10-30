# Beispiel-Umgebung (nur erfundene Daten)

Zweck: ein Ordner zum Kopieren, der zeigt, wie der Arbeitsplan im Betrieb aufgesetzt ist: eine exe, die nur auf `daten\` daneben zugreift (Pakete, Statusdatei, Equipment-Liste, Sicherung).

## Bauen und starten
1. `scripts\beispiel-bauen.cmd` im Repo ausführen: baut die Release-exe und erzeugt `Beispiel-Arbeitsplan\` (gitignored, wird jedes Mal neu erzeugt, startet nichts).
2. Ordner `Beispiel-Arbeitsplan\` an einen beliebigen Platz kopieren (nicht ins Firmennetz-Produktiv-Laufwerk).
3. `Programm\Arbeitsplan.exe` starten. Erster Start: Rückfrage zur Einrichtung bestätigen (legt `arbeitsplan.apdb` und versteckte `arbeitsplan.key` in `daten\` an), du bist Admin.
4. Ausgangsplan laden: Einstellungen → Sicherung… → die Sicherung `…Beispiel-Ausgangsstand` wiederherstellen (Plan der nächsten Woche mit Kampagne). Ohne diesen Schritt ist der Plan leer.

## Was liegt wo
- `Programm\Arbeitsplan.exe`, `Arbeitsplan.ini` (nur relative Pfade, `Admins=` = Windows-Name des Erstellers, `Pilot=1`, `CacheTage=0`).
- `daten\Pakete\*.apkg`: Kampagne mit Hauptlauf/Packen/Puffer, ein Pufferpaket, ein Sonderereignis (Pakete → Übernehmen).
- `daten\equipstatus.csv`: Gerätestatus (Format `docs/Equipment-Status.md`); `daten\Equipment-Liste.txt`: lesbare Geräteübersicht.
- `daten\sicherung\`: Ausgangsstand (`.apbak`), später auch die Sicherungen des Programms.
- Quellen im Repo: `beispiel\` (ini, Pakete, CSV, Liste); der Ausgangsplan wird beim Bauen mit `bin\Arbeitsplan.exe --makebeispiel` erzeugt (relativ zu heute).

## Annahmen und Grenzen
- Die Release-exe schreibt außerhalb des Ordners nur Komfort-Merker unter `%APPDATA%\Arbeitsplan` (Verknüpfungsfrage, Pufferstandards-Frage, zuletzt gewählter Datenordner); keine Datenänderung dort. Beim Start vom Netz fragt sie nach einer Desktop-Verknüpfung: "Nein".
- Auf einem Domänen-PC bindet die Ersteinrichtung die Daten an diese Domäne; auf einem PC einer anderen Domäne startet die Kopie nicht ("nicht freigegeben"). Dann `daten\` leeren (ohne `Pakete`) und neu einrichten.
- Die Beispieldaten (Equipment-Katalog) sind die Standardwerte der Test-exe, nicht die Demo-Stammdaten.

## Demo-exe oder Beispiel-Umgebung?
| | Vorführ-Version (`build_demo.cmd`, `docs/Demo.md`) | Beispiel-Umgebung |
|---|---|---|
| exe | `ArbeitsplanDemo.exe`, nie ausliefern | echte Release-exe |
| Daten | `%LOCALAPPDATA%\ArbeitsplanDemo`, fest geschützt | Ordner neben der exe, kopierbar |
| Nutzer | Rollenwahl, vier Demo-Nutzer | dein Windows-Name als Admin |
| Zweck | Vorführen ohne Risiko | Aufbau/Verteilung der echten Version zeigen |
Im Zweifel die Demo-exe: sie kann nie auf echte Daten zeigen.

# Arbeitsplan – auf dem Laptop holen, bauen, testen

Stand 2026-10-27 (Version 0.10). Ziel: auf dem Laptop alles prüfen, was ohne Firmennetz geht, dann vor Ort (Teil 6).

## 1. Projekt holen
Befehle in PowerShell; Programme aus dem Projektordner mit `.\` davor starten. Die Git-Historie wurde am 26.10.2026 abends bereinigt und neu geschrieben. Ein älterer Stand auf dem Laptop lässt sich deshalb nicht einfach mit `git pull` aktualisieren.

**A) Neu klonen** (empfohlen, sicher):
1. Den alten Projektordner umbenennen, z. B. `Arbeitsplan` → `Arbeitsplan_alt`. Nichts löschen.
2. Im übergeordneten Ordner, z. B. `...\VS Code\Boehringer`, klonen. Den gleichen Ordnernamen wie vorher nehmen, damit Claude Code seine Projekt-Notizen wiederfindet.
   ```
   git clone https://github.com/lngwaI/arbeitsplan.git Arbeitsplan
   cd Arbeitsplan
   git config core.hooksPath .githooks
   ```
3. Bei Bedarf `.claude\settings.local.json` aus dem alten Ordner herüberkopieren (lokale Freigaben für Claude, nicht im Repo).

**B) Vorhandenen Ordner angleichen.** Verwirft alle lokalen Änderungen im Ordner; die nicht versionierten `bin`, `dist` und `testpakete` bleiben:
```
cd "<Projektordner>"
git fetch origin
git checkout -f main
git reset --hard origin/main
```
Ist der Laptop schon auf dem Stand vom 26.10. abends oder neuer, reicht `git checkout main` und `git pull`.

**Prüfen:** `git log --oneline -1` zeigt „Testanleitung …“ oder einen neueren Commit.

## 2. Vorher: Smart App Control
Windows-Sicherheit → App- & Browsersteuerung → Smart App Control.
- **Ein**: Windows blockiert die selbst gebaute, unsignierte exe (wie am Desktop-PC). Bauen geht trotzdem, starten nicht. Die Tests laufen dann nur in GitHub (Teil 5) und vor Ort (Teil 6). Ob du die Einstellung änderst, entscheidest du; das Programm braucht es nicht.
- **Aus** oder **Evaluierung**: die exe startet, weiter mit Teil 3.

## 3. Bauen
Im Projektordner (`.\` in PowerShell):
```
.\build.cmd
```
Ergebnis: `bin\Arbeitsplan.exe`, der Test-Build mit Testschaltern.
```
.\make_dist.cmd
```
Ergebnis: `dist\Arbeitsplan\`, das Release so wie fürs Laufwerk.

**Wichtig:** `make_dist.cmd` behält eine vorhandene `Arbeitsplan.ini` und den Ordner `daten`. Gibt es `dist\Arbeitsplan` schon von früher (alte ini mit `Pilot=1`), den Ordner vorher umbenennen, z. B. in `Arbeitsplan_alt`.

## 4. Testen auf dem Laptop

### A) Testdaten, 5 Minuten
`.\bin\Arbeitsplan.exe --user mmuster` (Admin, Testdaten nur im Speicher)
- Woche und Tag wechseln, Zeile auf- und zuklappen, Doppelklick → bearbeiten, Strg+S speichert, Suche mit Strg+F.
- Pakete → Neues Paket anlegen… : Bausteine per Ziehen umsortieren. Unter „Vorschau & Prüfung“ stehen die Prüfhinweise (Klick springt zur Stelle).

### B) Mehrplatz: zwei Fenster wie zwei PCs
Zwei Fenster mit verschiedenen Nutzern auf derselben verschlüsselten Datendatei.
1. Fenster 1 starten: `.\bin\Arbeitsplan.exe --data C:\APTest --user mmuster`. Die Frage „Neuen Arbeitsplan einrichten?“ mit Ja beantworten.
2. Einstellungen → Nutzerverwaltung → „+“: Login `pwerk`, Kürzel `WEPA`, Paket Produktion. Schließen.
3. Testpaket anlegen: `.\bin\Arbeitsplan.exe --makepkg C:\APTest\Pakete\Testprodukt.apkg`.
4. Fenster 2 starten: `.\bin\Arbeitsplan.exe --data C:\APTest --user pwerk`.

Prüfen:

| Was tun | Erwartet |
|---|---|
| Fenster 1: Block ändern, Strg+S | Erscheint in Fenster 2 nach höchstens etwa 30 s; per „Heute“ oder Pfeil sofort. |
| Beide öffnen denselben Block | Popup „… wird gerade von … bearbeitet“; trotzdem bearbeiten geht. |
| Fenster 1 ändert Zeile 1, Fenster 2 Zeile 3, beide speichern | Beide Änderungen sind da, keine Rückfrage. |
| Beide ändern dieselbe Zeile, beide speichern | Der zweite bekommt „Gleichzeitige Änderung“ mit drei Fassungen. Nach „Vorschlag übernehmen“ bleibt der Block im Bearbeitungsmodus: prüfen, Strg+S. |
| Rechtsklick auf den Block → Verlauf… | Beide Fassungen mit Name und Uhrzeit. |
| Fenster 1: Pakete → Kampagne starten… → Testprodukt | Datum als TT.MM.JJJJ eingeben, Läufe erscheinen im Plan und in Fenster 2. |
| `.\bin\Arbeitsplan.exe --data C:\APTest --user ebeispiel` | Meldung „noch nicht als Nutzer angelegt“, das Programm endet. Nach dem Anlegen als Leser: nur ansehen. |
| Mit `--nosave` (z. B. bei pwerk) | Nur ansehen, kein Speichern (wie Systemrechner). |

Danach `C:\APTest` löschen oder für den nächsten Test behalten.

### C) Release wie auf dem Laufwerk
Die Reihenfolge einhalten: Schritte 1 und 2 nur vor der Einrichtung.
1. **Kopie-Test:** `dist\Arbeitsplan\Programm\Arbeitsplan.exe` allein auf den Desktop kopieren und starten. Erwartet: „Daten nicht erreichbar … nur im Firmennetz“. Das Programm endet und legt nichts an.
2. `dist\Arbeitsplan\Programm\Arbeitsplan.exe` starten, `Admins=` ist noch leer. Erwartet: „noch nicht eingerichtet“.
3. Den eigenen Windows-Anmeldenamen anzeigen: `$env:USERNAME`. In `dist\Arbeitsplan\Programm\Arbeitsplan.ini` eintragen: `Admins=<Name>`. Erneut starten → „Neuen Arbeitsplan einrichten?“ → Ja. Der Ordner `daten` wird versteckt.
4. Hilfe → Info… zeigt Version, Datenordner, Nutzer und Domäne. Auf einem privaten Laptop gibt es keine Domänenbindung, das ist richtig so.
5. Hilfe → „Verknüpfung prüfen / anlegen“ (listet vorhandene Verknüpfungen; keine auf dieses Programm → anlegen mit Ja). Auf dem Laptop kommt der Hinweis „liegt nicht auf dem Laufwerk“; vor Ort zeigt sie auf `\\Server\…`.
6. Die Kopie auf dem Desktop noch einmal starten. Jetzt zeigt sie „OFFLINE“ mit dem letzten Stand dieses PCs: nur Lesen, höchstens `CacheTage` (7) Tage. Das ist für Laptops ohne Netz gewollt; auf einem fremden PC (z. B. zu Hause) startet sie nicht. Danach die Kopie löschen.

## 5. Automatische Tests (GitHub)
Bei jedem Push baut GitHub beide exe und führt alle Selbsttests aus, auch den Lasttest mit zwei Prozessen und die Testbilder.
- github.com/lngwaI/arbeitsplan → Actions → Selbsttest: grüner Haken = alle Pflichtzeilen ohne FEHLER.
- Das Artefakt „Arbeitsplan-Release“ ist der fertige Ordner (unsigniert; eine heruntergeladene exe kann SmartScreen auslösen).
- Testbilder liegen im Branch `bilder/main`.

## 6. Vor Ort (Firma)
1. **Vorher fragen:** Vorgesetzten bzw. IT, ob die unsignierte exe getestet werden darf (GMP; Systemrechner erst nach Freigabe). `docs/IT-Freigabe.md` mitgeben.
2. Einen eigenen **Testordner** nehmen, nicht den echten, z. B. `…\Formulierung\Arbeitsplan_Test\`. Den Inhalt eines frischen `dist\Arbeitsplan` dorthin bringen (den Weg mit der IT klären).
3. Den Arbeits-Login mit `whoami` anzeigen (`DOMÄNE\name`) und nur `name` bei `Admins=` eintragen. Starten, einrichten, Hilfe → Info notieren.
4. Zweiter PC: aus dem Ordner starten → Verknüpfung anlegen → mit einem anderen PC prüfen (OneDrive-Desktop).
5. Einen Kollegen anlegen und gleichzeitig arbeiten: verschiedene Zeilen, dann dieselbe Zeile (wie Test B).
6. Nicht angelegter Kollege startet → Meldung „Vorgesetzten/Admin fragen“.
7. Systemrechner (erst nach Freigabe): Konto bei `NurLesenFuer=` eintragen → nur ansehen.
8. Update: die laufende `Arbeitsplan.exe` umbenennen und eine neue hineinlegen. Erwartet: offene Fenster zeigen „NEUE VERSION“. Klappt das Umbenennen nicht, notieren.
9. Die exe allein auf C: kopieren und starten. Erwartet: nur „OFFLINE“ mit dem letzten Stand (nur Lesen, nie speichern). Bei einem Nutzer, der den Arbeitsplan auf diesem PC noch nie geöffnet hat, startet sie gar nicht.

**Notieren für Claude/IT:** UNC-Pfad des Ordners, Domäne (aus Info), Konto bzw. Rechnernamen der Systemrechner, genauer Wortlaut (Foto) einer Windows-Blockiermeldung, alles, was anders war als erwartet.

Im Testordner liegen danach echte Eingaben: nicht nach Hause mitnehmen. Für den Pilot weiterverwenden oder von der IT löschen lassen.

# Schlüsselkonzept (Entwurf, 2026-10-26)

Ziel: Keine Geheimnisse im Repo und nicht in der exe. Eine kopierte exe oder Datendatei ist außerhalb der Firma wertlos. So viel wie möglich ohne IT; die IT soll nur bestätigen.

## Kernidee
- Der Schlüssel ist eine **Datei im Datenordner** (`<Datenordner>\schluessel\arbeitsplan.key`, 32 Zufallsbytes), nicht Teil der exe.
- Die exe erzeugt sie **selbst beim allerersten Start** (Admin, leerer Datenordner). Kein IT-Eingriff nötig.
- Ohne Zugriff auf den Datenordner (Firmennetz/Laufwerk) gibt es weder Daten noch Schlüssel → exe leer, nichts lesbar.
- Die `.apdb` und die Sicherungen (`.apbak`) sind mit diesem Schlüssel verschlüsselt (AES + HMAC wie bisher). Im Dateikopf steht nur eine Schlüssel-ID (Hash), damit "falscher Schlüssel" klar erkannt wird (→ OFFLINE, nie überschreiben).

## Was ohne IT geht / was die IT nur bestätigt
| Punkt | Ohne IT | IT sagt nur "passt" |
|---|---|---|
| Schlüsseldatei erzeugen, Verschlüsselung, Umschlüsseln, Menü "Schlüssel erneuern" | ja (in der App) | – |
| Engere Ordnerrechte (z. B. kein Löschen in `daten`) | ja, wenn Sie am Laufwerk Rechte setzen dürfen (Rechtsklick → Sicherheit) | sonst Bitte an IT |
| Laufwerk nur für berechtigte Gruppe sichtbar | – | bestätigt, dass die Freigabe nur die Abteilung enthält |
| Zugriff von außen (Handy/Home-PC) gesperrt | – | bestätigt: Laufwerk/Server nur im Firmennetz (kein VPN-Zugang oder VPN ausdrücklich gewollt) |

## "Nur in der Firma synchronisieren"
- **PC**: Synchronisation = Zugriff auf den Datenordner. Er ist außerhalb des Firmennetzes nicht erreichbar → kein Sync. Das ist die eigentliche Sperre; sie liegt im Netz, nicht in der App. Zusätzlich optional `Domaene=` in der ini: App arbeitet nur auf Rechnern dieser Domäne (schwacher Zusatz, kein Ersatz).
- **Offline-Cache** (`%LOCALAPPDATA%\Arbeitsplan\cache.apdb`, nur Lesen): wird mit einem Schlüssel verschlüsselt, den Windows pro Benutzer schützt (DPAPI). Kopiert man die Datei auf einen anderen PC/Nutzer, ist sie unlesbar. Lebensdauer begrenzt (ini `CacheTage=`, Vorschlag 7), danach gelöscht.
  - **Warum nicht in OneDrive/`%APPDATA%` (roamend):** Der Cache soll den Plan nur auf genau diesem Firmen-PC lesbar halten; eine synchronisierte Kopie läge sonst auch in der Cloud und auf privaten Geräten mit demselben Konto, also außerhalb des Firmennetzes, das die eigentliche Sperre ist. DPAPI-Schlüssel sind an Benutzer und Rechner gebunden, ein geroamter Cache wäre auf dem nächsten PC ohnehin unlesbar und würde nur fehlerhafte OFFLINE-Starts erzeugen. Deshalb bleibt er lokal in `%LOCALAPPDATA%` und enthält keinen Pfad zum Datenordner (der Datenordner wird getrennt gemerkt, siehe unten).
- **Datenordner merken** (`%APPDATA%\Arbeitsplan\datenordner.txt`, je Nutzer und Programmordner): Ist der Datenordner beim Start nicht erreichbar (Laufwerk nicht verbunden, neuer PC), fragt das Programm danach (Laufwerk oder `\\server\freigabe\...`), akzeptiert nur einen Ordner mit vorhandener `arbeitsplan.apdb` (legt nie etwas an) und merkt ihn als UNC-Pfad. Die Datei enthält nur Pfade, keine Daten oder Schlüssel; sie darf mitroamen.
- **Handy**: geht erst mit Server (siehe tasks.md "Nach IT-Klärung"). Der Server hält den Schlüssel; das Handy bekommt nur Daten über die authentifizierte API, im Firmennetz/WLAN (oder VPN, falls die IT das will). Kein Schlüssel und keine Datenbank auf dem Handy; keine Offline-Kopie. Das braucht die IT zwingend (Server, Erreichbarkeit); ohne IT ist es nicht seriös lösbar.

## Grenzen (ehrlich)
- Wer legal Zugriff auf den Ordner hat (also alle Nutzer), kann Daten **und** Schlüssel kopieren. Das verhindert kein Schlüssel; dafür gelten Ordnerrechte + Änderungsprotokoll. Der Schutz gilt gegen Außenstehende, verlorene exe, USB-Stick, private Geräte.
- **Schlüssel weg = Daten weg.** Darum eine Kopie des Schlüssels getrennt hinterlegen (Admin, z. B. ausgedruckt/Tresor oder IT-Passwortmanager), nicht im selben Ordner wie die Sicherungen.

## Umsetzung (nach Freigabe)
1. `FileStore`/`Backup`: Schlüssel aus Datei laden statt aus der exe; Schlüssel-ID im Kopf; Fehler → OFFLINE wie bisher.
2. Erststart: Schlüsseldatei erzeugen (nur wenn Datenordner leer und Nutzer Admin).
3. Cache per DPAPI + Ablauf; ini `CacheTage=`, optional `Domaene=`.
4. Menü "Schlüssel erneuern" (Admin): alles neu verschlüsseln, vorher automatische Sicherung.
5. Test-Build: eigener, nicht eingecheckter Schlüssel (`%LOCALAPPDATA%`, `.gitignore`); Selbsttests erzeugen ihren Schlüssel zur Laufzeit.
6. Release-Build enthält keinen Schlüssel mehr; Verteilordner enthält ihn nicht (nur `daten\arbeitsplan.key` im Produktivordner).
7. Vorhandene Pilot-Daten: einmalig umschlüsseln (braucht den alten Schlüssel) oder neu beginnen.

## Entscheidungen des Nutzers (2026-10-26)
- Es gibt noch keine echten Daten -> frisch beginnen, kein Umschlüsseln nötig.
- Nutzer kann am Laufwerk keine Rechte setzen. Ordnerrechte (kopieren ja, verschieben/löschen nein) nur über IT; App-seitiger Ersatz: `.bak1..3`, Sicherungen, Änderungsprotokoll.
- Pfade: `Datenordner=` immer als UNC (z. B. Server und Freigabe als Doppel-Backslash-Pfad), nie Laufwerksbuchstabe; gleiche ini für alle PCs.

## Ordnerstruktur auf dem Laufwerk (Stand 2026-10-26, nach Rückmeldung)
Nutzer kopieren höchstens `Programm` (exe + ini). Alles andere liegt in **einem** Ordner `daten` auf dem Laufwerk:
```
...\Formulierung\Arbeitsplan\
  Programm\   Arbeitsplan.exe, Arbeitsplan.ini, statuscolors.ini, LIESMICH.txt   (starten oder lokal kopieren)
  daten\      arbeitsplan.apdb, arbeitsplan.key (versteckt), .bak1..3, sperren\, Pakete\, sicherung\
```
- Kein eigener sichtbarer Ordner `schluessel`: die Schlüsseldatei liegt versteckt neben der apdb. Wer `daten` lesen darf, kann sie lesen; das ist unvermeidbar (die exe muss sie ja lesen) und wird über Ordnerrechte und Protokoll abgesichert.
- `Pakete` und `sicherung` sind Unterordner von `daten`: bleiben auf dem Laufwerk.
- Lokal kopierte `Programm`-Ordner brauchen in der ini nur `Datenordner=<Pfad zum Laufwerk>`. Geheim ist darin nichts.
- Rechte-Wunsch an die IT (einmalig, optional): `Programm` nur Lesen; `daten` Lesen+Schreiben (ideal: kein Löschen für Nutzer in `daten`, `sicherung` nur Admin). Ohne diese Rechte läuft alles, nur der Schutz ist schwächer.

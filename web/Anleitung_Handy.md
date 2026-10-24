# Arbeitsplan auf dem Handy (schreibgeschützte Vorschau)

Reines HTML/JS mit erfundenen Testdaten. Kein Bearbeiten.

## Lokal starten (am PC, im Ordner `web`)
- Mit Python: `python -m http.server 8080` (Windows auch `py -m http.server 8080`).
- Ohne Python/Node: VS-Code-Erweiterung "Live Server" oder `npx` ist nicht nötig.
- Am PC öffnen: http://localhost:8080 (dort funktionieren Service Worker und Installation).

## Im Heim-WLAN am Handy
1. PC-IP ermitteln: `ipconfig` -> IPv4-Adresse (z. B. 192.168.1.20).
2. Handy im selben WLAN: `http://192.168.1.20:8080` im Browser öffnen.
3. Windows-Firewall-Rückfrage für Python ggf. nur für das private Netz zulassen.

## Zum Startbildschirm
- Android (Chrome): Menü (drei Punkte) -> "Zum Startbildschirm hinzufügen" bzw. "App installieren".
- iPhone (Safari): Teilen-Symbol -> "Zum Home-Bildschirm".

## Hinweis https / Offline
Der Service Worker (Offline, echte Installation) braucht https oder `localhost`. Über die WLAN-IP ohne https läuft die Seite normal, aber ohne Installation und ohne Offline-Betrieb; "Zum Startbildschirm" legt dann nur eine Verknüpfung an.

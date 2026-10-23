# Arbeitsplan – Neubau eines internen Windows-Programms

## Kontext
BioPharma, Vollkonti-Betrieb. Das alte Programm "Arbeitsplan" (exe) läuft aus und wird neu gebaut.
Es dient dazu, einzutragen, was wann im Betrieb läuft. Darf nur intern funktionieren.

## Oberfläche
- Wochenansicht: 7 Spalten (je 1 Tag), beliebig viele Zeilen.
- Jede Zeile hat Überschrift, Inhaltsblock, Einteilungsblock.
  - Beispiel: Überschrift "Montag, 23.10.2027" / Inhalt "Raumreinigung im Laufe des Tages. Produkt XY Lauf Z." / Einteilung "F: ABCD EFGH IJKL / S: MNOP QRST UVWX / N: (leer)".
  - Der Inhaltsblock enthält auch die benötigten Equipments.
- Kürzel (Mitarbeiter) und Equipments werden frei von Hand eingetragen, keine Stammlisten, keine Prüfungen.
- Nur Bildschirmanzeige (kein Druck); muss in 4:3 und 16:9 gut aussehen.
- Navigation: Button "Heute"; Pfeile unten für 1 Tag bzw. 1 Woche vor/zurück.
- Ansicht aktualisiert sich bei jeder UI-Änderung (Öffnen, Heute, Pfeile).

## Anforderungen
1. Ausgabe als eine .exe für Windows. Ablage auf dem Server, Start von dort, ohne Installation, ohne Admin-Rechte.
2. Läuft auf normalen Windows-PCs und auf "System"-PCs (Produktion, Anmeldung per Kartenleser-Anmeldung; dort kann man heute nichts speichern).
3. Ist-Problem: Nutzer müssen den Ordner (exe + Config) auf die eigene Platte kopieren, um speichern zu können; pro PC neu einrichten, lokale Daten werden bei Abwesenheit gelöscht. Soll entfallen.
4. Gleichzeitige Nutzung durch mehrere Mitarbeiter. "Letzter Speicherstand gewinnt" abfangen (Versionsprüfung pro Eintrag + Konfliktmeldung). Zusätzlich Nachladen im Hintergrund.
5. Benutzerverwaltung: Mitarbeiter einfach anlegen (+). Rechte einzeln oder als "Rechtepaket" vergeben, danach fein einstellbar. Alle Produktionsarbeiter müssen Text ändern/löschen/hinzufügen sowie Farben und Textformat (Schriftart, Farbe, fett/kursiv o. ä.) einstellen können.
6. Audit Trail (wer hat wann was geändert): optional, ggf. im Hintergrund.
7. Später: Equipment-Status abfragen (z. B. "G2-F-1234-30") und Equipment farblich nach Status darstellen. Schnittstelle von Anfang an vorsehen, zunächst leer.
8. Nur intern nutzbar. Wird die exe nach Hause kopiert, darf nichts Nutzbares passieren.

## Architekturvorschlag (noch nicht entschieden, mit IT klären)
- **Bevorzugt:** kleiner zentraler Dienst auf dem Server (Daten z. B. SQLite) + Client-exe. Client braucht keinen Schreibzugriff auf Laufwerke und spricht nur über das interne Netz mit dem Dienst. Löst Speichern auf System-PCs, Mehrbenutzer-Konflikte, Rechte und Audit zentral; Client startet nur, wenn er den internen Server erreicht.
- **Fallback ohne Dienst:** gemeinsame Datendatei auf dem Netzlaufwerk (braucht Schreibrechte für alle inkl. System-PCs; Schutz vor Kopieren nach Hause schwächer).
- Technologie: siehe Entscheidungen.

## Entscheidungen
- 2026-10-23: In der Entwicklungsumgebung ist **kein .NET SDK** installiert, aber der .NET-Framework-4.x-Compiler (`csc.exe`) von Windows. Prototyp daher in **C# / WinForms (.NET Framework 4.x)**, gebaut mit `build.cmd`. Vorteil: Zielrechner mit Windows 10/11 haben Framework 4.8 bereits → eine exe, keine Installation, keine Admin-Rechte. Später ggf. auf .NET 8 (Single-File) umstellbar, wenn SDK/IT-Freigabe da ist.
- Prototyp hält Daten nur im Speicher (Testdaten); Server-Anbindung folgt nach IT-Klärung.
- Wochenstart: vorläufig Montag ("Heute" springt auf den Montag der aktuellen Woche, Tag-Pfeile verschieben das 7-Tage-Fenster um 1 Tag).

## Offene Punkte (Nutzer / IT)
- Kann die IT einen kleinen Dienst auf einem Server laufen lassen?
- Wer ist am System-PC angemeldet (Sammelkonto bei Kartenleser-Anmeldung?) und wie wird die Person beim Ändern identifiziert (Kürzel/PIN)?
- Windows-Version der Rechner; sind unsignierte exe-Dateien erlaubt?
- Sollen alte Wochenpläne übernommen werden?
- Woche immer Montag bis Sonntag?

## Arbeitsweise
- Sparsam arbeiten, Rückfragen gebündelt stellen.
- Bauen: `build.cmd` → `bin\Arbeitsplan.exe`. Testbild: `bin\Arbeitsplan.exe --shot datei.png 1920 1000`.
- Repo: https://github.com/lngwaI/arbeitsplan (öffentlich! Keine echten Namen, Kürzel, Server- oder Produktdaten committen.)

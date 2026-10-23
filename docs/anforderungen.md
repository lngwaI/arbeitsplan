# Anforderungen Arbeitsplan (Details; bei Bedarf lesen, nicht standardmäßig)

BioPharma, Vollkonti-Betrieb. Altes "Arbeitsplan"-exe läuft aus. Dient zum Eintragen, was wann im Betrieb läuft. Nur intern.

## Oberfläche
- Wochenansicht: 7 Spalten (je 1 Tag), beliebig viele Zeilen. Zeile = Überschrift + Inhaltsblock + Einteilungsblock.
- Beispiel: "Montag, 23.10.2027" / "Raumreinigung im Laufe des Tages. Produkt XY Lauf Z." / "F: ABCD EFGH IJKL / S: MNOP QRST UVWX / N: (leer)".
- Inhalt enthält auch benötigte Equipments. Kürzel/Equipments frei von Hand, keine Stammlisten/Prüfungen.
- Nur Bildschirm (kein Druck), gut in 4:3 und 16:9.
- Navigation: "Heute"; Pfeile für 1 Tag / 1 Woche. Ansicht aktualisiert sich bei jeder UI-Änderung.

## Anforderungen
1. Eine .exe für Windows (Ziel: ab XP SP3 + .NET 4.0, sowie Win 10/11). Ablage/Start vom Server, ohne Installation und Admin.
2. Läuft auf normalen PCs und "System"-PCs (Produktion, Kartenleser-Anmeldung, dort kein lokales Speichern).
3. Entfällt: Ordner pro Nutzer lokal kopieren/einrichten; lokale Daten werden gelöscht.
4. Mehrbenutzer: Überschreiben ("letzter gewinnt") abfangen (Version pro Eintrag + Konfliktmeldung); Nachladen im Hintergrund.
5. Benutzerverwaltung: Mitarbeiter einfach anlegen (+); Rechte einzeln oder als Rechtepaket, danach fein einstellbar. Alle Produktionsarbeiter: Text ändern/löschen/hinzufügen, Farben und Textformat (Schrift, Farbe, fett/kursiv o. ä.).
6. Audit Trail (wer/wann/was): optional, ggf. im Hintergrund.
7. Später: Equipment-Status (z. B. "G2-F-1234-30") abfragen, Equipment farblich nach Status. Schnittstelle vorsehen, zunächst leer (`IEquipmentStatus`).
8. Nur intern nutzbar; exe nach Hause kopiert → darf nichts Nutzbares passieren.

## Architektur (nicht entschieden, mit IT klären)
- Bevorzugt: kleiner Server-Dienst (Daten z. B. SQLite) + Client-exe; Client spricht nur über internes Netz, kein Schreibzugriff nötig. Löst 2/3/4/5/6/8 zentral.
- Fallback: gemeinsame Datendatei auf Netzlaufwerk (Schreibrechte für alle inkl. System-PCs nötig; Schutz gegen Kopieren schwächer).
- Mobile (Android/iOS): ON HOLD. Weg dorthin: Server bekommt HTTP-API, dazu Web-Client (HTML/JS, als PWA nutzbar). `src/Core` bleibt UI-frei, damit Logik wiederverwendbar ist.

## Offene Punkte (Nutzer/IT)
- Kann IT einen Dienst auf einem Server betreiben?
- Wer ist am System-PC angemeldet (Sammelkonto Kartenleser-Anmeldung?); Identifikation beim Ändern (Kürzel/PIN)?
- Windows-Version der Rechner; unsignierte exe erlaubt? Läuft wirklich noch XP (dann .NET 4.0 dort vorhanden)?
- Alte Wochenpläne übernehmen?
- Woche immer Mo–So?

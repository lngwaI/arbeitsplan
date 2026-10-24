# Arbeitsplan – Anleitung Windows

## Starten
- **Release** (`release\Arbeitsplan.exe`): Doppelklick. Läuft ohne Installation und ohne weitere Dateien (Win 10/11; ab XP SP3 mit .NET Framework 4.0). Angemeldet wird mit dem Windows-Namen.
  Im Prototyp erhält ein unbekannter Windows-Nutzer das Rechtepaket "Produktion" (später per IT-Entscheidung: Leser/abweisen). Daten liegen nur im Speicher (Testdaten), nichts wird gespeichert.
- **Test-Build** (`bin\Arbeitsplan.exe`, nur Entwicklung): zusätzliche Schalter
  - `--user mmuster` (Admin) | `ebeispiel` (Leser) | `pwerk` (Produktion)
  - `--nosave` simuliert einen PC ohne Speichern
  - `--4x3` Fenster 1024×768, `--size BxH` beliebig
  - Beispiel: `bin\Arbeitsplan.exe --user mmuster --nosave --4x3`

## Bauen
- Test-Build: `build.cmd` → `bin\Arbeitsplan.exe`
- Release: `build_release.cmd` → `release\Arbeitsplan.exe` (ohne Testschalter, Manifest, keine Debug-Symbole)
- Beide mit dem in Windows enthaltenen Compiler, kein SDK nötig.

## Look umschalten
`UseModernTheme=true|false` in `theme.ini` neben der exe (optional; fehlt die Datei, gilt der moderne Look).

## Kurz testen
1. Woche wechseln (« Woche, ‹ Tag, Heute, Tag ›, Woche »).
2. Zeile auf-/zuklappen (Klick auf den Zeilenkopf); "(LKZ)" rot = noch zu messen, grün = gemessen.
3. Doppelklick auf einen Block → Bearbeiten; Strg+S speichert und beendet, Esc beendet (bei Änderungen Rückfrage: speichern Ja/Nein/Abbrechen); Enter und Umschalt+Enter = neue Zeile. Formatleiste im Bearbeitungsmodus.
4. Suche (Strg+F), Enter springt zum nächsten Treffer; Groß-/Kleinschreibung egal.
5. Menü Woche → Export (PNG/PDF/Zwischenablage/Paket); Menü Pakete → Paket-Editor.

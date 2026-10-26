# Arbeitsplan – Anleitung Windows

## Starten
- **Release** (`release\Arbeitsplan.exe`): Doppelklick. Läuft ohne Installation und ohne weitere Dateien (Win 10/11; ab XP SP3 mit .NET Framework 4.0). Angemeldet wird mit dem Windows-Namen.
  Im Release gibt es keine Testnutzer: jeder Windows-Nutzer erhält nur Leserechte (Ansehen, Export). Zum Vorführen von Bearbeiten, Rechten und Katalog den Test-Build verwenden. Daten sind Testdaten und liegen nur im Speicher.
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
Startparameter `--theme xp|win7|win10` (ohne Angabe passend zum Betriebssystem; Win 10/11 = heller Look). Das Modell steht im Fenstertitel.

## Kurz testen
1. Woche wechseln (« Woche, ‹ Tag, Heute, Tag ›, Woche »).
2. Zeile auf-/zuklappen (Klick auf den Zeilenkopf); "(LKZ)" rot = noch zu messen, grün = gemessen.
3. Doppelklick auf einen Block → Bearbeiten; Strg+S speichert und beendet, Esc beendet (bei Änderungen Rückfrage: speichern Ja/Nein/Abbrechen); Enter und Umschalt+Enter = neue Zeile. Formatleiste im Bearbeitungsmodus.
4. Suche (Strg+F), Enter springt zum nächsten Treffer; Groß-/Kleinschreibung egal.
5. Menü Woche → Export (PNG/PDF/Zwischenablage/Paket); Menü Pakete → Paket-Editor.

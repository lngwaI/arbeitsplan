# Arbeitsplan – Anleitung Windows

## Starten
- **Release** (`make_dist.cmd` → `dist\Arbeitsplan\Programm\Arbeitsplan.exe`): läuft ohne Installation (Win 10/11; ab XP SP3 mit .NET Framework 4.0) und braucht den Ordner `daten` daneben (Laufwerk). Angemeldet wird mit dem Windows-Namen. Nur angelegte Nutzer starten. Ein leerer Datenordner wird einmalig von einem Nutzer aus `Admins=` (Arbeitsplan.ini) eingerichtet. Ablauf und Tests: `docs/Testanleitung.md`.
- **Test-Build** (`bin\Arbeitsplan.exe`, nur Entwicklung): zusätzliche Schalter
  - `--user mmuster` (Admin) | `ebeispiel` (Leser) | `pwerk` (Produktion)
  - `--nosave` simuliert einen PC ohne Speichern
  - `--data <ordner>` verschlüsselte Datendatei statt Testdaten (Mehrplatz-Test, siehe `docs/Testanleitung.md`)
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

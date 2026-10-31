using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

// Core: keine UI-Abhaengigkeit (spaeter wiederverwendbar fuer Server/Web-Client).
// Zeilenumbrueche in Texten immer "\n".
namespace Arbeitsplan.Core
{
    // Manuelle Formatierung eines Textbereichs im Inhalt. Standardwerte (Rgb 0 = schwarz, Size 0, Font null) werden nicht gespeichert.
    public class FormatSpan
    {
        public int Start, Length, Rgb;      // Rgb 0xRRGGBB
        public float Size;                  // Punkt; 0 = Standardgroesse
        public bool Bold, Italic;
        public string Font;                 // null = Standardschrift

        public FormatSpan Clone() { return (FormatSpan)MemberwiseClone(); }
        public string Key() { return Start + "," + Length + "," + Rgb + "," + Size + "," + Bold + "," + Italic + "," + Font + ";"; }
    }

    public class Entry   // Inhalt eines Blocks (Tag x Zeile)
    {
        public string Content = "";
        public List<FormatSpan> Spans = new List<FormatSpan>();
        public string[] People = { "", "", "" };   // Kuerzel je Schicht (F, S, N)
        public bool Lkz;                            // "LKZ gemessen?" (nur Zeilen mit HasLkz)
        public List<BufferItem> Buffers = new List<BufferItem>();   // Puffer (Zeile mit RowInfo.Variants); einzige Datenquelle der Herstellungstypen
        public List<FreezeItem> Freezes = new List<FreezeItem>();   // Freeze-Vorgaenge (Zeile mit RowInfo.Freeze); Cycles werden berechnet (FreezeCalc)
        public List<string> Marks = new List<string>();   // gesetzte Pflichtpunkte (CheckDef.Key, ausser LKZ)
        public int Version;                         // Konflikterkennung

        // Typen der Puffer als VariantSet-String ("mobil:2|stationaer"), "" = keine; Eingabe fuer CheckEval
        public string Types { get { return BufferSet.Types(Buffers); } }

        public Entry Clone()
        {
            Entry e = (Entry)MemberwiseClone();
            e.People = (string[])People.Clone();
            e.Marks = new List<string>(Marks);
            e.Buffers = BufferSet.CloneAll(Buffers);
            e.Freezes = FreezeSet.CloneAll(Freezes);
            e.Spans = new List<FormatSpan>();
            foreach (FormatSpan s in Spans) e.Spans.Add(s.Clone());
            return e;
        }
        // Block nur mit Notizzeilen gilt als leer
        public bool IsEmpty { get { return (Content.Length == 0 || (HasNotes && Notes.ContentText(this).Trim().Length == 0)) && People[0].Length == 0 && People[1].Length == 0 && People[2].Length == 0 && Freezes.Count == 0; } }
        // Leer UND ohne Haken/Variante (Zuklappen, Hinweispunkt); IsEmpty bleibt fuer Rechte/Pakete wie bisher
        public bool IsBlank { get { return !HasText && People[0].Trim().Length == 0 && People[1].Trim().Length == 0 && People[2].Trim().Length == 0 && !Lkz && Buffers.Count == 0 && Freezes.Count == 0 && Marks.Count == 0; } }
        // Inhalt mit sichtbarem Text ohne Notizzeilen (nur Leerzeichen/Tabs/Zeilenumbrueche = leer); Daten bleiben unveraendert
        public bool HasText { get { return Notes.ContentText(this).Trim().Length > 0; } }
        // Notizzeilen vorhanden (Block mit nur Notizen gilt als leer)
        public bool HasNotes { get { return Notes.NoteLines(this).Count > 0; } }
        public string MarksKey() { return string.Join("|", Marks.ToArray()); }
        public string SpanKey() { string k = ""; foreach (FormatSpan s in Spans) k += s.Key(); return k; }
    }

    public enum ShiftMode { FSN, FS, Task }

    public class RowInfo   // gilt fuer die ganze Zeile (alle Tage); wird nur ueber die Blockstruktur-Einstellungen geaendert
    {
        public ShiftMode Mode = ShiftMode.FSN;
        public string Variants = "";                // z. B. "mobil|stationaer", "" = keine
        public int Id;                              // stabil; Reihenfolge ergibt sich aus der Listenposition
        public string Name = "";
        public string[] Labels = { "F", "S", "N" };
        public int MinLines = 3;                    // Mindesthoehe des Inhaltsfelds in Textzeilen
        public bool HasLkz;                         // Feld "LKZ gemessen?" zwischen Titel und Inhalt
        public bool Freeze;                         // Zeile fuer Freeze-Vorgaenge (Entry.Freezes); alte Strukturen: Name enthaelt "Freeze"

        public RowInfo Clone() { RowInfo r = (RowInfo)MemberwiseClone(); r.Labels = (string[])Labels.Clone(); return r; }
        // Schichtfeld s (0=F, 1=S, 2=N) sichtbar? FS ohne N, Tagesaufgabe nur ein Feld (F = "Erledigt von"); Daten bleiben erhalten.
        public bool HasShift(int s) { return s == 0 || (s == 1 && Mode != ShiftMode.Task) || (s == 2 && Mode == ShiftMode.FSN); }
        public static bool IsFreezeName(string name) { return name != null && name.IndexOf("Freeze", StringComparison.OrdinalIgnoreCase) >= 0; }
        public string[] VariantList() { return Variants.Length == 0 ? new string[0] : Variants.Split('|'); }
    }

    public class SearchHit { public DateTime Day; public int RowId; }

    // Ein Block fuer IDataStore.SaveManyWithAudit. Audit null = ohne Protokoll. Saved/Data.Version erst nach erfolgreichem Schreiben.
    public class SaveItem
    {
        public DateTime Day; public int RowId; public Entry Data; public AuditEntry Audit;
        public bool Saved;                  // false = Konflikt (Data.Version veraltet)
    }

    public interface IDataStore
    {
        Entry Get(DateTime day, int rowId);                  // Kopie oder null
        bool Save(DateTime day, int rowId, Entry e);         // false = Konflikt (e.Version veraltet); setzt sonst e.Version neu; Exception = nicht speicherbar
        List<RowInfo> GetRows();                             // Kopien in Anzeigereihenfolge
        void SaveStructure(List<RowInfo> rows);              // Reihenfolge/Namen/Anzahl; Id <= 0 = neue Zeile; keine Zeile wird geloescht
        List<SearchHit> Find(string query);                  // Inhalt und Kuerzel (nicht Raumnamen), nach Datum/Zeile sortiert
        Catalog GetCatalog();                                // Equipment-Positionen und Bausteine fuer Pakete (Kopie)
        void SaveCatalog(Catalog catalog);
        DutyConfig GetDuties();                              // Pflichtpunkte, Rundgaenge, Feiertage (Kopie)
        void SaveDuties(DutyConfig duties);
        List<WeekMark> GetWeekMarks(DateTime monday);        // Kopien
        // Wochenhaken + Protokoll atomar; false = Konflikt (m.Version veraltet); setzt sonst m.Version neu. a darf null sein.
        bool SaveWeekMark(WeekMark m, AuditEntry a);
        List<DeviceStatus> GetStatuses();                    // manuell gesetzte Equipment-Status (Kopien)
        // Status + Protokoll atomar; Cleared = Eintrag entfernen. Letzter Schreiber gewinnt je Nummer. a darf null sein.
        bool SetStatus(DeviceStatus s, AuditEntry a);
        // Historie ohne Obergrenze: alte Wochen und Protokolleintraege werden nie geloescht ("ein Jahr zurueck" und mehr).
        void AppendAudit(AuditEntry a);                      // append-only, kein Aendern/Loeschen
        // Save + Protokolleintrag atomar (beides oder nichts); setzt a.Before/After/Version auf den Stand beim Speichern
        bool SaveWithAudit(DateTime day, int rowId, Entry e, AuditEntry a);
        // Viele Bloecke in EINEM Schreibvorgang, Versionspruefung je Block (Saved je Item); Rueckgabe = Anzahl gespeichert.
        // Exception = nichts gespeichert.
        int SaveManyWithAudit(IList<SaveItem> items);
        List<AuditEntry> GetAudit(DateTime day, int rowId);  // Kopien, chronologisch
        StoreData Snapshot();                                // vollstaendige Kopie aller Daten (Sicherung)
        void Restore(StoreData data);                        // ersetzt Plaene/Struktur/Katalog; Protokoll wird zusammengefuehrt
    }
}

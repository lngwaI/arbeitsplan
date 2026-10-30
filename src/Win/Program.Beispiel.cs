#if TESTHOOKS
using System;
using System.IO;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Hilfe fuer beispiel\ (scripts\beispiel-bauen.cmd): erfundene Pakete und ein Ausgangsstand als Sicherung (APBAK1, ohne Schluessel).
    static partial class Program
    {
        static void MakeBeispiel(string folder, bool packagesOnly)
        {
            string pk = Path.Combine(folder, "Pakete"); Directory.CreateDirectory(pk);
            Package.SampleBuffers("Beispiel-Kampagne Produkt 1234").Save(Path.Combine(pk, "Beispiel-Kampagne Produkt 1234.apkg"));
            Package pu = new Package(); pu.Name = "Beispiel-Puffer"; pu.Type = PackageType.Einzel;
            PackageStep ps = new PackageStep(); ps.Title = "Puffer mobil"; ps.Source = "Puffer mobil"; ps.Row = "Puffer"; ps.Day = 1; ps.Variant = "mobil"; ps.PufferCount = 2; ps.PufferNames = "Lösung A|Lösung B";
            pu.Steps.Add(ps); pu.Save(Path.Combine(pk, "Beispiel-Puffer.apkg"));
            Package ev = new Package(); ev.Name = "Beispiel-Sonderereignis TFF Flush"; ev.Type = PackageType.Ereignis;
            PackageStep es = new PackageStep(); es.Title = "TFF Flush"; es.Source = "TFF Flush"; es.Row = "Suite 1"; es.Day = 1; es.Text = "TFF Flush, Membran spülen"; ev.Steps.Add(es);
            ev.Save(Path.Combine(pk, "Beispiel-Sonderereignis TFF Flush.apkg"));
            if (packagesOnly) return;
            DateTime monday = DateTime.Today; while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(-1);
            MemoryStore ms = MemoryStore.Sample(monday);
            User by = new User(); by.Login = "mmuster"; by.Kuerzel = "MUMA"; by.First = "Max"; by.Last = "Mustermann"; by.RoleText = "Admin";
            Package.SampleCampaign("Beispiel").Apply(ms, monday.AddDays(7), ApplyMode.FillEmpty, null, by);
            Backup.WriteTo(ms.Snapshot(), Path.Combine(folder, "sicherung"), "Beispiel-Ausgangsstand", null);
        }
    }
}
#endif

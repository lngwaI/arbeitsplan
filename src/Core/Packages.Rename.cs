using System;
using System.Collections.Generic;
using System.IO;

namespace Arbeitsplan.Core
{
    public partial class Package
    {
        // P6: Produktcode in allen Paketdateien des Ordners aendern (map alt -> neu). Gibt die Zahl geaenderter Dateien zurueck;
        // je Datei, die sich nicht lesen/schreiben laesst, kommt eine Zeile in errors (Datei bleibt dann unveraendert, nichts geht still verloren).
        public static int RenameProduct(string folder, Dictionary<string, string> map, List<string> errors)
        {
            int n = 0;
            string[] files;
            try { files = Directory.GetFiles(folder ?? "", "*.apkg"); }
            catch (Exception ex) { errors.Add(folder + ": " + ex.Message); return 0; }
            foreach (string f in files)
            {
                try
                {
                    Package p = Load(f); string nc;
                    if (!map.TryGetValue(p.ProductCode, out nc)) continue;
                    p.ProductCode = nc; p.Save(f); n++;
                }
                catch (Exception ex) { errors.Add(Path.GetFileName(f) + ": " + ex.Message); }
            }
            return n;
        }
    }
}

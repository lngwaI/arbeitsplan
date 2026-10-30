#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Selbsttest "Packen (Back-Up)" am Paket-Editor: neues Paket hat Packen + Back-Up (Tag +1, gleicher Raum), Packen nimmt seinen
    // Back-Up beim Entfernen mit, Back-Up allein entfernbar, Back-Up folgt dem Packen beim Verschieben.
    partial class PackageForm
    {
        public static string TestBackup(IDataStore store, IEquipmentStatusProvider equip, DateTime start)
        {
            PackageForm f = new PackageForm(store, equip, start, null, true, true);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
            try
            {
                f.Show(); Application.DoEvents();
                PackageStep pk = f.steps.Find(delegate(PackageStep s) { return s.Source == Package.PackenName; });
                PackageStep bk = f.steps.Find(delegate(PackageStep s) { return s.IsBackup; });
                bool fresh = pk != null && bk != null && bk.BacksUp(pk) && bk.Row == pk.Row && bk.FirstDay == pk.FirstDay + 1;
                pk.MoveAll(2); f.Bind(pk); f.Changed(); Application.DoEvents();
                bool follow = bk.FirstDay == pk.FirstDay + 1 && pk.FirstDay == 3;
                f.Bind(bk); f.RemoveStep(false); Application.DoEvents();
                bool alone = !f.steps.Contains(bk) && f.steps.Contains(pk);
                f.Bind(pk); f.RemoveStep(false); Application.DoEvents();
                bool then = !f.steps.Contains(pk);
                f.LoadPackage(null); Application.DoEvents();
                pk = f.steps.Find(delegate(PackageStep s) { return s.Source == Package.PackenName; }); bk = f.steps.Find(delegate(PackageStep s) { return s.IsBackup; });
                f.Bind(pk); f.RemoveStep(false); Application.DoEvents();
                bool both = !f.steps.Contains(pk) && !f.steps.Contains(bk);   // Packen nimmt seinen Back-Up mit
                return "Back-Up Editor: neues Paket " + (fresh ? "ok" : "FEHLER") + ", Back-Up folgt " + (follow ? "ok" : "FEHLER")
                    + ", Back-Up allein entfernbar " + (alone ? "ok" : "FEHLER") + ", danach Packen entfernbar " + (then ? "ok" : "FEHLER") + ", Packen nimmt Back-Up mit " + (both ? "ok" : "FEHLER");
            }
            catch (Exception ex) { return "Back-Up Editor: FEHLER " + ex.GetType().Name + ": " + ex.Message; }
            finally { f.dirty = false; f.Close(); f.Dispose(); }
        }
    }
}
#endif

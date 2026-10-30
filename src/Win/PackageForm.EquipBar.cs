using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Paket-Editor (N5): unbekanntes Geraet im aktuellen Schritt -> Leiste "<ID> ist nicht in der Equipment-Liste – [Hinzufügen] [Ignorieren]".
    // Mit Recht PackageAdmin legt "Hinzufügen" das Geraet sofort an (Geraeteart aus dem Schema geraten, Position, Katalog speichern), ohne Dialog;
    // "Details…" oeffnet EquipAddForm. Ohne Recht: "Admin informieren" = dauerhafte Vormerkung im Katalog (EquipMaster.Pending).
    partial class PackageForm
    {
        internal class Unk { public string Id, Short; public PackageStep Step; }

        readonly EquipBar eqBar = new EquipBar();
        readonly HashSet<string> ignoredIds = new HashSet<string>();   // "Ignorieren" (nur in diesem Fenster)
        readonly List<Unk> unk = new List<Unk>();                      // unbekannte Geraete aller Schritte (von UpdateHints gesammelt)
        readonly Timer doneTimer = new Timer();
        string Login { get { return By != null ? By.Login : Environment.UserName; } }   // Nutzer fuer die Vormerkung (By setzt MainForm)
        Unk barTarget;
        string doneId = "", donePos = ""; bool doneAdded, doneHadPos, donePending; PackageStep doneStep;   // letzte Rueckmeldung (fuer Rueckgaengig)

        void InitEquipBar()
        {
            eqBar.Main = BarMain; eqBar.Details = delegate { if (barTarget != null) AddToList(barTarget.Id, barTarget.Short); }; eqBar.Close = BarClose;
            doneTimer.Interval = 9000; doneTimer.Tick += delegate { EndDone(); };
            Disposed += delegate { doneTimer.Stop(); doneTimer.Dispose(); };   // N7: kein Tick auf ein geschlossenes Fenster
        }

        // Von EquipHint aufgerufen: ID ist nirgends in der Equipment-Liste
        void NoteUnknown(string id, string shortName, PackageStep s)
        {
            if (!unk.Exists(delegate(Unk x) { return x.Step == s && x.Id == id; })) { Unk u = new Unk(); u.Id = id; u.Short = shortName; u.Step = s; unk.Add(u); }
        }

        bool IsNoted(string id) { return notedIds.Contains(id) || cat.Master.PendingFor(id) != null; }

        void UpdateEquipBar()
        {
            if (eqBar.Confirm && doneStep == cur) return;   // Rueckmeldung bleibt stehen, bis OK/Zeit/anderer Schritt
            if (eqBar.Confirm) { EndDone(); return; }
            List<Unk> open = unk.FindAll(delegate(Unk x) { return x.Step == cur && !ignoredIds.Contains(x.Id) && (canAdmin || !IsNoted(x.Id)); });   // Admin sieht auch Vorgemerktes
            if (cur == null || open.Count == 0) { barTarget = null; eqBar.Hide2(); return; }
            barTarget = open[0];
            eqBar.ShowPrompt(barTarget.Id + " ist nicht in der Equipment-Liste" + (open.Count > 1 ? " (+" + (open.Count - 1) + " weitere)" : "") + " – " + (canAdmin ? "hinzufügen?" : "Admin informieren?"),
                canAdmin ? "Hinzufügen" : "Admin informieren", canAdmin);
        }

        void BarMain()
        {
            if (eqBar.Confirm) { UndoDone(); return; }
            if (barTarget == null) return;
            if (canAdmin) QuickAdd(barTarget); else Inform(barTarget);
        }

        void BarClose()
        {
            if (eqBar.Confirm) { EndDone(); return; }
            if (barTarget != null) { ignoredIds.Add(barTarget.Id); UpdateEquipBar(); }
        }

        void Done(string text, string id, string pos, bool added, bool hadPos, bool pending)
        {
            doneId = id; donePos = pos; doneAdded = added; doneHadPos = hadPos; donePending = pending; doneStep = cur;
            eqBar.ShowDone(text, true); doneTimer.Stop(); doneTimer.Start();
        }

        void EndDone() { doneTimer.Stop(); eqBar.Hide2(); doneId = ""; doneStep = null; UpdateEquipBar(); }

        // Admin: Geraet sofort anlegen
        internal bool QuickAdd(Unk t)
        {
            EquipPosition pos = cat.Position(t.Short);
            EquipDevice d = cat.Master.NewDevice(t.Id, pos);
            bool had = pos != null && pos.Ids.Contains(d.Id);
            if (!AddDevice(d, t.Short)) return false;
            Done(d.Id + " hinzugefügt (" + (d.Class.Length > 0 ? "Art " + d.Class : "ohne Geräteart") + (pos != null ? ", Position " + pos.Short : "") + ")", d.Id, t.Short, true, had, false);
            return true;
        }

        // Ohne Recht: dauerhaft vormerken (frischer Katalogstand, damit nur die Vormerkung dazukommt)
        internal bool Inform(Unk t)
        {
            try
            {
                Catalog fresh = store.GetCatalog();
                if (fresh.Master.Device(t.Id) == null && fresh.Master.AddPending(t.Id, t.Short, Login, DateTime.Now)) store.SaveCatalog(fresh);
                cat.Master.Pending = fresh.Master.Pending;
            }
            catch (Exception ex) { MessageBox.Show(this, "Die Vormerkung konnte nicht gespeichert werden:\n" + ex.Message, "Equipment", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            notedIds.Add(t.Id); UpdateHints();
            Done(t.Id + " vorgemerkt – der Admin nimmt die Nummer in die Liste auf", t.Id, t.Short, false, false, true);
            return true;
        }

        void UndoDone()
        {
            string id = doneId;
            try
            {
                if (donePending)
                {
                    Catalog fresh = store.GetCatalog(); fresh.Master.RemovePending(id); store.SaveCatalog(fresh); cat.Master.Pending = fresh.Master.Pending; notedIds.Remove(id);
                }
                else if (doneAdded)
                {
                    cat.Master.Devices.RemoveAll(delegate(EquipDevice x) { return string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase); });
                    EquipPosition pos = cat.Position(donePos);
                    if (pos != null && !doneHadPos) pos.Ids.Remove(id);
                    store.SaveCatalog(cat);
                }
            }
            catch (Exception ex) { MessageBox.Show(this, "Rückgängig machen ist fehlgeschlagen:\n" + ex.Message, "Equipment", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            doneTimer.Stop(); eqBar.Hide2(); doneStep = null; doneId = "";
            if (cur != null) Bind(cur); else UpdateHints();
        }

#if TESTHOOKS
        internal EquipBar TestBar { get { return eqBar; } }
        internal string TestBarTarget { get { return barTarget != null ? barTarget.Id : ""; } }
#endif
    }
}

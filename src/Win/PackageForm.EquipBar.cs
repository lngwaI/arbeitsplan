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
        internal class DoneItem { public string Id, Pos; public bool Added, HadPos, HadDev, Pending; public EquipPending HadPend; }
        List<DoneItem> done = new List<DoneItem>(); string doneText = ""; PackageStep doneStep;   // letzte Rueckmeldung (fuer Rueckgaengig; "Alle hinzufuegen" = ein Eintrag je Geraet)

        void InitEquipBar()
        {
            eqBar.Main = BarMain; eqBar.Details = delegate { if (barTarget != null) AddToList(barTarget.Id, barTarget.Short); }; eqBar.Close = BarClose;
            eqBar.All = delegate { AddAll(); }; eqBar.Undo = UndoDone; eqBar.Replace = ReplaceMenu;
            doneTimer.Interval = 9000; doneTimer.Tick += delegate { EndDone(); };
            Disposed += delegate { doneTimer.Stop(); doneTimer.Dispose(); };   // N7: kein Tick auf ein geschlossenes Fenster
        }

        // Von EquipHint aufgerufen: ID ist nirgends in der Equipment-Liste
        void NoteUnknown(string id, string shortName, PackageStep s)
        {
            if (!unk.Exists(delegate(Unk x) { return x.Step == s && x.Id == id; })) { Unk u = new Unk(); u.Id = id; u.Short = shortName; u.Step = s; unk.Add(u); }
        }

        bool IsNoted(string id) { return notedIds.Contains(id) || cat.Master.PendingFor(id) != null; }

        List<Unk> OpenUnk() { return unk.FindAll(delegate(Unk x) { return x.Step == cur && !ignoredIds.Contains(x.Id) && (canAdmin || !IsNoted(x.Id)); }); }   // Admin sieht auch Vorgemerktes

        void UpdateEquipBar()
        {
            List<Unk> open = OpenUnk();
            bool recent = cur != null && doneStep == cur && done.Count > 0;   // Rueckmeldung bleibt, bis OK/Zeit/anderer Schritt
            if (cur == null || (open.Count == 0 && !recent)) { barTarget = null; eqBar.Hide2(); return; }
            if (open.Count == 0) { barTarget = null; eqBar.ShowDone(doneText, true); return; }
            barTarget = open[0];
            int n = open.FindAll(delegate(Unk x) { return open.FindIndex(delegate(Unk y) { return y.Id == x.Id; }) == open.IndexOf(x); }).Count;   // verschiedene Nummern
            eqBar.ShowPrompt(barTarget.Id + " ist nicht in der Equipment-Liste" + (open.Count > 1 ? " (+" + (open.Count - 1) + " weitere)" : "") + " – " + (canAdmin ? "hinzufügen?" : "Admin informieren?")
                + (recent ? "   Zuletzt: " + doneText : ""), canAdmin ? "Hinzufügen" : "Admin informieren", canAdmin, n, recent);
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

        void Done(string text, List<DoneItem> items)
        {
            done = items; doneText = text; doneStep = cur;
            doneTimer.Stop(); doneTimer.Start();
            UpdateEquipBar();
        }

        void EndDone() { doneTimer.Stop(); done = new List<DoneItem>(); doneText = ""; doneStep = null; UpdateEquipBar(); }

        // Admin: Geraet sofort anlegen (null = abgebrochen); die Leiste springt danach zum naechsten unbekannten Geraet
        DoneItem QuickAdd1(Unk t, out string text)
        {
            text = "";
            EquipPosition pos = cat.Position(t.Short);
            EquipDevice d = cat.Master.NewDevice(t.Id, pos);
            bool had = pos != null && pos.Ids.Contains(d.Id), hadDev = cat.Master.Device(d.Id) != null;
            EquipPending hadPend = cat.Master.PendingFor(d.Id);
            if (!AddDevice(d, t.Short)) return null;
            text = d.Id + " hinzugefügt (" + (d.Class.Length > 0 ? "Art " + d.Class : "ohne Geräteart") + (pos != null ? ", Position " + pos.Short : "") + ")";
            DoneItem i = new DoneItem(); i.Id = d.Id; i.Pos = t.Short; i.Added = true; i.HadPos = had; i.HadDev = hadDev; i.HadPend = hadPend;
            return i;
        }

        internal bool QuickAdd(Unk t)
        {
            string text; DoneItem i = QuickAdd1(t, out text);
            if (i == null) return false;
            Done(text, new List<DoneItem>(new[] { i }));
            return true;
        }

        // Alle unbekannten Geraete des Schritts auf einmal; ein Rueckgaengig nimmt alle zurueck
        internal bool AddAll()
        {
            if (!canAdmin) return false;
            List<Unk> open = OpenUnk(), todo = new List<Unk>();
            foreach (Unk u in open) if (!todo.Exists(delegate(Unk x) { return x.Id == u.Id; })) todo.Add(u);
            List<DoneItem> items = new List<DoneItem>(); string text = "", last = "";
            foreach (Unk u in todo) { DoneItem i = QuickAdd1(u, out text); if (i == null) break; items.Add(i); last = text; }
            if (items.Count == 0) return false;
            Done(items.Count == 1 ? last : items.Count + " Geräte hinzugefügt (" + string.Join(", ", items.ConvertAll<string>(delegate(DoneItem x) { return x.Id; }).ToArray()) + ")", items);
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
            DoneItem pi = new DoneItem(); pi.Id = t.Id; pi.Pos = t.Short; pi.Pending = true;
            Done(t.Id + " vorgemerkt – der Admin nimmt die Nummer in die Liste auf", new List<DoneItem>(new[] { pi }));
            return true;
        }

        void UndoDone()
        {
            try
            {
                foreach (DoneItem i in done)
                {
                    string id = i.Id;
                    if (i.Pending)
                    {
                        Catalog fresh = store.GetCatalog(); fresh.Master.RemovePending(id); store.SaveCatalog(fresh); cat.Master.Pending = fresh.Master.Pending; notedIds.Remove(id);
                    }
                    else if (i.Added)
                    {
                        if (!i.HadDev) cat.Master.Devices.RemoveAll(delegate(EquipDevice x) { return string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase); });
                        if (i.HadPend != null && cat.Master.PendingFor(id) == null) cat.Master.Pending.Add(i.HadPend);
                        EquipPosition pos = cat.Position(i.Pos);
                        if (pos != null && !i.HadPos) pos.Ids.Remove(id);
                        store.SaveCatalog(cat);
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show(this, "Rückgängig machen ist fehlgeschlagen:\n" + ex.Message, "Equipment", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            doneTimer.Stop(); done = new List<DoneItem>(); doneText = ""; doneStep = null;
            if (cur != null) Bind(cur); else UpdateHints();
        }

#if TESTHOOKS
        internal EquipBar TestBar { get { return eqBar; } }
        internal string TestBarTarget { get { return barTarget != null ? barTarget.Id : ""; } }
#endif
    }
}

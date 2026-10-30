#if TESTHOOKS
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Arbeitsplan.Core;

namespace Arbeitsplan.Win
{
    // Testhaken fuer "Produkte & Equipment" (nur Testbuild).
    partial class ProductForm
    {
        public DialogResult TestAnswer = DialogResult.None;   // != None: Rueckfragen werden nicht angezeigt, sondern so beantwortet
        public string TestMsgs = "";                           // Texte der unterdrueckten Meldungen

        public void TestSelect(string code, string shortName)
        {
            FillProducts(cat.Product(code));
            foreach (object o in lbPos.Items) { PosItem pi = (PosItem)o; if (!pi.IsGroup && pi.Ps[0].Short == shortName) { lbPos.SelectedItem = pi; SelectPos(pi); break; } }
        }
        public void TestSelectGroup(string name) { foreach (object o in lbPos.Items) { PosItem pi = (PosItem)o; if (pi.IsGroup && pi.GroupName == name) { lbPos.SelectedItem = pi; SelectPos(pi); break; } } }
        public void TestAdd(string code, string name) { AddProduct(code, name); }
        public bool TestCopy(string to, string name) { ProductProfile c = prod != null ? cat.CopyProduct(prod.Code, to, name) : null; FillProducts(c); return c != null; }
        public bool TestEdit(string code, string name) { if (prod == null || ProductAsk.Check(cat, code, prod.Code).Length > 0) return false; ApplyEdit(code, name); return true; }
        public void TestRemove() { RemoveSelected(); }
        public string TestOwn(bool own, bool fix, params string[] ids)
        {
            SetOwn(own); SetFixed(fix);
            for (int i = 0; i < devs.Items.Count; i++) { DevItem d = devs.Items[i] as DevItem; if (d != null) devs.SetItemChecked(i, Array.IndexOf(ids, d.Id) >= 0); }
            return string.Join(",", cat.Allowed(prod.Code, pos.Short).ToArray());
        }
        public void TestCheckGroup(int index) { devs.SetItemChecked(index, !devs.GetItemChecked(index)); }
        public void TestSwap(string name, params string[] ids) { NewSwap(); swName.Text = name; for (int i = 0; i < swMembers.Items.Count; i++) swMembers.SetItemChecked(i, Array.IndexOf(ids, ((SwItem)swMembers.Items[i]).Id) >= 0); }
        public void TestSwapToggle(string id, bool on) { for (int i = 0; i < swMembers.Items.Count; i++) if (((SwItem)swMembers.Items[i]).Id == id) swMembers.SetItemChecked(i, on); }
        public string TestSwapMembers { get { List<string> r = new List<string>(); foreach (object o in swMembers.Items) r.Add(o.ToString()); return string.Join("|", r.ToArray()); } }
        public bool TestCommit() { Commit(); return Result != null; }
        public Catalog TestCatalog { get { return cat; } }
        public string TestPufButton { get { return bPuf.Text + "|" + bPuf.Enabled; } }
        public int TestChanges { get { return changes; } }
        public string TestStatus { get { return status.Text; } }
        public string TestHead { get { return head.Text; } }
        public void TestShowPos() { tabs.SelectedTab = tabPos; }
        public void TestNoAsk() { noAsk = true; }
        public bool TestNeedsAsk { get { return NeedsAsk(CloseReason.None); } }
        public bool TestNeedsAskFor(CloseReason r) { return NeedsAsk(r); }
        public string TestPosList { get { List<string> r = new List<string>(); foreach (object o in lbPos.Items) r.Add(o.ToString()); return string.Join("\n", r.ToArray()); } }
        public string TestPosInfo { get { return posInfo.Text; } }
        public bool TestUnifyVisible { get { return unify.Visible; } }
        public bool TestPosLocked { get { return !rStd.Enabled && !rOwn.Enabled && !rFixed.Enabled && !rFree.Enabled && !devs.Enabled && !reset.Enabled && unify.Enabled && unify.Visible; } }
        public bool TestResetEnabled { get { return reset.Enabled; } }
        public void TestUnify() { Unify(); }
        public void TestApplyStandard(Catalog src) { ApplyStandard(src); }
        public string TestProductRow(int i) { ListViewItem it = lv.Items[i]; return it.SubItems[0].Text + "|" + it.SubItems[1].Text + "|" + it.SubItems[2].Text; }
        public bool TestNeedsAskAfterCancel() { DialogResult = DialogResult.Cancel; bool r = NeedsAsk(CloseReason.None); DialogResult = DialogResult.None; return r; }   // Abbrechen-Knopf/Esc: CloseReason.None
    }
}
#endif

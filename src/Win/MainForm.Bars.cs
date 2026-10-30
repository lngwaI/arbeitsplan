using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Arbeitsplan.Core;

// Nur .NET-4.0-APIs, C# 5 und Systemschrift (Windows-XP-Kompatibilitaet).
namespace Arbeitsplan.Win
{
    partial class MainForm : Form
    {
        readonly Button eqBtn = new Button();   // sichtbarer Knopf "Equipment-Liste" (N3), rechts in der unteren Leiste

        // ---------- Aufbau der Leisten ----------

        MenuStrip BuildMenu()
        {
            MenuStrip ms = new MenuStrip();
            bool exp = me.Has(Perm.Export), pkg = me.Has(Perm.ImportPackage) || exp;
            bool structure = me.Has(Perm.EditNames) || me.Has(Perm.EditHeight) || me.Has(Perm.SortRows);
            bool usr = me.Has(Perm.UserCreate) || me.Has(Perm.UserEdit) || me.Has(Perm.UserDelete);

            ToolStripMenuItem file = new ToolStripMenuItem("Woche");
            file.DropDownItems.Add("Meine Schichten…", null, delegate { ShowPersons(); });
            file.DropDownItems.Add("Offene Pflichten dieser Woche…", null, delegate { ShowOpenDuties(); });
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add("Als Bild speichern (PNG)…", null, delegate { ExportImage(false); }).Enabled = exp;
            file.DropDownItems.Add("Als PDF speichern…", null, delegate { ExportImage(true); }).Enabled = exp;
            file.DropDownItems.Add("In Zwischenablage kopieren", null, delegate { ExportClipboard(); }).Enabled = exp;
            file.DropDownItems.Add("Als Paket speichern (.apkg)…", null, delegate { ExportPackage(); }).Enabled = exp;
            file.DropDownItems.Add("Equipment-Liste exportieren…", null, delegate { ExportEquipList(); }).Enabled = exp;
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add("Als Plan-Datei speichern (.applan)…", null, delegate { SavePlanFile(); }).Enabled = exp;
            file.DropDownItems.Add("Plan-Datei ansehen…", null, delegate { OpenPlanFile(); });

            ToolStripMenuItem pk = new ToolStripMenuItem("Pakete");
            pk.DropDownItems.Add("Neue Kampagne…", null, delegate { NewCampaign(); }).Enabled = me.Has(Perm.ImportPackage);
            pk.DropDownItems.Add("Kampagne starten…", null, delegate { StartCampaignFromFile(); }).Enabled = me.Has(Perm.ImportPackage);
            pk.DropDownItems.Add(new ToolStripSeparator());
            pk.DropDownItems.Add("Neues Paket anlegen…", null, delegate { OpenPackageEditor(null); });
            pk.DropDownItems.Add("Paket öffnen und bearbeiten…", null, delegate { ImportPackageFile(); });
            pk.Enabled = pkg && shownPlan == null;

            ToolStripMenuItem set = new ToolStripMenuItem("Einstellungen");
            ToolStripItem st = set.DropDownItems.Add("Blockstruktur…", null, delegate { EditStructure(); }); st.Enabled = structure;
            ToolStripItem us = set.DropDownItems.Add("Nutzerverwaltung…", null, delegate { RefreshUsers(); using (UserAdminForm f = new UserAdminForm(users, me)) { f.ShowDialog(this); SaveUsers(f.Changes); } }); us.Enabled = usr;
            ToolStripItem cf = set.DropDownItems.Add("Equipment && Bausteine…", null, delegate { EditCatalog(); }); cf.Enabled = me.Has(Perm.PackageAdmin);
            ToolStripItem mf = set.DropDownItems.Add("Equipment-Stammdaten…", null, delegate { EditMaster(); }); mf.Enabled = me.Has(Perm.PackageAdmin);
            ToolStripItem du = set.DropDownItems.Add("Pflichtpunkte && Feiertage…", null, delegate { EditDuties(); }); du.Enabled = me.Has(Perm.PackageAdmin);
            ToolStripItem pf = set.DropDownItems.Add("Produkte && Equipment…", null, delegate { EditProducts(); }); pf.Enabled = me.Has(Perm.PackageAdmin);
            set.DropDownItems.Add("Pufferpakete…", null, delegate { EditPufferPacks(); });   // ohne PackageAdmin nur ansehen
            ToolStripItem bk = set.DropDownItems.Add("Sicherung…", null, delegate { OpenBackup(); }); bk.Enabled = me.Has(Perm.PackageAdmin);
            set.Visible = structure || usr || me.Has(Perm.PackageAdmin);

            ToolStripMenuItem help = new ToolStripMenuItem("Hilfe");
            miNotices = (ToolStripMenuItem)help.DropDownItems.Add("Benachrichtigungen…", null, delegate { ShowNotices(); });
            miNotices.Enabled = shownPlan == null;
#if DEMO
            AddDemoMenu(help);   // Rolle wechseln, Demo zuruecksetzen (keine Verknuepfung)
#else
            help.DropDownItems.Add("Verknüpfung prüfen / anlegen…", null, delegate { Shortcut.Check(this); });
#endif
            help.DropDownItems.Add("Info…", null, delegate { ShowInfo(); });

            ms.Items.AddRange(new ToolStripItem[] { file, pk, set, help });
#if TESTHOOKS
            ToolStripMenuItem av = new ToolStripMenuItem("Ansicht (Test)");
            ToolStripMenuItem w2 = new ToolStripMenuItem("2 Wochen"); w2.CheckOnClick = true; w2.Checked = TestWeeks == 2;
            w2.CheckedChanged += delegate { SetWeeks(w2.Checked ? 2 : 1); };
            av.DropDownItems.Add(w2);
            ms.Items.Add(av);
#endif
            ms.Dock = DockStyle.Top;
            return ms;
        }

        void BuildTopBar()
        {
            weekLabel.Dock = DockStyle.Fill;
            weekLabel.TextAlign = ContentAlignment.MiddleCenter;
            weekLabel.Font = new Font(Font.FontFamily, 12f, FontStyle.Bold);

            // rechts: Suche in Inhalt/Equipment/Kuerzel (nicht Raumnamen)
            searchLabel.Text = "Suchen:"; searchLabel.AutoSize = true; searchLabel.Location = new Point(0, 9);
            search.Width = 130; search.Location = new Point(searchLabel.PreferredWidth + 6, 6);
            hitsLabel.AutoSize = true; hitsLabel.Location = new Point(search.Right + 6, 9);
            search.TextChanged += delegate { OnSearchChanged(); };
            Ui.SimpleMenu(search);
            search.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { NextHit(); e.SuppressKeyPress = true; } };
            Panel sp = new Panel();
            sp.Dock = DockStyle.Right; sp.Width = search.Right + 130;
            sp.Controls.Add(searchLabel); sp.Controls.Add(search); sp.Controls.Add(hitsLabel);

            topBar.Dock = DockStyle.Top; topBar.Height = Theme.Current.BarH;
            topBar.Controls.Add(weekLabel);
            topBar.Controls.Add(sp);
        }

        void BuildBottomBar()
        {
            bar.Dock = DockStyle.Bottom;
            bar.Height = Theme.Current.ButtonH + 18;
            nav.AutoSize = true; nav.WrapContents = false; nav.Top = 6;
            AddNav("«  Woche", delegate { Go(-7); });
            AddNav("‹  Tag", delegate { Go(-1); });
            AddNav("Heute", delegate { Go(MondayOf(DateTime.Today) - start); });
            AddNav("Tag  ›", delegate { Go(1); });
            AddNav("Woche  »", delegate { Go(7); });
            bar.Controls.Add(nav);
            eqBtn.Text = "Equipment-Liste"; eqBtn.AutoSize = true; eqBtn.MinimumSize = new Size(110, Theme.Current.ButtonH + 4); eqBtn.Enabled = me.Has(Perm.Export);
            eqBtn.Click += delegate { ExportEquipList(); };
            Theme.Current.StyleButton(eqBtn);
            ToolTip eqTip = new ToolTip(); eqTip.SetToolTip(eqBtn, "Equipment-Nummern eines Tags, Zeitraums oder einer Kampagne auflisten (kopieren / als .txt), optional mit Anzahl");
            bar.Controls.Add(eqBtn);
            bar.Resize += delegate { CenterBar(); };
            Shown += delegate { CenterBar(); };

            // Statusleiste: links Nutzer · Rolle · Kuerzel, rechts Datenstand (Status in MainForm.Storage.cs)
            statusBar.Dock = DockStyle.Bottom; statusBar.Height = Font.Height + 8;
            statusBar.Paint += delegate(object s, PaintEventArgs e) { using (Pen p = new Pen(Theme.Current.ButtonBorder)) e.Graphics.DrawLine(p, 0, 0, statusBar.Width, 0); };
            foreach (Label l in new[] { statusLeft, statusRight })
            {
                l.AutoSize = true; l.Padding = new Padding(8, 3, 8, 0); l.Dock = l == statusLeft ? DockStyle.Left : DockStyle.Right;
                statusBar.Controls.Add(l);
            }
            statusLeft.Text = me.Display + "  ·  " + users.RoleLabel(me) + (me.Kuerzel.Length > 0 ? "  ·  " + me.Kuerzel : "");
            ToolTip roleTip = new ToolTip(); roleTip.AutoPopDelay = 20000; roleTip.SetToolTip(statusLeft, users.RoleTooltip(me));
            Status(fstore != null ? "Aktualisiert " + DateTime.Now.ToString("HH:mm") : null, false);
        }

        void AddNav(string text, EventHandler click)
        {
            Button b = new Button();
            b.Text = text; b.AutoSize = true; b.MinimumSize = new Size(86, Theme.Current.ButtonH + 4); b.Margin = new Padding(3, 0, 3, 0);
            b.Click += click;
            Theme.Current.StyleButton(b);
            nav.Controls.Add(b);
        }

        void CenterBar() { nav.Left = Math.Max(10, (bar.ClientSize.Width - nav.Width) / 2); eqBtn.Top = nav.Top; eqBtn.Left = Math.Max(nav.Right + 10, bar.ClientSize.Width - eqBtn.Width - 12); }
    }
}

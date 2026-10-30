using System;
using System.Collections.Generic;
using System.Text;

namespace Arbeitsplan.Core
{
    // "Packen (Back-Up)": Reservetag am Tag nach dem Packen, im Paket an den Packen-Schritt gekoppelt (PackageStep.BackupOf = Source des
    // Packen-Schritts, XML "backup", Paketformat 6; fehlt bei alten Paketen). Der Back-Up folgt dem Packen-Schritt (Tag +1) und kann nicht
    // ohne ihn existieren; wird Packen entfernt, geht sein Back-Up mit (Paket-Editor, eine Rueckfrage).
    public partial class PackageStep
    {
        public string BackupOf = "";        // Source des Packen-Schritts, zu dem dieser Schritt der Back-Up ist; "" = normaler Schritt

        public bool IsBackup { get { return BackupOf.Length > 0; } }

        // true, wenn dieser Schritt der Back-Up von m ist
        public bool BacksUp(PackageStep m)
        {
            return m != null && m != this && BackupOf.Length > 0 && m.BackupOf.Length == 0 && string.Equals(BackupOf, m.Source, StringComparison.OrdinalIgnoreCase);
        }

        string DaysKey() { List<int> d = Days(); return string.Join(",", d.ConvertAll<string>(delegate(int x) { return x.ToString(); }).ToArray()); }

        // Zeitplan von m uebernehmen, einen Tag spaeter. true = hat sich geaendert.
        internal bool FollowMaster(PackageStep m)
        {
            string before = DaysKey() + "|" + Count + "|" + Repeat;
            Repeat = m.Repeat; WeekDays = m.WeekDays; DayList = m.DayList; Count = m.Count; Interval = m.Interval; Until = m.Until; Day = m.Day;
            Shift = new Dictionary<int, int>(m.Shift);
            Move = new Dictionary<int, int>(m.Move);
            MoveAll(1);
            return before != DaysKey() + "|" + Count + "|" + Repeat;
        }
    }

    public partial class Package
    {
        public const string PackenName = "Packen", BackupName = "Packen (Back-Up)";

        // Packen-Schritt, zu dem b der Back-Up ist; null = keiner
        public static PackageStep MasterOf(List<PackageStep> steps, PackageStep b)
        {
            foreach (PackageStep s in steps) if (b.BacksUp(s)) return s;
            return null;
        }

        public static bool HasBackup(List<PackageStep> steps, PackageStep m)
        {
            return steps.Exists(delegate(PackageStep s) { return s.BacksUp(m); });
        }

        // Back-Up ohne Packen-Schritt (z. B. Packen entfernt oder umbenannt); null = in Ordnung
        public static string BackupProblem(List<PackageStep> steps)
        {
            foreach (PackageStep s in steps)
                if (s.IsBackup && MasterOf(steps, s) == null)
                    return "\"" + (s.Title.Length > 0 ? s.Title : BackupName) + "\" gehört zu \"" + s.BackupOf + "\", dieser Schritt fehlt. Back-Up und Packen lassen sich nur zusammen ändern.";
            return null;
        }

        // Back-Ups auf Tag +1 ihres Packen-Schritts bringen; true = mindestens einer wurde verschoben
        public static bool SyncBackups(List<PackageStep> steps)
        {
            bool any = false;
            foreach (PackageStep b in steps)
            {
                if (!b.IsBackup) continue;
                PackageStep m = MasterOf(steps, b);
                if (m != null && b.FollowMaster(m)) any = true;
            }
            return any;
        }

        // Neuer Back-Up-Schritt zu m: gleicher Raum, Tag +1, Equipment = Ausweich-Geraete aus m (danach frei aenderbar).
        // proto = Baustein "Packen (Back-Up)" (Text), darf null sein.
        public static PackageStep MakeBackup(PackageStep m, Baustein proto)
        {
            PackageStep b = new PackageStep();
            b.Title = BackupName; b.Source = BackupName; b.BackupOf = m.Source; b.Row = m.Row; b.Text = proto != null ? proto.Text : "";
            foreach (EquipAssign a in m.Equipment)
            {
                EquipAssign n = new EquipAssign(); n.Short = a.Short;
                if (a.Fallback.Length > 0) n.Choices.Add(new EquipChoice(a.Short, a.Fallback));
                b.Equipment.Add(n);
            }
            b.FollowMaster(m);
            return b;
        }
    }
}

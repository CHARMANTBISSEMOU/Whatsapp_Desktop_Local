using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using P2PChat.App.Profile;
using P2PChat.App.Theme;

namespace P2PChat.App.UI
{
    /// <summary>
    /// Page « Paramètres » (spec, Page K) : Apparence (thème, taille du texte),
    /// Notifications, Confidentialité, Stockage (tailles, vider l'historique,
    /// ouvrir le dossier des données) et À propos — navigation cohérente (nav 6),
    /// pas seulement une boîte de dialogue.
    /// </summary>
    public sealed partial class MainForm
    {
        private const int SetRowTheme = 0;
        private const int SetRowFont = 1;
        private const int SetRowSound = 2;
        private const int SetRowFlash = 3;
        private const int SetRowPreview = 4;
        private const int SetRowReceipts = 5;
        private const int SetRowBlocked = 6;
        private const int SetRowHistorySize = 7;
        private const int SetRowTransfersSize = 8;
        private const int SetRowClear = 9;
        private const int SetRowOpenDir = 10;
        private const int SetRowAbout = 11;
        private const int SetRowCount = 12;

        private string _histSizeCache = "";
        private string _trSizeCache = "";
        private long _sizeCacheAt;

        /// <summary>Ouvre la page Paramètres (navigation, comme les autres pages).</summary>
        internal void ActivateSettingsPage()
        {
            _activeNav = 6;
            _sizeCacheAt = 0; // force le recalcul des tailles
            RelayoutControls();
            Invalidate();
        }

        internal Rectangle SettingsPageRect()
        {
            int rail = NavigationRailWidth();
            return new Rectangle(rail, 0, Math.Max(0, ClientSize.Width - rail), ClientSize.Height);
        }

        private Rectangle SettingsRowRect(int row)
        {
            Rectangle page = SettingsPageRect();
            return new Rectangle(page.X + WaTheme.S(40), page.Y + WaTheme.S(92) + row * WaTheme.S(56),
                page.Width - WaTheme.S(80), WaTheme.S(44));
        }

        private Rectangle SettingsThemeChipRect(int chip)
        {
            Rectangle row = SettingsRowRect(SetRowTheme);
            int w = WaTheme.S(92), h = WaTheme.S(32);
            int x = row.Right - (3 - chip) * (w + WaTheme.S(8));
            return new Rectangle(x, row.Y + (row.Height - h) / 2, w, h);
        }

        private static string SettingsSectionFor(int row)
        {
            switch (row)
            {
                case SetRowTheme: return "APPARENCE";
                case SetRowSound: return "NOTIFICATIONS";
                case SetRowReceipts: return "CONFIDENTIALITÉ";
                case SetRowHistorySize: return "STOCKAGE";
                case SetRowAbout: return "À PROPOS";
                default: return null;
            }
        }

        internal void PaintSettingsPage(Graphics g)
        {
            Rectangle page = SettingsPageRect();
            using (SolidBrush bg = new SolidBrush(WaTheme.EmptyBg)) g.FillRectangle(bg, page);
            RefreshSizeCache();

            using (Font f = WaTheme.Semi(20f))
            using (SolidBrush tb = new SolidBrush(WaTheme.TextDark))
                g.DrawString("Paramètres", f, tb, page.X + WaTheme.S(40), page.Y + WaTheme.S(24));
            using (Font f = WaTheme.Text(9.6f))
            using (SolidBrush tb = new SolidBrush(WaTheme.TextGray))
                g.DrawString("Messagerie P2P locale, chiffrée de bout en bout", f, tb,
                    page.X + WaTheme.S(40), page.Y + WaTheme.S(60));

            Rectangle st = SettingsPageRect();
            GraphicsState clip = g.Save();
            g.SetClip(new Rectangle(page.X, page.Y, page.Width, page.Height));
            for (int row = 0; row < SetRowCount; row++)
            {
                Rectangle r = SettingsRowRect(row);
                if (r.Bottom > page.Bottom) break;
                string section = SettingsSectionFor(row);
                if (section != null)
                {
                    using (Font f = WaTheme.Semi(8.8f))
                    using (SolidBrush tb = new SolidBrush(WaTheme.Accent))
                        g.DrawString(section, f, tb, r.X, r.Y - WaTheme.S(18));
                }
                PaintSettingsRow(g, row, r);
            }
            g.Restore(clip);
        }

        private void PaintSettingsRow(Graphics g, int row, Rectangle r)
        {
            if (row == SetRowClear || row == SetRowOpenDir)
            {
                bool danger = row == SetRowClear;
                using (GraphicsPath p = WaTheme.RoundRect(r.X, r.Y, r.Width, WaTheme.S(40), WaTheme.S(8)))
                using (SolidBrush b = new SolidBrush(WaTheme.MenuBg))
                {
                    g.FillPath(b, p);
                    using (Pen pen = new Pen(danger ? Color.FromArgb(0xF1, 0x5C, 0x6D) : WaTheme.Accent, WaTheme.S(1.4f)))
                        g.DrawPath(pen, p);
                }
                using (Font f = WaTheme.Semi(9.6f))
                using (SolidBrush tb = new SolidBrush(danger ? Color.FromArgb(0xF1, 0x5C, 0x6D) : WaTheme.Accent))
                {
                    string t = danger ? "Vider tout l'historique" : "Ouvrir le dossier des données";
                    SizeF sz = g.MeasureString(t, f);
                    g.DrawString(t, f, tb, r.X + (r.Width - sz.Width) / 2f, r.Y + (WaTheme.S(40) - sz.Height) / 2f);
                }
                return;
            }

            if (row == SetRowTheme)
            {
                string[] names = new string[] { "Système", "Clair", "Sombre" };
                for (int i = 0; i < 3; i++)
                {
                    Rectangle chip = SettingsThemeChipRect(i);
                    bool on = Cfg.ThemeModeInt == i;
                    using (SolidBrush b = new SolidBrush(on ? WaTheme.Accent : WaTheme.SearchBg))
                    using (GraphicsPath p = WaTheme.RoundRect(chip.X, chip.Y, chip.Width, chip.Height, WaTheme.S(16)))
                        g.FillPath(b, p);
                    using (Font f = WaTheme.Text(8.8f))
                    using (SolidBrush tb = new SolidBrush(on ? Color.White : WaTheme.TextDark))
                    {
                        SizeF sz = g.MeasureString(names[i], f);
                        g.DrawString(names[i], f, tb, chip.X + (chip.Width - sz.Width) / 2f,
                            chip.Y + (chip.Height - sz.Height) / 2f);
                    }
                }
                return;
            }

            string label;
            switch (row)
            {
                case SetRowFont: label = "Taille du texte"; break;
                case SetRowSound: label = "Son des notifications"; break;
                case SetRowFlash: label = "Clignotement de la barre des tâches"; break;
                case SetRowPreview: label = "Aperçu des messages"; break;
                case SetRowReceipts: label = "Accusés de lecture"; break;
                case SetRowBlocked: label = "Contacts bloqués"; break;
                case SetRowHistorySize: label = "Historique des discussions"; break;
                case SetRowTransfersSize: label = "Fichiers reçus (vocaux, photos…)"; break;
                case SetRowAbout: label = "Version"; break;
                default: label = ""; break;
            }
            using (Font f = WaTheme.Text(10.2f))
            using (SolidBrush tb = new SolidBrush(WaTheme.TextDark))
                g.DrawString(label, f, tb, r.X, r.Y + WaTheme.S(10));

            switch (row)
            {
                case SetRowFont: SettingsValue(g, r, SettingsFontLabel() + "  ›"); break;
                case SetRowSound: DrawSettingsSwitch(g, r, Cfg.NotifSound); break;
                case SetRowFlash: DrawSettingsSwitch(g, r, Cfg.NotifFlash); break;
                case SetRowPreview: DrawSettingsSwitch(g, r, Cfg.NotifPreview); break;
                case SetRowReceipts: DrawSettingsSwitch(g, r, Cfg.ReadReceipts); break;
                case SetRowBlocked: SettingsValue(g, r, Cfg.BlockedIds.Count + " contact(s)"); break;
                case SetRowHistorySize: SettingsValue(g, r, _histSizeCache); break;
                case SetRowTransfersSize: SettingsValue(g, r, _trSizeCache); break;
                case SetRowAbout: SettingsValue(g, r, "P2PChat 1.0 — projet scolaire"); break;
            }
        }

        private string SettingsFontLabel()
        {
            string[] names = new string[] { "Petite", "Normale", "Grande", "Très grande" };
            int i = Cfg.FontStepInt;
            if (i < 0) i = 0;
            if (i > 3) i = 3;
            return names[i];
        }

        private static void SettingsValue(Graphics g, Rectangle r, string text)
        {
            using (Font f = WaTheme.Text(9.4f))
            using (SolidBrush tb = new SolidBrush(WaTheme.TextGray))
            {
                SizeF sz = g.MeasureString(text, f);
                g.DrawString(text, f, tb, r.Right - sz.Width, r.Y + (r.Height - sz.Height) / 2f);
            }
        }

        private static void DrawSettingsSwitch(Graphics g, Rectangle r, bool on)
        {
            int w = WaTheme.S(42), h = WaTheme.S(23);
            int x = r.Right - w, y = r.Y + (r.Height - h) / 2;
            using (GraphicsPath p = WaTheme.RoundRect(x, y, w, h, h / 2f))
            using (SolidBrush b = new SolidBrush(on ? WaTheme.Accent : WaTheme.Divider))
                g.FillPath(b, p);
            int d = h - WaTheme.S(4);
            int kx = on ? x + w - d - WaTheme.S(2) : x + WaTheme.S(2);
            using (SolidBrush kb = new SolidBrush(Color.White))
                g.FillEllipse(kb, kx, y + WaTheme.S(2), d, d);
        }

        // ------------------------------------------------------------ interactions
        private void SettingsMouseDown(Point p)
        {
            Rectangle page = SettingsPageRect();
            if (!page.Contains(p)) return;

            for (int i = 0; i < 3; i++)
            {
                if (SettingsThemeChipRect(i).Contains(p))
                {
                    Cfg.ThemeModeInt = i;
                    Cfg.Save();
                    ApplyThemeChange();
                    return;
                }
            }

            for (int row = 0; row < SetRowCount; row++)
            {
                if (!SettingsRowRect(row).Contains(p)) continue;
                switch (row)
                {
                    case SetRowFont:
                        Cfg.FontStepInt = (Cfg.FontStepInt + 1) % 4;
                        Cfg.Save();
                        ApplyThemeChange();
                        Invalidate();
                        return;
                    case SetRowSound:
                        Cfg.NotifSound = !Cfg.NotifSound; Cfg.Save(); Invalidate(); return;
                    case SetRowFlash:
                        Cfg.NotifFlash = !Cfg.NotifFlash; Cfg.Save(); Invalidate(); return;
                    case SetRowPreview:
                        Cfg.NotifPreview = !Cfg.NotifPreview; Cfg.Save(); Invalidate(); return;
                    case SetRowReceipts:
                        Cfg.ReadReceipts = !Cfg.ReadReceipts; Cfg.Save(); Invalidate(); return;
                    case SetRowBlocked:
                        _toasts.Show(Cfg.BlockedIds.Count + " contact(s) bloqué(s).");
                        return;
                    case SetRowHistorySize:
                    case SetRowTransfersSize:
                        _sizeCacheAt = 0; RefreshSizeCache(); Invalidate(); return;
                    case SetRowClear:
                        if (MessageBox.Show(this, "Effacer tout l'historique des discussions ?",
                            "P2PChat", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                        {
                            try { _history.ClearAll(); } catch { }
                            foreach (KeyValuePair<string, Conversation> kv in _convs)
                            {
                                kv.Value.Messages.Clear();
                                kv.Value.Layouts.Clear();
                                kv.Value.ContentH = 0;
                            }
                            RelayoutChat(false);
                            RebuildOrder();
                            Invalidate();
                            _toasts.Show("Historique effacé.");
                        }
                        return;
                    case SetRowOpenDir:
                        try { Process.Start(ProfileStore.AppDataDir); }
                        catch { _toasts.Show("Impossible d'ouvrir le dossier."); }
                        return;
                }
            }
        }

        private void RefreshSizeCache()
        {
            long now = Environment.TickCount;
            if (_sizeCacheAt != 0 && now - _sizeCacheAt < 5000) return;
            _sizeCacheAt = now;
            string root = ProfileStore.AppDataDir;
            _histSizeCache = WaTheme.FormatSize(DirSize(Path.Combine(root, "historique")));
            _trSizeCache = WaTheme.FormatSize(DirSize(Path.Combine(root, "transfers")));
        }

        private static long DirSize(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return 0;
                long total = 0;
                foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try { total += new FileInfo(f).Length; }
                    catch { }
                }
                return total;
            }
            catch { return 0; }
        }
    }
}
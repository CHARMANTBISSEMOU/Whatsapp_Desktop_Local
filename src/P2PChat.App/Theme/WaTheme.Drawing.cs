using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using P2PChat.Core.Protocol;

namespace P2PChat.App.Theme
{
    /// <summary>
    /// Design System WhatsApp — partie 2 : avatars (photo ou initiales),
    /// coques de statut vectorielles (✗ horloge ✓ ✓✓ ✓✓bleues),
    /// formats de dates/heures exacts de WhatsApp (HH:mm / HIER / 12:34 / 05/03/2026).
    /// </summary>
    public static partial class WaTheme
    {
        private static readonly ImageAttributes CircularWrap;

        static WaTheme()
        {
            CircularWrap = new ImageAttributes();
            CircularWrap.SetWrapMode(WrapMode.TileFlipXY);
        }

        // ---------------------------------------------------------- avatars
        /// <summary>Palette des avatars à initiales (nuances calmes type WhatsApp).</summary>
        private static readonly int[] AvatarHues = new int[]
        {
            4, 18, 32, 173, 358, 172, 330, 256, 12, 205, 142, 174, 45, 200, 95, 24, 290, 315
        };

        /// <summary>Couleur stable dérivée du nom (teinte = hash du pseudo).</summary>
        public static Color ColorFromName(string name)
        {
            int h = 0;
            if (name != null)
            {
                foreach (char c in name) { unchecked { h = h * 31 + c; } }
            }
            if (h < 0) h = -h;
            int hue = AvatarHues[h % AvatarHues.Length];
            double sat = 0.32, lig = Dark ? 0.38 : 0.52;
            return FromHsl(hue, sat, lig);
        }

        private static Color FromHsl(double hue, double sat, double lig)
        {
            double q = lig < 0.5 ? lig * (1 + sat) : lig + sat - lig * sat;
            double p = 2 * lig - q;
            double r = Hue2Rgb(p, q, hue / 360.0 + 1.0 / 3.0);
            double g = Hue2Rgb(p, q, hue / 360.0);
            double b = Hue2Rgb(p, q, hue / 360.0 - 1.0 / 3.0);
            return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        private static double Hue2Rgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 0.5) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
        }

        /// <summary>Initiales (2 lettres max.) du pseudo.</summary>
        public static string Initials(string pseudo)
        {
            if (pseudo == null) return "?";
            string t = pseudo.Trim();
            if (t.Length == 0) return "?";
            string[] parts = t.Split(' ');
            if (parts.Length >= 2 && parts[parts.Length - 1].Length > 0)
            {
                return (parts[0].Substring(0, 1) + parts[parts.Length - 1].Substring(0, 1)).ToUpperInvariant();
            }
            return t.Substring(0, 1).ToUpperInvariant();
        }

        /// <summary>Avatar circulaire : photo si fournie (charger via LoadRoundPhoto), sinon initiales.</summary>
        public static void DrawAvatar(Graphics g, Rectangle r, string pseudo, Image photo, bool onlineDot)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (photo != null)
            {
                DrawRoundImage(g, photo, r);
            }
            else
            {
                using (SolidBrush b = new SolidBrush(ColorFromName(pseudo)))
                {
                    g.FillEllipse(b, r);
                }
                string inits = Initials(pseudo);
                using (SolidBrush tb = new SolidBrush(Color.White))
                using (Font f = Semi(Math.Max(8f, r.Height / Dpi * 0.38f)))
                {
                    SizeF s = g.MeasureString(inits, f);
                    g.DrawString(inits, f, tb,
                        r.X + (r.Width - s.Width) / 2f - 1,
                        r.Y + (r.Height - s.Height) / 2f - (float)(2 * Dpi));
                }
            }
            if (onlineDot && r.Width > S(30))
            {
                int dot = S(9);
                Rectangle dr = new Rectangle(r.Right - dot, r.Bottom - dot, dot, dot);
                using (SolidBrush ring = new SolidBrush(Dark ? SidebarBg : Color.White))
                {
                    g.FillEllipse(ring, dr.X - 1, dr.Y - 1, dot + 2, dot + 2);
                }
                using (SolidBrush dotb = new SolidBrush(Accent))
                {
                    g.FillEllipse(dotb, dr);
                }
            }
        }

        public static void DrawAvatar(Graphics g, Rectangle r, string pseudo, Image photo)
        {
            DrawAvatar(g, r, pseudo, photo, false);
        }

        /// <summary>Dévelope une image en cercle anti-aliasé.</summary>
        public static void DrawRoundImage(Graphics g, Image img, Rectangle r)
        {
            GraphicsState state = g.Save();
            using (GraphicsPath clip = new GraphicsPath())
            {
                clip.AddEllipse(r);
                g.SetClip(clip);
                double ratio = Math.Max((double)r.Width / img.Width, (double)r.Height / img.Height);
                int w = (int)Math.Ceiling(img.Width * ratio);
                int h = (int)Math.Ceiling(img.Height * ratio);
                Rectangle dst = new Rectangle(r.X - (w - r.Width) / 2, r.Y - (h - r.Height) / 2, w, h);
                g.DrawImage(img, dst, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, CircularWrap);
            }
            g.Restore(state);
        }

        /// <summary>Développe une image en rectangle à coins arrondis (miniatures photos).</summary>
        public static void DrawRoundedImage(Graphics g, Image img, Rectangle r, float radius)
        {
            if (img == null || r.Width <= 0 || r.Height <= 0) return;
            GraphicsState state = g.Save();
            using (GraphicsPath clip = RoundRect(r.X, r.Y, r.Width, r.Height, radius))
            {
                g.SetClip(clip);
                double ratio = Math.Max((double)r.Width / img.Width, (double)r.Height / img.Height);
                int w = (int)Math.Ceiling(img.Width * ratio);
                int h = (int)Math.Ceiling(img.Height * ratio);
                Rectangle dst = new Rectangle(r.X - (w - r.Width) / 2, r.Y - (h - r.Height) / 2, w, h);
                g.DrawImage(img, dst, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, CircularWrap);
            }
            g.Restore(state);
        }

        // --------------------------------------------------------------- ticks
        /// <summary>
        /// Coques de statut exactes de WhatsApp (l'icône d'horloge en attente,
        /// ✓ simple gris, ✓✓ gris distribué, ✓✓ bleues lues, ✗ rouge échec).
        /// </summary>
        public static void DrawChecks(Graphics g, float x, float y, DeliveryStatus st)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float u = Dpi; // unité de base 96dpi

            if (st == DeliveryStatus.Failed)
            {
                using (Pen pe = new Pen(Color.FromArgb(0xF1, 0x5C, 0x6D), 1.6f * u))
                {
                    pe.StartCap = LineCap.Round;
                    pe.EndCap = LineCap.Round;
                    g.DrawLine(pe, x + 1 * u, y + 1 * u, x + 9 * u, y + 9 * u);
                    g.DrawLine(pe, x + 9 * u, y + 1 * u, x + 1 * u, y + 9 * u);
                }
                return;
            }

            if (st == DeliveryStatus.Pending)
            {
                // petite horloge WhatsApp (cercle + aiguilles), gris
                Color c = Dark ? Color.FromArgb(0x86, 0x96, 0xA0) : Color.FromArgb(0x9E, 0xA5, 0xA9);
                using (Pen pe = new Pen(Color.FromArgb(200, c), 1.35f * u))
                {
                    pe.StartCap = LineCap.Round;
                    pe.EndCap = LineCap.Round;
                    g.DrawEllipse(pe, x + 0.5f * u, y + 0.5f * u, 9 * u, 9 * u);
                    g.DrawLine(pe, x + 5 * u, y + 3 * u, x + 5 * u, y + 5.5f * u);
                    g.DrawLine(pe, x + 5 * u, y + 5.5f * u, x + 7 * u, y + 6.5f * u);
                }
                return;
            }

            Color col = st == DeliveryStatus.Read ? ReadBlue
                : (Dark ? Color.FromArgb(0x86, 0x96, 0xA0) : Color.FromArgb(0x86, 0x96, 0xA0));
            using (Pen p = new Pen(col, 1.6f * u))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                if (st == DeliveryStatus.Sent)
                {
                    // coque unique, légèrement pivotée comme WhatsApp
                    DrawOneTick(g, p, x + 1 * u, y + 2 * u);
                }
                else
                {
                    DrawOneTick(g, p, x + 1 * u, y + 2 * u);
                    DrawOneTick(g, p, x + 5 * u, y + 2 * u);
                }
            }
        }

        private static void DrawOneTick(Graphics g, Pen p, float x, float y)
        {
            // forme exacte du check WhatsApp : descend, puis grande remontée
            g.DrawLine(p, x, y + 4.5f * Dpi, x + 3.5f * Dpi, y + 8 * Dpi);
            g.DrawLine(p, x + 3.5f * Dpi, y + 8 * Dpi, x + 9.5f * Dpi, y + 1.5f * Dpi);
        }

        /// <summary>Largeur approximative du dessin des coques (pour la méta interne).</summary>
        public static int ChecksWidth(DeliveryStatus st)
        {
            if (st == DeliveryStatus.Pending || st == DeliveryStatus.Failed) return S(11);
            return st == DeliveryStatus.Sent ? S(12) : S(16);
        }

        // ------------------------------------------------------------- formats
        private static DateTime EpochToLocal(long utcMs)
        {
            long normalized = ChatMessage.NormalizeTimestampUtcMs(utcMs);
            try { return DateTimeOffset.FromUnixTimeMilliseconds(normalized).LocalDateTime; }
            catch { return DateTimeOffset.FromUnixTimeMilliseconds(ChatMessage.NowUtcMs()).LocalDateTime; }
        }

        /// <summary>HH:mm local (bulles + liste).</summary>
        public static string FormatTime(long utcMs)
        {
            return EpochToLocal(utcMs).ToString("HH:mm");
        }

        /// <summary>Heure de la liste façon WhatsApp : HH:mm aujourd'hui, "hier", jour
        /// de la semaine cette semaine, sinon jj/mm/aaaa.</summary>
        public static string FormatListTime(long utcMs)
        {
            DateTime t = EpochToLocal(utcMs);
            DateTime today = DateTime.Today;
            DateTime yest = today.AddDays(-1);
            if (t.Date == today) return t.ToString("HH:mm");
            if (t.Date == yest) return "hier";
            if ((today - t.Date).TotalDays < 7) return t.ToString("dddd", new System.Globalization.CultureInfo("fr-FR"));
            return t.ToString("dd/MM/yyyy");
        }

        /// <summary>En-tête de section date : "AUJOURD'HUI", "HIER", jour long, ou date.</summary>
        public static string FormatDateHeader(long utcMs)
        {
            DateTime t = EpochToLocal(utcMs);
            DateTime today = DateTime.Today;
            DateTime yest = today.AddDays(-1);
            if (t.Date == today) return "AUJOURD'HUI";
            if (t.Date == yest) return "HIER";
            System.Globalization.CultureInfo fr = new System.Globalization.CultureInfo("fr-FR");
            if ((today - t.Date).TotalDays < 6)
            {
                return t.ToString("dddd", fr).ToUpperInvariant();
            }
            DateTime lastMonth = today.AddMonths(-1);
            if (t >= lastMonth)
            {
                return t.ToString("d MMMM", fr).ToUpperInvariant();
            }
            return t.ToString("dd/MM/yyyy");
        }

        /// <summary>Taille humaine (o / Ko / Mo) avec vraies unités françaises.</summary>
        public static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L) return Math.Round(bytes / 1073741824.0, 1) + " Go";
            if (bytes >= 1024 * 1024) return Math.Round(bytes / 1048576.0, 1) + " Mo";
            if (bytes >= 1024) return Math.Round(bytes / 1024.0, 0) + " Ko";
            return bytes + " o";
        }

        /// <summary>Durée vocale "0:07" façon WhatsApp.</summary>
        public static string FormatDur(int s)
        {
            if (s < 0) s = 0;
            return (s / 60) + ":" + (s % 60).ToString("D2");
        }
    }
}

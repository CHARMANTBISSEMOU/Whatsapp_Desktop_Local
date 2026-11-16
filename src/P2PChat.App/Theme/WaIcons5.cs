using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace P2PChat.App.Theme
{
    /// <summary>Iconothèque WhatsApp — partie 5 : caméra vidéo, personne, info, copier.</summary>
    public static partial class WaIcons
    {
        // ========================================================== VIDEOCAM 🎥
        public static void VideoCam(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.6f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawRectangle(p, U(x), U(y + s * 0.20f), U(s * 0.64f), U(s * 0.56f));
                using (GraphicsPath lens = new GraphicsPath())
                {
                    lens.AddLine(U(x + s * 0.64f), U(y + s * 0.36f), U(x + s * 0.94f), U(y + s * 0.20f));
                    lens.AddLine(U(x + s * 0.94f), U(y + s * 0.20f), U(x + s * 0.94f), U(y + s * 0.76f));
                    lens.AddLine(U(x + s * 0.94f), U(y + s * 0.76f), U(x + s * 0.64f), U(y + s * 0.60f));
                    lens.CloseFigure();
                    g.DrawPath(p, lens);
                }
            }
        }

        // ============================================================== PERSON 👤
        public static void Person(Graphics g, float x, float y, float s, Color c)
        {
            using (SolidBrush b = new SolidBrush(c))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(b, U(x + s * 0.24f), U(y + s * 0.06f), U(s * 0.52f), U(s * 0.52f));
                using (GraphicsPath body = new GraphicsPath())
                {
                    body.AddArc(U(x - s * 0.06f), U(y + s * 0.50f), U(s * 1.12f), U(s * 1.10f), 180, 180);
                    g.FillPath(b, body);
                }
            }
        }

        // ================================================================= INFO ℹ
        public static void Info(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.6f))
            using (SolidBrush b = new SolidBrush(c))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawEllipse(p, U(x + s * 0.04f), U(y + s * 0.04f), U(s * 0.92f), U(s * 0.92f));
                g.FillEllipse(b, U(x + s * 0.44f), U(y + s * 0.18f), U(s * 0.13f), U(s * 0.13f));
                g.DrawLine(p, U(x + s * 0.505f), U(y + s * 0.40f), U(x + s * 0.505f), U(y + s * 0.58f));
                g.DrawLine(p, U(x + s * 0.505f), U(y + s * 0.62f), U(x + s * 0.505f), U(y + s * 0.70f));
            }
        }

        // ================================================================ COPY ⧉
        public static void Copy(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.5f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawRectangle(p, U(x + s * 0.26f), U(y), U(s * 0.68f), U(s * 0.68f));
                g.DrawLine(p, U(x + s * 0.26f), U(y + s * 0.16f), U(x + s * 0.10f), U(y + s * 0.16f));
                g.DrawLine(p, U(x + s * 0.10f), U(y + s * 0.16f), U(x + s * 0.10f), U(y + s * 0.90f));
                g.DrawLine(p, U(x + s * 0.10f), U(y + s * 0.90f), U(x + s * 0.84f), U(y + s * 0.90f));
                g.DrawLine(p, U(x + s * 0.84f), U(y + s * 0.90f), U(x + s * 0.84f), U(y + s * 0.70f));
            }
        }
    }
}

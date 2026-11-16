using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace P2PChat.App.Theme
{
    /// <summary>Iconothèque WhatsApp — partie 3 : réponse, réaction, épingler,
    /// cloche sourdine, folio/document, photo, lecture, pause, téléphone, caméra, personne, info, copier.</summary>
    public static partial class WaIcons
    {
        // ================================================================ REPLY ↩
        public static void Reply(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.6f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawLine(p, U(x + s * 0.86f), U(y + s * 0.70f), U(x + s * 0.30f), U(y + s * 0.70f));
                using (GraphicsPath curve = new GraphicsPath())
                {
                    curve.AddLine(U(x + s * 0.30f), U(y + s * 0.70f), U(x + s * 0.30f), U(y + s * 0.34f));
                    curve.AddArc(U(x + s * 0.10f), U(y + s * 0.10f), U(s * 0.42f), U(s * 0.42f), 90, 180);
                    g.DrawPath(p, curve);
                }
                g.FillEllipse(new SolidBrush(c), U(x + s * 0.10f), U(y + s * 0.28f), U(s * 0.12f), U(s * 0.12f));
            }
        }

        // ======================================================= REACT (:+1 face)
        public static void React(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.5f))
            using (SolidBrush b = new SolidBrush(c))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                // tête smiley
                g.DrawEllipse(p, U(x + s * 0.06f), U(y + s * 0.06f), U(s * 0.52f), U(s * 0.52f));
                g.FillEllipse(b, U(x + s * 0.20f), U(y + s * 0.22f), U(s * 0.07f), U(s * 0.07f));
                g.FillEllipse(b, U(x + s * 0.38f), U(y + s * 0.22f), U(s * 0.07f), U(s * 0.07f));
                using (GraphicsPath smile = new GraphicsPath())
                {
                    smile.AddArc(U(x + s * 0.16f), U(y + s * 0.28f), U(s * 0.32f), U(s * 0.26f), 20, 140);
                    g.DrawPath(p, smile);
                }
                // petit +
                g.DrawLine(p, U(x + s * 0.72f), U(y + s * 0.50f), U(x + s * 0.94f), U(y + s * 0.50f));
                g.DrawLine(p, U(x + s * 0.83f), U(y + s * 0.39f), U(x + s * 0.83f), U(y + s * 0.61f));
            }
        }

        // ================================================================== PIN 📌
        public static void Pin(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.5f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = new GraphicsPath())
                {
                    // tête ovale inclinée
                    path.AddLine(U(x + s * 0.34f), U(y + s * 0.66f), U(x + s * 0.60f), U(y + s * 0.12f));
                    path.AddLine(U(x + s * 0.60f), U(y + s * 0.12f), U(x + s * 0.84f), U(y + s * 0.28f));
                    path.AddLine(U(x + s * 0.84f), U(y + s * 0.28f), U(x + s * 0.50f), U(y + s * 0.74f));
                    path.CloseFigure();
                    g.DrawPath(p, path);
                }
                g.DrawLine(p, U(x + s * 0.14f), U(y + s * 0.86f), U(x + s * 0.36f), U(y + s * 0.64f));
                g.DrawLine(p, U(x + s * 0.30f), U(y + s * 0.60f), U(x + s * 0.52f), U(y + s * 0.82f));
            }
        }

        // =========================================================== MUTE 🔔̶
        public static void BellMuted(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.5f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath bell = new GraphicsPath())
                {
                    bell.AddLine(U(x + s * 0.20f), U(y + s * 0.64f), U(x + s * 0.20f), U(y + s * 0.36f));
                    bell.AddArc(U(x + s * 0.20f), U(y + s * 0.12f), U(s * 0.60f), U(s * 0.55f), 180, 180);
                    bell.AddLine(U(x + s * 0.80f), U(y + s * 0.36f), U(x + s * 0.80f), U(y + s * 0.64f));
                    g.DrawPath(p, bell);
                }
                g.DrawLine(p, U(x + s * 0.12f), U(y + s * 0.64f), U(x + s * 0.88f), U(y + s * 0.64f));
                g.DrawLine(p, U(x + s * 0.42f), U(y + s * 0.76f), U(x + s * 0.58f), U(y + s * 0.76f));
                // barre diagonale de sourdine
                g.DrawLine(p, U(x + s * 0.14f), U(y + s * 0.80f), U(x + s * 0.86f), U(y + s * 0.14f));
            }
        }

        // ================================================================= DOC 📄
        public static void Doc(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.5f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath page = new GraphicsPath())
                {
                    float w = s * 0.72f, h = s;
                    PointF[] pts = new PointF[]
                    {
                        new PointF(U(x), U(y)),
                        new PointF(U(x + w * 0.66f), U(y)),
                        new PointF(U(x + w), U(y + h * 0.22f)),
                        new PointF(U(x + w), U(y + h * 0.92f)),
                        new PointF(U(x + w * 0.86f), U(y + h)),
                        new PointF(U(x), U(y + h)),
                    };
                    page.AddPolygon(pts);
                    page.CloseFigure();
                    g.DrawPath(p, page);
                }
                g.DrawLine(p, U(x + w0(s) * 0.66f), U(y), U(x + w0(s) * 0.66f), U(y + s * 0.22f));
                g.DrawLine(p, U(x + w0(s) * 0.66f), U(y + s * 0.22f), U(x + w0(s)), U(y + s * 0.22f));
            }
        }

        private static float w0(float s) { return s * 0.72f; }
    }
}

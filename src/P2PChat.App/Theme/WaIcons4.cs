using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace P2PChat.App.Theme
{
    /// <summary>Iconothèque WhatsApp — partie 4 : photo, lecture, pause, téléphone.</summary>
    public static partial class WaIcons
    {
        // ================================================================ PHOTO 🖼
        public static void Photo(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.5f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawRectangle(p, U(x), U(y), U(s * 0.96f), U(s * 0.78f));
                g.FillEllipse(new SolidBrush(c), U(x + s * 0.16f), U(y + s * 0.12f), U(s * 0.12f), U(s * 0.12f));
                g.DrawLine(p, U(x + s * 0.08f), U(y + s * 0.66f), U(x + s * 0.38f), U(y + s * 0.30f));
                g.DrawLine(p, U(x + s * 0.38f), U(y + s * 0.30f), U(x + s * 0.54f), U(y + s * 0.48f));
                g.DrawLine(p, U(x + s * 0.54f), U(y + s * 0.48f), U(x + s * 0.68f), U(y + s * 0.36f));
                g.DrawLine(p, U(x + s * 0.68f), U(y + s * 0.36f), U(x + s * 0.90f), U(y + s * 0.66f));
            }
        }

        // ================================================================= PLAY ▶
        public static void Play(Graphics g, float x, float y, float s, Color c)
        {
            using (SolidBrush b = new SolidBrush(c))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                PointF[] tri = new PointF[]
                {
                    new PointF(U(x + s * 0.28f), U(y + s * 0.12f)),
                    new PointF(U(x + s * 0.88f), U(y + s * 0.50f)),
                    new PointF(U(x + s * 0.28f), U(y + s * 0.88f)),
                };
                g.FillPolygon(b, tri);
            }
        }

        // ================================================================ PAUSE ⏸
        public static void Pause(Graphics g, float x, float y, float s, Color c)
        {
            using (SolidBrush b = new SolidBrush(c))
            {
                g.FillRectangle(b, U(x + s * 0.26f), U(y + s * 0.14f), U(s * 0.16f), U(s * 0.72f));
                g.FillRectangle(b, U(x + s * 0.58f), U(y + s * 0.14f), U(s * 0.16f), U(s * 0.72f));
            }
        }

        // ============================================================== PHONE ☎
        public static void Phone(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.6f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddBezier(U(x + s * .22f), U(y + s * .13f), U(x + s * .16f), U(y + s * .08f),
                        U(x + s * .09f), U(y + s * .14f), U(x + s * .10f), U(y + s * .25f));
                    path.AddBezier(U(x + s * .10f), U(y + s * .25f), U(x + s * .20f), U(y + s * .57f),
                        U(x + s * .43f), U(y + s * .80f), U(x + s * .75f), U(y + s * .90f));
                    path.AddBezier(U(x + s * .75f), U(y + s * .90f), U(x + s * .86f), U(y + s * .91f),
                        U(x + s * .92f), U(y + s * .84f), U(x + s * .87f), U(y + s * .78f));
                    g.DrawPath(p, path);
                    g.DrawLine(p, U(x + s * .22f), U(y + s * .13f), U(x + s * .37f), U(y + s * .30f));
                    g.DrawLine(p, U(x + s * .75f), U(y + s * .90f), U(x + s * .90f), U(y + s * .76f));
                }
            }
        }
    }
}

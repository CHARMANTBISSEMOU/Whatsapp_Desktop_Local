using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace P2PChat.App.Theme
{
    /// <summary>
    /// Iconothèque vectorielle "WhatsApp Desktop". Tracés à la main dans une
    /// boîte s×s d'origine (x,y) — valeurs en px 96 dpi, scalées par WaTheme.Dpi.
    /// </summary>
    public static partial class WaIcons
    {
        /// <summary>Pen arrondi pré-configuré (largeur en px 96dpi).</summary>
        private static Pen N(Color c, float w96)
        {
            Pen p = new Pen(c, Math.Max(1f, w96 * WaTheme.Dpi));
            p.StartCap = LineCap.Round;
            p.EndCap = LineCap.Round;
            p.LineJoin = LineJoin.Round;
            return p;
        }

        private static float U(float v96) { return v96 * WaTheme.Dpi; }

        // ================================================================= SEARCH
        public static void Search(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.7f))
            {
                float r = s * 0.42f;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawEllipse(p, U(x), U(y), U(r * 2), U(r * 2));
                g.DrawLine(p, U(x + r * 1.42f), U(y + r * 1.42f), U(x + s), U(y + s));
            }
        }

        // ================================================================== PLUS
        public static void Plus(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.9f))
            {
                float m = s * 0.26f;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawLine(p, U(x + m), U(y + s / 2), U(x + s - m), U(y + s / 2));
                g.DrawLine(p, U(x + s / 2), U(y + m), U(x + s / 2), U(y + s - m));
            }
        }

        // ============================================================== MENU DOTS
        public static void MenuDots(Graphics g, float x, float y, float s, Color c)
        {
            using (SolidBrush b = new SolidBrush(c))
            {
                float d = s * 0.22f;
                float cy = U(y + s / 2 - d / 2);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(b, U(x + s * 0.08f), cy, U(d), U(d));
                g.FillEllipse(b, U(x + s * 0.39f), cy, U(d), U(d));
                g.FillEllipse(b, U(x + s * 0.70f), cy, U(d), U(d));
            }
        }

        // ===================================================================== MIC
        public static void Mic(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.7f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float w = s * 0.34f, h = s * 0.50f;
                g.DrawEllipse(p, U(x + s * 0.33f), U(y + s * 0.05f), U(w), U(h));
                using (GraphicsPath arch = new GraphicsPath())
                {
                    float left = U(x + s * 0.17f), right = U(x + s * 0.83f);
                    float top = U(y + s * 0.28f), bottom = U(y + s * 0.66f);
                    arch.AddArc(left, top, U(s * 0.14f), U(s * 0.30f), 90, 180);
                    arch.AddArc(right - U(s * 0.14f), top, U(s * 0.14f), U(s * 0.30f), -90, -180);
                    g.DrawPath(p, arch);
                }
                g.DrawLine(p, U(x + s / 2), U(y + s * 0.66f), U(x + s / 2), U(y + s * 0.80f));
                g.DrawLine(p, U(x + s * 0.30f), U(y + s * 0.86f), U(x + s * 0.70f), U(y + s * 0.86f));
            }
        }

        // ==================================================================== SEND
        public static void Send(Graphics g, float x, float y, float s, Color c)
        {
            using (SolidBrush b = new SolidBrush(c))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                PointF[] tri = new PointF[]
                {
                    new PointF(U(x), U(y)),
                    new PointF(U(x + s), U(y + s / 2)),
                    new PointF(U(x), U(y + s)),
                    new PointF(U(x + s * 0.30f), U(y + s / 2)),
                };
                g.FillPolygon(b, tri);
            }
        }

        // =================================================================== EMOJI
        public static void Emoji(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.7f))
            using (SolidBrush b = new SolidBrush(c))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawEllipse(p, U(x + s * 0.04f), U(y + s * 0.04f), U(s * 0.92f), U(s * 0.92f));
                float ed = s * 0.10f;
                g.FillEllipse(b, U(x + s * 0.28f), U(y + s * 0.26f), U(ed), U(ed));
                g.FillEllipse(b, U(x + s * 0.62f), U(y + s * 0.26f), U(ed), U(ed));
                using (GraphicsPath smile = new GraphicsPath())
                {
                    smile.AddArc(U(x + s * 0.22f), U(y + s * 0.38f), U(s * 0.56f), U(s * 0.46f), 25, 130);
                    g.DrawPath(p, smile);
                }
            }
        }
    }
}

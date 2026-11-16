using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace P2PChat.App.Theme
{
    /// <summary>Iconothèque WhatsApp — partie 6 : réseau, soleil, coche cerclée, clear.</summary>
    public static partial class WaIcons
    {
        // ============================================================== NETWORK 🌐
        public static void Network(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.5f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawEllipse(p, U(x + s * 0.02f), U(y + s * 0.02f), U(s * 0.96f), U(s * 0.96f));
                g.DrawEllipse(p, U(x + s * 0.30f), U(y + s * 0.02f), U(s * 0.40f), U(s * 0.96f));
                g.DrawLine(p, U(x), U(y + s * 0.50f), U(x + s), U(y + s * 0.50f));
                using (GraphicsPath half = new GraphicsPath())
                {
                    half.AddArc(U(x + s * 0.24f), U(y), U(s * 0.52f), U(s * 1.02f), 260, 200);
                    g.DrawPath(p, half);
                }
            }
        }

        // ================================================================ SUN ☀
        public static void Sun(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.6f))
            using (SolidBrush b = new SolidBrush(c))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(b, U(x + s * 0.26f), U(y + s * 0.26f), U(s * 0.48f), U(s * 0.48f));
                float cx = x + s / 2, cy = y + s / 2, r1 = s * 0.36f, r2 = s * 0.50f;
                for (int i = 0; i < 8; i++)
                {
                    double a = Math.PI * 2 * i / 8.0;
                    g.DrawLine(p,
                        U((float)(cx + Math.Cos(a) * r1)), U((float)(cy + Math.Sin(a) * r1)),
                        U((float)(cx + Math.Cos(a) * r2)), U((float)(cy + Math.Sin(a) * r2)));
                }
            }
        }

        //                                                              CHECK CIRCLE
        public static void CheckCircle(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 2.0f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawEllipse(p, U(x), U(y), U(s), U(s));
                g.DrawLine(p, U(x + s * 0.26f), U(y + s * 0.52f), U(x + s * 0.44f), U(y + s * 0.70f));
                g.DrawLine(p, U(x + s * 0.44f), U(y + s * 0.70f), U(x + s * 0.78f), U(y + s * 0.30f));
            }
        }

        //                                                        CLEAR (loupe ×)
        public static void Clear(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.6f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawEllipse(p, U(x), U(y), U(s), U(s));
                float m = s * 0.30f;
                g.DrawLine(p, U(x + m), U(y + m), U(x + s - m), U(y + s - m));
                g.DrawLine(p, U(x + s - m), U(y + m), U(x + m), U(y + s - m));
            }
        }

        public static void Archive(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.7f))
            {
                g.DrawRectangle(p, U(x + s * .14f), U(y + s * .30f), U(s * .72f), U(s * .58f));
                g.DrawRectangle(p, U(x + s * .08f), U(y + s * .12f), U(s * .84f), U(s * .20f));
                g.DrawLine(p, U(x + s * .38f), U(y + s * .49f), U(x + s * .62f), U(y + s * .49f));
            }
        }

        //                                                        BLOCK (cercle /)
        public static void Block(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.6f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawEllipse(p, U(x), U(y), U(s), U(s));
                g.DrawLine(p, U(x + s * 0.25f), U(y + s * 0.75f), U(x + s * 0.75f), U(y + s * 0.25f));
            }
        }

        //                                                            STICKER (✨)
        public static void Star(Graphics g, float x, float y, float s, Color c)
        {
            PointF[] pts = new PointF[10];
            double cx = x + s / 2, cy = y + s / 2;
            for (int i = 0; i < 10; i++)
            {
                double r = (i % 2 == 0) ? s * 0.50 : s * 0.22;
                double a = -Math.PI / 2 + Math.PI * i / 5.0;
                pts[i] = new PointF(U((float)(cx + Math.Cos(a) * r)), U((float)(cy + Math.Sin(a) * r)));
            }
            using (SolidBrush b = new SolidBrush(c))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPolygon(b, pts);
            }
        }

        /// <summary>Bouton circulaire translucide (fond hover des icon buttons).</summary>
        public static void CircleBack(Graphics g, RectangleF r, Color c, double alpha)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(255 * alpha), c)))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(b, r);
            }
        }

        // ================================================================ PENCIL ✏
        public static void Pencil(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.6f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawLine(p, U(x + s * 0.20f), U(y + s * 0.80f), U(x + s * 0.70f), U(y + s * 0.24f));
                g.DrawLine(p, U(x + s * 0.70f), U(y + s * 0.24f), U(x + s * 0.90f), U(y + s * 0.44f));
                g.DrawLine(p, U(x + s * 0.64f), U(y + s * 0.64f), U(x + s * 0.82f), U(y + s * 0.48f));
            }
        }

        // ================================================================ BULLE 💬
        public static void Bubble(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.7f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float w = s * 0.92f, h = s * 0.62f, r = s * 0.20f;
                using (GraphicsPath b = new GraphicsPath())
                {
                    b.AddArc(U(x), U(y), U(r * 2), U(r * 2), 180, 90);
                    b.AddArc(U(x + w - r * 2), U(y), U(r * 2), U(r * 2), 270, 90);
                    b.AddArc(U(x + w - r * 2), U(y + h - r * 2), U(r * 2), U(r * 2), 0, 90);
                    b.AddArc(U(x), U(y + h - r * 2), U(r * 2), U(r * 2), 90, 90);
                    b.CloseFigure();
                    g.DrawPath(p, b);
                }
                g.DrawLine(p, U(x + s * 0.28f), U(y + h), U(x + s * 0.14f), U(y + h + s * 0.24f));
                g.DrawLine(p, U(x + s * 0.14f), U(y + h + s * 0.24f), U(x + s * 0.04f), U(y + h - s * 0.04f));
                g.DrawLine(p, U(x + s * 0.20f), U(y + h * 0.42f), U(x + s * 0.70f), U(y + h * 0.42f));
                g.DrawLine(p, U(x + s * 0.20f), U(y + h * 0.68f), U(x + s * 0.52f), U(y + h * 0.68f));
            }
        }
    }
}

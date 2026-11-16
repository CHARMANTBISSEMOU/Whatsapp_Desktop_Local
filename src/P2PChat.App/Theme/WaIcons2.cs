using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace P2PChat.App.Theme
{
    /// <summary>Iconothèque WhatsApp — partie 2 : flèche retour, chevrons, X,
    /// corbeille, cadenas, réponse, réaction, épingler, cloche sourdine.</summary>
    public static partial class WaIcons
    {
        // ================================================================ BACK ←
        public static void Back(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.9f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawLine(p, U(x + s), U(y + s / 2), U(x + s * 0.15f), U(y + s / 2));
                g.DrawLine(p, U(x + s * 0.40f), U(y + s * 0.14f), U(x + s * 0.09f), U(y + s / 2));
                g.DrawLine(p, U(x + s * 0.09f), U(y + s / 2), U(x + s * 0.40f), U(y + s * 0.86f));
            }
        }

        // ========================================================= CHEVRON ▾ / ▸
        public static void ChevronDown(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.9f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawLine(p, U(x + s * 0.16f), U(y + s * 0.34f), U(x + s / 2), U(y + s * 0.68f));
                g.DrawLine(p, U(x + s / 2), U(y + s * 0.68f), U(x + s * 0.84f), U(y + s * 0.34f));
            }
        }

        public static void ChevronRight(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.9f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawLine(p, U(x + s * 0.34f), U(y + s * 0.12f), U(x + s * 0.68f), U(y + s / 2));
                g.DrawLine(p, U(x + s * 0.68f), U(y + s / 2), U(x + s * 0.34f), U(y + s * 0.88f));
            }
        }

        public static void ChevronLeft(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.9f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawLine(p, U(x + s * 0.66f), U(y + s * 0.12f), U(x + s * 0.32f), U(y + s / 2));
                g.DrawLine(p, U(x + s * 0.32f), U(y + s / 2), U(x + s * 0.66f), U(y + s * 0.88f));
            }
        }

        public static void ChevronUp(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.9f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawLine(p, U(x + s * 0.16f), U(y + s * 0.66f), U(x + s / 2), U(y + s * 0.32f));
                g.DrawLine(p, U(x + s / 2), U(y + s * 0.32f), U(x + s * 0.84f), U(y + s * 0.66f));
            }
        }

        // ================================================================ CLOSE ×
        public static void Close(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.9f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float m = s * 0.20f;
                g.DrawLine(p, U(x + m), U(y + m), U(x + s - m), U(y + s - m));
                g.DrawLine(p, U(x + s - m), U(y + m), U(x + m), U(y + s - m));
            }
        }

        // ================================================================ TRASH
        public static void Trash(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.6f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                // couvercle
                g.DrawLine(p, U(x + s * 0.18f), U(y + s * 0.24f), U(x + s * 0.82f), U(y + s * 0.24f));
                g.DrawLine(p, U(x + s * 0.40f), U(y + s * 0.08f), U(x + s * 0.60f), U(y + s * 0.08f));
                g.DrawLine(p, U(x + s * 0.40f), U(y + s * 0.08f), U(x + s * 0.40f), U(y + s * 0.24f));
                g.DrawLine(p, U(x + s * 0.60f), U(y + s * 0.08f), U(x + s * 0.60f), U(y + s * 0.24f));
                // corps
                using (GraphicsPath body = new GraphicsPath())
                {
                    body.AddLine(U(x + s * 0.24f), U(y + s * 0.24f), U(x + s * 0.28f), U(y + s * 0.90f));
                    body.AddLine(U(x + s * 0.28f), U(y + s * 0.90f), U(x + s * 0.72f), U(y + s * 0.90f));
                    body.AddLine(U(x + s * 0.72f), U(y + s * 0.90f), U(x + s * 0.76f), U(y + s * 0.24f));
                    g.DrawPath(p, body);
                }
                g.DrawLine(p, U(x + s * 0.42f), U(y + s * 0.38f), U(x + s * 0.44f), U(y + s * 0.76f));
                g.DrawLine(p, U(x + s * 0.58f), U(y + s * 0.38f), U(x + s * 0.56f), U(y + s * 0.76f));
            }
        }

        // ================================================================= LOCK
        public static void Lock(Graphics g, float x, float y, float s, Color c)
        {
            using (Pen p = N(c, 1.5f))
            using (SolidBrush b = new SolidBrush(c))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                // anse
                using (GraphicsPath shackle = new GraphicsPath())
                {
                    shackle.AddArc(U(x + s * 0.26f), U(y + s * 0.06f), U(s * 0.48f), U(s * 0.50f), 180, 180);
                    g.DrawPath(p, shackle);
                }
                // corps
                g.FillRectangle(b, U(x + s * 0.14f), U(y + s * 0.42f), U(s * 0.72f), U(s * 0.50f));
            }
        }
    }
}

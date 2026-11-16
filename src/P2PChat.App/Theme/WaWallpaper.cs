using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace P2PChat.App.Theme
{
    /// <summary>
    /// Papier peint de conversation : mosaïque de "doodles" ligne grise pâle
    /// sur le fond exact de WhatsApp (#EFEAE2 clair / #0B141A sombre), comme
    /// la vraie application. Tuile 512 px mise en cache par thème.
    /// </summary>
    public static class WaWallpaper
    {
        private static Bitmap _lightTile;
        private static Bitmap _darkTile;
        private static readonly object Gate = new object();
        private const int Tile96 = 512;

        /// <summary>Remplit rect avec la tuile doodles du thème courant (mosaïque).</summary>
        public static void Fill(Graphics g, Rectangle rect)
        {
            Bitmap tile = Get(WaTheme.Dark);
            if (tile == null) return;
            using (TextureBrush tb = new TextureBrush(tile, WrapMode.Tile))
            {
                g.FillRectangle(tb, rect);
            }
        }

        /// <summary>Charge une capture PLAN (rect) du papier peint — sans allocation.</summary>
        public static void PaintRegion(Graphics g, Rectangle rect, int offsetY)
        {
            Bitmap tile = Get(WaTheme.Dark);
            if (tile == null) return;
            // on "déroule" avec décalage vertical offsetY (scroll fluide)
            using (TextureBrush tb = new TextureBrush(tile, WrapMode.Tile))
            {
                tb.TranslateTransform(0, -offsetY % tile.Height);
                g.FillRectangle(tb, rect);
            }
        }

        private static Bitmap Get(bool dark)
        {
            lock (Gate)
            {
                if (dark)
                {
                    if (_darkTile == null) _darkTile = Build(true);
                    return _darkTile;
                }
                if (_lightTile == null) _lightTile = Build(false);
                return _lightTile;
            }
        }

        /// <summary>Change de thème : invalide les tuiles cachées.</summary>
        public static void Invalidate()
        {
            lock (Gate)
            {
                if (_lightTile != null) { _lightTile.Dispose(); _lightTile = null; }
                if (_darkTile != null) { _darkTile.Dispose(); _darkTile = null; }
            }
        }
        private static Bitmap Build(bool dark)
        {
            int size = WaTheme.S(Tile96);
            Bitmap bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            using (SolidBrush bg = new SolidBrush(WaTheme.ChatBg))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillRectangle(bg, 0, 0, size, size);

                Color ink = dark
                    ? Color.FromArgb(28, Color.FromArgb(0x16, 0x2E, 0x34))
                    : Color.FromArgb(46, Color.FromArgb(0xD5, 0xC8, 0xB4));
                float k = size / (float)Tile96;

                // grille irrégulière mais déterministe de ~18 doodles par tuile
                int[] cells = new int[] { 0, 0, 0, 1, 4, 1, 3, 4, 2, 6, 1, 6, 5, 2, 6, 5, 4, 3 };
                float step = size / 7f; // 7 x 7 zones
                for (int i = 0; i + 1 < cells.Length; i += 2)
                {
                    int row = cells[i];
                    int col = cells[i + 1];
                    float cx = col * step + step * 0.45f;
                    float cy = row * step + step * 0.35f;
                    DrawDoodle(g, (i / 2) % 12, cx, cy, ink, 30f * k);
                }
                for (int i = 1; i + 1 < cells.Length; i += 2)
                {
                    int row = cells[i];
                    int col = 6 - cells[i + 1];
                    float cx = col * step + step * 0.65f;
                    float cy = row * step + step * 0.85f;
                    DrawDoodle(g, (i / 2 + 3) % 12, cx, cy, ink, 26f * k);
                }
            }
            return bmp;
        }

        /// <summary>Un doodle = iconographie stylée type WhatsApp (traits 2px).</summary>
        private static void DrawDoodle(Graphics g, int kind, float cx, float cy, Color ink, float s)
        {
            using (Pen p = new Pen(ink, Math.Max(1.2f, s * 0.07f)))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                float x = cx - s / 2, y = cy - s / 2;
                switch (kind)
                {
                    case 0: // cœur
                        using (GraphicsPath path = new GraphicsPath())
                        {
                            path.AddBezier(x, y + s * 0.32f, x, y - s * 0.10f, x + s * 0.42f, y - s * 0.08f, x + s * 0.5f, y + s * 0.30f);
                            path.AddBezier(x + s * 0.5f, y + s * 0.30f, x + s * 0.58f, y - s * 0.08f, x + s, y - s * 0.10f, x + s, y + s * 0.32f);
                            path.AddLine(x + s, y + s * 0.32f, x + s * 0.5f, y + s * 0.85f);
                            path.AddLine(x + s * 0.5f, y + s * 0.85f, x, y + s * 0.32f);
                            g.DrawPath(p, path);
                        }
                        break;
                    case 1: // enveloppe
                        g.DrawRectangle(p, x, y + s * 0.15f, s, s * 0.66f);
                        g.DrawLine(p, x, y + s * 0.15f, x + s / 2, y + s * 0.5f);
                        g.DrawLine(p, x + s / 2, y + s * 0.5f, x + s, y + s * 0.15f);
                        break;
                    case 2: // bulle de chat
                        using (GraphicsPath b = new GraphicsPath())
                        {
                            b.AddArc(x, y, s * 0.8f, s * 0.62f, 180, 320);
                            b.AddLine(x + s * 0.35f, y + s * 0.62f, x + s * 0.2f, y + s * 0.86f);
                            b.AddLine(x + s * 0.2f, y + s * 0.86f, x + s * 0.42f, y + s * 0.60f);
                            b.CloseFigure();
                            g.DrawPath(p, b);
                        }
                        break;
                    case 3: // note de musique
                        g.FillEllipse(new SolidBrush(ink), x, y + s * 0.55f, s * 0.34f, s * 0.26f);
                        g.DrawLine(p, x + s * 0.32f, y + s * 0.55f, x + s * 0.32f, y + s * 0.05f);
                        g.DrawLine(p, x + s * 0.32f, y + s * 0.05f, x + s * 0.86f, y + s * 0.16f);
                        g.DrawLine(p, x + s * 0.86f, y + s * 0.16f, x + s * 0.86f, y + s * 0.48f);
                        break;
                    case 4: // appareil photo
                        g.DrawRectangle(p, x + s * 0.06f, y + s * 0.28f, s * 0.88f, s * 0.58f);
                        g.DrawEllipse(p, x + s * 0.33f, y + s * 0.40f, s * 0.34f, s * 0.34f);
                        g.FillEllipse(new SolidBrush(ink), x + s * 0.46f, y + s * 0.53f, s * 0.07f, s * 0.07f);
                        g.DrawRectangle(p, x + s * 0.34f, y + s * 0.16f, s * 0.32f, s * 0.13f);
                        break;
                    case 5: // étoile filante
                        g.DrawLine(p, x, y + s, x + s * 0.7f, y + s * 0.3f);
                        g.DrawLine(p, x + s * 0.52f, y + s * 0.48f, x + s * 0.92f, y + s * 0.24f);
                        g.DrawLine(p, x + s * 0.62f, y + s * 0.72f, x + s, y + s * 0.5f);
                        break;
                    case 6: // tasse
                        g.DrawArc(p, x - s * 0.02f, y - s * 0.02f + s * 0.3f, s * 0.8f, s * 0.55f, 0, 180);
                        g.DrawLine(p, x, y + s * 0.28f, x + s * 0.78f, y + s * 0.28f);
                        g.DrawLine(p, x + s * 0.78f, y + s * 0.45f, x + s * 0.98f, y + s * 0.45f);
                        g.DrawArc(p, x + s * 0.72f, y + s * 0.33f, s * 0.26f, s * 0.24f, 270, 180);
                        g.DrawLine(p, x + s * 0.18f, y + s * 0.92f, x + s * 0.62f, y + s * 0.92f);
                        break;
                    case 7: // horloge
                        g.DrawEllipse(p, x + s * 0.05f, y + s * 0.05f, s * 0.9f, s * 0.9f);
                        g.DrawLine(p, x + s * 0.5f, y + s * 0.5f, x + s * 0.5f, y + s * 0.22f);
                        g.DrawLine(p, x + s * 0.5f, y + s * 0.5f, x + s * 0.72f, y + s * 0.62f);
                        break;
                    case 8: // avion papier
                        g.DrawPolygon(p, new PointF[]
                        {
                            new PointF(x, y + s * 0.5f),
                            new PointF(x + s, y + s * 0.1f),
                            new PointF(x + s * 0.62f, y + s * 0.9f),
                            new PointF(x + s * 0.48f, y + s * 0.52f),
                        });
                        break;
                    case 9: // cadenas
                        g.DrawArc(p, x + s * 0.28f, y + s * 0.05f, s * 0.44f, s * 0.5f, 180, 180);
                        g.DrawRectangle(p, x + s * 0.15f, y + s * 0.3f, s * 0.7f, s * 0.55f);
                        break;
                    case 10: // nuage
                        using (GraphicsPath c = new GraphicsPath())
                        {
                            c.AddEllipse(x, y + s * 0.4f, s, s * 0.42f);
                            c.AddEllipse(x + s * 0.12f, y + s * 0.18f, s * 0.55f, s * 0.5f);
                            c.AddEllipse(x + s * 0.42f, y + s * 0.28f, s * 0.5f, s * 0.44f);
                            g.DrawPath(p, c);
                        }
                        break;
                    default: // bougie
                        g.DrawRectangle(p, x + s * 0.3f, y + s * 0.28f, s * 0.4f, s * 0.6f);
                        g.DrawLine(p, x + s * 0.5f, y + s * 0.28f, x + s * 0.5f, y + s * 0.08f);
                        g.FillEllipse(new SolidBrush(ink), x + s * 0.44f, y + s * 0.04f, s * 0.12f, s * 0.12f);
                        break;
                }
            }
        }
    }
}

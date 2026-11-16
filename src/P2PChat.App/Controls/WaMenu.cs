using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using P2PChat.App.Theme;

namespace P2PChat.App.Controls
{
    /// <summary>Item lumineux d'un menu WhatsApp (icône facultative de 20px).</summary>
    public sealed class WaMenuItem
    {
        public string Label;
        /// <summary>Dessin d'icône 20px appellé avec (g, x, y, size, color).</summary>
        public Action<Graphics, float, float, float, Color> Icon;
        public object Callback;
        public bool Separator;
        public bool Dangerous;
        public object Tag;
    }

    /// <summary>
    /// Menu contextuel « WhatsApp » : popup sans bord, fond #FFFFFF/#233138,
    /// coins arrondis 8, ombre portée, items 40px, survol #F0F2F5/#2A3942,
    /// séparateurs, libellés, gestion clavier (Échap ferme). Ferme au clic
    /// hors zone (Deactivate) comme la vraie application.
    /// </summary>
    public sealed class WaMenu : Form
    {
        private readonly List<WaMenuItem> _items = new List<WaMenuItem>();
        private int _hover = -1;
        private int _padV;
        private int _itemH;

        /// <summary>Horodatage du dernier clic (anti-réouverture immédiate).</summary>
        public static DateTime LastClosedUtc = DateTime.MinValue;

        public WaMenu()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) Close();
            };
            Deactivate += delegate
            {
                if (!IsDisposed) Close();
            };
            DoubleBuffered = true;
            BackColor = WaTheme.MenuBg;
        }

        /// <summary>Ajoute un item avec callback générique.</summary>
        public WaMenu Add(string label, object callback,
            Action<Graphics, float, float, float, Color> icon, object tag)
        {
            WaMenuItem it = new WaMenuItem();
            it.Label = label;
            it.Icon = icon;
            it.Tag = tag;
            it.Callback = callback;
            _items.Add(it);
            return this;
        }

        public WaMenu Add(string label, Action onClick,
            Action<Graphics, float, float, float, Color> icon)
        {
            return Add(label, onClick, icon, null);
        }

        public WaMenu AddSep()
        {
            WaMenuItem it = new WaMenuItem();
            it.Separator = true;
            _items.Add(it);
            return this;
        }

        /// <summary>Ouvre le menu à l'écran (coordonnées écran), sans déborder.</summary>
        public void ShowAt(Point screenPt, int verticalOffset)
        {
            _padV = WaTheme.S(8);
            _itemH = WaTheme.S(42);
            int separatorH = WaTheme.S(9);
            int w = WaTheme.S(230);
            int h = _padV * 2;
            for (int i = 0; i < _items.Count; i++)
            {
                h += _items[i].Separator ? separatorH : _itemH;
            }
            // largeur selon le plus long libellé
            using (Graphics g = CreateGraphics())
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i].Separator || _items[i].Label == null) continue;
                    using (Font f = WaTheme.Text(10.2f))
                    {
                        SizeF sz = g.MeasureString(_items[i].Label, f);
                        int need = WaTheme.S(24 + 44) + (int)sz.Width + WaTheme.S(16);
                        if (need > w) w = need;
                    }
                }
            }

            Screen s = Screen.FromPoint(screenPt);
            int x = screenPt.X, y = screenPt.Y + verticalOffset;
            if (x + w > s.WorkingArea.Right) x = s.WorkingArea.Right - w;
            if (y + h > s.WorkingArea.Bottom) y = s.WorkingArea.Bottom - h;
            if (y < s.WorkingArea.Top) y = s.WorkingArea.Top;
            Bounds = new Rectangle(new Point(x, y), new Size(w, h));
            Show();
        }

        private Rectangle ItemBounds(int index)
        {
            int y = _padV;
            int separatorH = WaTheme.S(9);
            for (int i = 0; i < index; i++)
            {
                y += _items[i].Separator ? separatorH : _itemH;
            }
            return new Rectangle(WaTheme.S(4), y, Width - WaTheme.S(8), _itemH);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // fond arrondi
            using (GraphicsPath path = WaTheme.RoundRect(0, 0, Width - 1, Height - 1, WaTheme.S(8)))
            using (SolidBrush bg = new SolidBrush(WaTheme.MenuBg))
            {
                g.FillPath(bg, path);
            }
            using (Pen edge = new Pen(WaTheme.Dark ? Color.FromArgb(0x11, 0x1B, 0x21) : WaTheme.Divider, 1))
            using (GraphicsPath path2 = WaTheme.RoundRect(0.5f, 0.5f, Width - 1.5f, Height - 1.5f, WaTheme.S(8)))
            {
                g.DrawPath(edge, path2);
            }

            int separatorH = WaTheme.S(9);
            for (int i = 0; i < _items.Count; i++)
            {
                WaMenuItem it = _items[i];
                if (it.Separator)
                {
                    int y = ItemBounds(i).Y - _padV + 1;
                    using (Pen p = new Pen(WaTheme.Divider, 1))
                    {
                        g.DrawLine(p, WaTheme.S(12), y, Width - WaTheme.S(12), y);
                    }
                    continue;
                }
                Rectangle r = ItemBounds(i);
                if (i == _hover)
                {
                    using (GraphicsPath hp = WaTheme.RoundRect(r.X, r.Y, r.Width, r.Height, WaTheme.S(6)))
                    using (SolidBrush hb = new SolidBrush(WaTheme.MenuHover))
                    {
                        g.FillPath(hb, hp);
                        _hover = i; // conservé
                    }
                }
                Color fg = it.Dangerous
                    ? Color.FromArgb(0xF1, 0x5C, 0x6D)
                    : WaTheme.TextDark;
                if (it.Icon != null)
                {
                    it.Icon(g, r.X + WaTheme.S(16), r.Y + (r.Height - WaTheme.S(20)) / 2f,
                        WaTheme.S(20), fg);
                }
                if (it.Label != null)
                {
                    using (SolidBrush tb = new SolidBrush(fg))
                    using (Font f = WaTheme.Text(10.2f))
                    {
                        g.DrawString(it.Label, f, tb,
                            r.X + WaTheme.S(48),
                            r.Y + (r.Height - g.MeasureString(it.Label, f).Height) / 2f - 1);
                    }
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int newHover = -1;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Separator) continue;
                if (ItemBounds(i).Contains(e.Location)) { newHover = i; break; }
            }
            if (newHover != _hover)
            {
                _hover = newHover;
                Invalidate();
                Cursor = _hover >= 0 ? Cursors.Hand : Cursors.Default;
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Separator) continue;
                if (ItemBounds(i).Contains(e.Location))
                {
                    object cb = _items[i].Callback;
                    Close();
                    Action simple = cb as Action;
                    if (simple != null) simple();
                    else
                    {
                        Action<WaMenuItem> withItem = cb as Action<WaMenuItem>;
                        if (withItem != null) withItem(_items[i]);
                    }
                    return;
                }
            }
        }

        protected override bool IsInputKey(Keys keyData) { return true; }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            LastClosedUtc = DateTime.UtcNow;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                // WS_EX_TOOLWINDOW : pas dans la barre des tâches ; CS_DROPSHADOW natif
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080;
                cp.ClassStyle |= 0x00020000;
                return cp;
            }
        }
    }
}

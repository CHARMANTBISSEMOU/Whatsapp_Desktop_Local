using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using P2PChat.App.Theme;
using P2PChat.Core.Protocol;

namespace P2PChat.App.UI
{
    /// <summary>
    /// Écran de bienvenue façon WhatsApp : bandeau vert #00A884, fond #F0F2F5,
    /// carte blanche « Votre nom de profil » + saisie + bouton Continuer vert.
    /// Modifie Self.Pseudo / Self.Bio puis DialogResult.OK.
    /// </summary>
    public sealed class LoginScreen : Form
    {
        private readonly PeerInfo _self;
        private TextBox _name;
        private TextBox _bio;
        private Button _ok;
        private int _cardTop;

        public LoginScreen(PeerInfo self)
        {
            _self = self;
            Text = "P2PChat";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 420);
            BackColor = WaTheme.ChatHeaderBg;
            try { Icon = new Icon(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "whatsapp.ico")); }
            catch { }

            BuildControls();
        }

        private void BuildControls()
        {
            _cardTop = 150;
            int cardH = 210;

            Panel card = new Panel();
            card.BackColor = Color.White;
            card.Bounds = new Rectangle(80, _cardTop, 400, cardH);
            Controls.Add(card);

            Label title = new Label();
            title.Text = "Votre nom de profil";
            title.Font = WaTheme.Text(13f);
            title.ForeColor = WaTheme.TextDark;
            title.AutoSize = true;
            title.Location = new Point(24, 20);
            card.Controls.Add(title);

            _name = new TextBox();
            _name.Font = WaTheme.Text(11.5f);
            _name.ForeColor = WaTheme.TextDark;
            _name.BorderStyle = BorderStyle.None;
            _name.MaxLength = 24;
            _name.Text = _self.Pseudo;
            _name.Bounds = new Rectangle(24, 56, 352, 26);
            card.Controls.Add(_name);

            Label bioTitle = new Label();
            bioTitle.Text = "Statut (facultatif)";
            bioTitle.Font = WaTheme.Text(10f);
            bioTitle.ForeColor = WaTheme.TextGray;
            bioTitle.AutoSize = true;
            bioTitle.Location = new Point(24, 96);
            card.Controls.Add(bioTitle);

            _bio = new TextBox();
            _bio.Font = WaTheme.Text(10.5f);
            _bio.ForeColor = WaTheme.TextDark;
            _bio.BorderStyle = BorderStyle.None;
            _bio.MaxLength = 60;
            _bio.Text = _self.Bio;
            _bio.Bounds = new Rectangle(24, 120, 352, 22);
            card.Controls.Add(_bio);

            _ok = new Button();
            _ok.Text = "CONTINUER";
            _ok.FlatStyle = FlatStyle.Flat;
            _ok.FlatAppearance.BorderSize = 0;
            _ok.BackColor = WaTheme.Accent;
            _ok.ForeColor = Color.White;
            _ok.Font = WaTheme.Semi(10f);
            _ok.Cursor = Cursors.Hand;
            _ok.Bounds = new Rectangle(24, 156, 352, 38);
            _ok.Click += OnOk;
            card.Controls.Add(_ok);

            _name.Focus();
            _name.SelectAll();
            AcceptButton = _ok;
        }

        private void OnOk(object sender, EventArgs e)
        {
            string pseudo = _name.Text.Trim();
            if (pseudo.Length == 0)
            {
                MessageBox.Show(this, "Veuillez saisir votre nom.", "P2PChat",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                _name.Focus();
                return;
            }
            _self.Pseudo = pseudo;
            _self.Bio = string.IsNullOrEmpty(_bio.Text.Trim()) ? "Disponible" : _bio.Text.Trim();
            _self.UpdatedUtc = ChatMessage.NowUtcMs();
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // bandeau vert WhatsApp
            using (SolidBrush b = new SolidBrush(WaTheme.Accent))
            {
                g.FillRectangle(b, 0, 0, ClientSize.Width, _cardTop - 108);
            }

            // logo + wordmark
            int logoS = 44;
            int lx = 40, ly = 34;
            using (SolidBrush white = new SolidBrush(Color.White))
            {
                g.FillEllipse(white, lx, ly, logoS, logoS);
            }
            WaIcons.Phone(g, lx + 11, ly + 11, 22, WaTheme.Accent);
            using (Font f = WaTheme.Semi(20f))
            using (SolidBrush white = new SolidBrush(Color.White))
            {
                g.DrawString("P2PChat", f, white, lx + logoS + 14, ly + 8);
            }

            // ombre douce de la carte
            using (SolidBrush sh = new SolidBrush(Color.FromArgb(26, 0, 0, 0)))
            using (GraphicsPath p = WaTheme.RoundRect(82, _cardTop + 4, 400, 210, 10))
            {
                g.FillPath(sh, p);
            }

            // soulignement des champs (style Material WhatsApp)
            using (Pen pen = new Pen(Color.FromArgb(0xD1, 0xD7, 0xDB)))
            {
                g.DrawLine(pen, 104, _cardTop + 84, 456, _cardTop + 84);
                g.DrawLine(pen, 104, _cardTop + 144, 456, _cardTop + 144);
            }

            // pied de page discret
            using (Font f = WaTheme.Text(8.5f))
            using (SolidBrush gray = new SolidBrush(WaTheme.TextGray))
            {
                string s = "Messagerie P2P chiffrée de bout en bout sur votre réseau local";
                SizeF sz = g.MeasureString(s, f);
                g.DrawString(s, f, gray, (ClientSize.Width - sz.Width) / 2f, ClientSize.Height - 30);
            }
        }
    }
}

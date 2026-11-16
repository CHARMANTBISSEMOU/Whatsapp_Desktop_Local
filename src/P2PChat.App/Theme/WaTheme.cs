using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace P2PChat.App.Theme
{
    /// <summary>Mode de thème demandé par l'utilisateur (comme WhatsApp).</summary>
    public enum WaThemeMode
    {
        System = 0,
        Light = 1,
        Dark = 2
    }

    /// <summary>
    /// Design System "WhatsApp Desktop" — tokens EXACTS de la vraie application.
    /// Tout passe par des propriétés dynamiques : basculer ApplyMode repeint tout.
    /// </summary>
    public static partial class WaTheme
    {
        private static bool _dark;

        /// <summary>true si le thème sombre est actif.</summary>
        public static bool Dark { get { return _dark; } }

        /// <summary>Bascule le thème (System détecte le mode sombre de Windows).</summary>
        public static void ApplyMode(WaThemeMode mode)
        {
            if (mode == WaThemeMode.Dark) _dark = true;
            else if (mode == WaThemeMode.Light) _dark = false;
            else _dark = IsSystemDark();
        }

        private static bool IsSystemDark()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser
                    .OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (k != null)
                    {
                        object v = k.GetValue("AppsUseLightTheme");
                        if (v is int) return (int)v == 0;
                    }
                }
            }
            catch { }
            return false;
        }

        private static Color C(int r0, int g0, int b0, int r1, int g1, int b1)
        {
            return _dark ? Color.FromArgb(r1, g1, b1) : Color.FromArgb(r0, g0, b0);
        }

        // ------------------------------- surfaces (tokens officiels WhatsApp)
        /// <summary>Fenêtre / barre latérale (#FFFFFF / #111B21).</summary>
        public static Color SidebarBg { get { return C(255, 255, 255, 0x11, 0x1B, 0x21); } }
        /// <summary>Entête de conversation à droite (#F0F2F5 / #202C33).</summary>
        public static Color ChatHeaderBg { get { return C(0xF7, 0xF5, 0xF4, 0x20, 0x2C, 0x33); } }
        /// <summary>Papier peint de discussion (#EFEAE2 / #0B141A).</summary>
        public static Color ChatBg { get { return C(0xEF, 0xEA, 0xE2, 0x0B, 0x14, 0x1A); } }
        /// <summary>Pilule de saisie (#FFFFFF / #2A3942).</summary>
        public static Color ComposerPill { get { return C(255, 255, 255, 0x2A, 0x39, 0x42); } }
        /// <summary>Barre de recherche + chips (#F0F2F5 / #202C33).</summary>
        public static Color SearchBg { get { return C(0xF4, 0xF4, 0xF3, 0x20, 0x2C, 0x33); } }
        /// <summary>Surface d'accueil sans conversation, distincte du fond illustré du fil.</summary>
        public static Color EmptyBg { get { return C(0xF7, 0xF5, 0xF4, 0x11, 0x1B, 0x21); } }
        /// <summary>Survol listes (#F5F6F6 / #202C33).</summary>
        public static Color Hover { get { return C(0xF5, 0xF6, 0xF6, 0x20, 0x2C, 0x33); } }
        /// <summary>Sélection listes (#F0F2F5 / #2A3942).</summary>
        public static Color Selected { get { return C(0xF0, 0xF2, 0xF5, 0x2A, 0x39, 0x42); } }
        /// <summary>Fond des menus (#FFFFFF / #233138).</summary>
        public static Color MenuBg { get { return C(255, 255, 255, 0x23, 0x31, 0x38); } }
        /// <summary>Survol item de menu (#F5F6F6 / #2A3942).</summary>
        public static Color MenuHover { get { return C(0xF5, 0xF6, 0xF6, 0x2A, 0x39, 0x42); } }

        // ------------------------------- bulles/accents (tokens officiels WhatsApp)
        /// <summary>Bulle sortante (#D9FDD3 / #005C4B).</summary>
        public static Color BubbleMine { get { return C(0xD9, 0xFD, 0xD3, 0x00, 0x5C, 0x4B); } }
        /// <summary>Bulle entrante (#FFFFFF / #202C33).</summary>
        public static Color BubbleOther { get { return C(255, 255, 255, 0x20, 0x2C, 0x33); } }
        /// <summary>Citation bulle sortante.</summary>
        public static Color QuoteMine { get { return C(0xCC, 0xEF, 0xC8, 0x01, 0x40, 0x35); } }
        /// <summary>Citation bulle entrante.</summary>
        public static Color QuoteOther { get { return C(0xF0, 0xF2, 0xF5, 0x1B, 0x27, 0x2E); } }
        /// <summary>Chip date (#FFFFFF / #182229).</summary>
        public static Color ChipBg { get { return C(255, 255, 255, 0x18, 0x22, 0x29); } }
        /// <summary>Bandeau cadenas (#FFE59A / #18222A).</summary>
        public static Color EncryptBg { get { return C(0xFF, 0xE5, 0x9A, 0x18, 0x22, 0x2A); } }

        // ------------------------------------------------------------- textes
        /// <summary>Texte principal (#111B21 / #E9EDEF).</summary>
        public static Color TextDark { get { return C(0x11, 0x1B, 0x21, 0xE9, 0xED, 0xEF); } }
        /// <summary>Texte secondaire (#667781 / #8696A0).</summary>
        public static Color TextGray { get { return C(0x66, 0x77, 0x81, 0x86, 0x96, 0xA0); } }
        /// <summary>Méta (heure) bulle entrante.</summary>
        public static Color MetaOther { get { return C(0x66, 0x77, 0x81, 0x86, 0x96, 0xA0); } }
        /// <summary>Méta (heure) bulle sortante.</summary>
        public static Color MetaMine { get { return C(0x78, 0x8C, 0x7C, 0x8D, 0xC0, 0xA9); } }
        /// <summary>Icônes latérales (#54656F / #AEBAC1).</summary>
        public static Color IconGray { get { return C(0x54, 0x65, 0x6F, 0xAE, 0xBA, 0xC1); } }

        // ------------------------------------------------------------- accents
        /// <summary>Vert action (#00A884) — micro/envoi, chúte verte, wordmark.</summary>
        public static Color Accent { get { return Color.FromArgb(0x00, 0xA8, 0x84); } }
        /// <summary>Vert marque (#25D366) — badges non-lus, diviseur NON LUS.</summary>
        public static Color BrandGreen { get { return Color.FromArgb(0x25, 0xD3, 0x66); } }
        /// <summary>Bleu coches ✓✓ lues (#53BDEB).</summary>
        public static Color ReadBlue { get { return Color.FromArgb(0x53, 0xBD, 0xEB); } }
        /// <summary>Rouge enregistrement (#F15C6D).</summary>
        public static Color RecordRed { get { return Color.FromArgb(0xF1, 0x5C, 0x6D); } }
        /// <summary>Séparateurs (#E9EDEF / #2A3942).</summary>
        public static Color Divider { get { return C(0xE9, 0xED, 0xEF, 0x2A, 0x39, 0x42); } }

        // ------------------------------------------------------------------ DPI
        private static float _dpi = 1f;

        /// <summary>Facteur d'échelle DPI (1.0 = 96 dpi).</summary>
        public static float Dpi
        {
            get { return _dpi; }
            set { if (value >= 0.5f && value <= 4f) _dpi = value; }
        }

        /// <summary>Mise à l'échelle px96 → px réels.</summary>
        public static int S(int px96) { return (int)Math.Round(px96 * _dpi); }
        public static int S(double px96) { return (int)Math.Round(px96 * _dpi); }

        // ----------------------------------------------------------------- fonts
        private static readonly string FName = "Segoe UI";
        private static readonly string FSemi = "Segoe UI Semibold";

        public static Font Text(float pt) { return new Font(FName, pt * _dpi); }
        public static Font Semi(float pt)
        {
            try { return new Font(FSemi, pt * _dpi); }
            catch { return new Font(FName, pt * _dpi, FontStyle.Bold); }
        }
        public static Font Bold(float pt) { return new Font(FName, pt * _dpi, FontStyle.Bold); }
        public static Font Emoji(float pt) { return new Font("Segoe UI Emoji", pt * _dpi); }

        /// <summary>Grandeur de police choisie dans Paramètres (0..3).</summary>
        public static float FontStep;

        /// <summary>Taille du texte des messages selon FontStep.</summary>
        public static float MsgPt { get { return 10.55f + FontStep * 0.75f; } }
        /// <summary>Taille des messages "emoji seul" (très grands, comme WhatsApp).</summary>
        public static float MsgEmojiPt { get { return 22f + FontStep * 2f; } }

        // ---------------------------------------------------------------- formes
        /// <summary>Chemin arrondi.</summary>
        public static GraphicsPath RoundRect(float x, float y, float w, float h, float r)
        {
            GraphicsPath p = new GraphicsPath();
            if (r < 0.5f) r = 0.5f;
            if (r * 2 > w) r = w / 2f;
            if (r * 2 > h) r = h / 2f;
            p.AddArc(x, y, r * 2, r * 2, 180, 90);
            p.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
            p.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
            p.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>Arrondi par coin (bulles WhatsApp : queue en haut).</summary>
        public static GraphicsPath RoundRectCoins(float x, float y, float w, float h,
            float tl, float tr, float br, float bl)
        {
            GraphicsPath p = new GraphicsPath();
            p.AddLine(x + tl, y, x + w - tr, y);
            if (tr > 0.5f) p.AddArc(x + w - tr * 2, y, tr * 2, tr * 2, 270, 90);
            p.AddLine(x + w, y + tr, x + w, y + h - br);
            if (br > 0.5f) p.AddArc(x + w - br * 2, y + h - br * 2, br * 2, br * 2, 0, 90);
            p.AddLine(x + w - br, y + h, x + bl, y + h);
            if (bl > 0.5f) p.AddArc(x, y + h - bl * 2, bl * 2, bl * 2, 90, 90);
            p.AddLine(x, y + h - bl, x, y + tl);
            if (tl > 0.5f) p.AddArc(x, y, tl * 2, tl * 2, 180, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>Mélange a + b (t = poids de b).</summary>
        public static Color Blend(Color a, Color b, double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(
                a.A + (int)((b.A - a.A) * t),
                a.R + (int)((b.R - a.R) * t),
                a.G + (int)((b.G - a.G) * t),
                a.B + (int)((b.B - a.B) * t));
        }

        /// <summary>Version translucide d'une couleur (alpha = t*255).</summary>
        public static Color Alpha(Color c, double t)
        {
            return Color.FromArgb((int)Math.Round(255 * t) , c.R, c.G, c.B);
        }
    }
}

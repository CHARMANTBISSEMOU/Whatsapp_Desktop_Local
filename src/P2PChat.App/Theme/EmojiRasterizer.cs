using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using Wpf = System.Windows.Media;
using WpfWindow = System.Windows;

namespace P2PChat.App.Theme
{
    /// <summary>Rendu emoji par DirectWrite/WPF pour les polices emoji couleur Windows.</summary>
    internal static class EmojiRasterizer
    {
        private const int MaxCachedImages = 192;
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, Bitmap> Cache = new Dictionary<string, Bitmap>();
        private static readonly Queue<string> CacheOrder = new Queue<string>();

        internal static bool ContainsEmoji(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsHighSurrogate(c) || c == '\uFE0F' || c == '\u200D' ||
                    (c >= '\u2300' && c <= '\u27FF') || (c >= '\u2B00' && c <= '\u2BFF')) return true;
            }
            return false;
        }

        internal static void Draw(Graphics graphics, string text, Font font, Brush brush, RectangleF bounds)
        {
            if (graphics == null || string.IsNullOrEmpty(text) || font == null || brush == null) return;
            float scale = graphics.DpiY / 96f;
            float widthDip = Math.Max(1f, bounds.Width / scale);
            SolidBrush solidBrush = brush as SolidBrush;
            Color drawColor = solidBrush == null ? Color.Black : solidBrush.Color;
            string key = font.Name + "|" + font.Style + "|" + font.SizeInPoints.ToString("0.0", CultureInfo.InvariantCulture) +
                "|" + drawColor.ToArgb() + "|" + widthDip.ToString("0", CultureInfo.InvariantCulture) + "|" + text;

            Bitmap image;
            lock (Sync)
            {
                if (!Cache.TryGetValue(key, out image))
                {
                    image = Render(text, font, drawColor, widthDip);
                    if (image == null)
                    {
                        graphics.DrawString(text, font, brush, bounds);
                        return;
                    }
                    Cache[key] = image;
                    CacheOrder.Enqueue(key);
                    while (CacheOrder.Count > MaxCachedImages)
                    {
                        string expired = CacheOrder.Dequeue();
                        Bitmap old;
                        if (Cache.TryGetValue(expired, out old))
                        {
                            Cache.Remove(expired);
                            old.Dispose();
                        }
                    }
                }
            }

            float imageWidth = image.Width * scale;
            float imageHeight = image.Height * scale;
            if (imageWidth > bounds.Width && imageWidth > 0)
            {
                float ratio = bounds.Width / imageWidth;
                imageWidth *= ratio;
                imageHeight *= ratio;
            }
            graphics.DrawImage(image, bounds.X, bounds.Y, imageWidth, imageHeight);
        }

        private static Bitmap Render(string text, Font font, Color color, float maxWidthDip)
        {
            try
            {
                double fontSizeDip = font.SizeInPoints * 96.0 / 72.0;
                Wpf.Typeface typeface = new Wpf.Typeface(new Wpf.FontFamily(font.Name),
                    font.Italic ? WpfWindow.FontStyles.Italic : WpfWindow.FontStyles.Normal,
                    font.Bold ? WpfWindow.FontWeights.Bold : WpfWindow.FontWeights.Normal,
                    WpfWindow.FontStretches.Normal);
                Wpf.FormattedText formatted = new Wpf.FormattedText(text, CultureInfo.CurrentCulture,
                    WpfWindow.FlowDirection.LeftToRight, typeface, fontSizeDip,
                    new Wpf.SolidColorBrush(Wpf.Color.FromArgb(color.A, color.R, color.G, color.B)),
                    1.0);
                formatted.MaxTextWidth = maxWidthDip;
                int width = Math.Max(1, (int)Math.Ceiling(Math.Min(maxWidthDip, formatted.WidthIncludingTrailingWhitespace)) + 2);
                int height = Math.Max(1, (int)Math.Ceiling(formatted.Height) + 2);
                Wpf.DrawingVisual visual = new Wpf.DrawingVisual();
                using (Wpf.DrawingContext context = visual.RenderOpen())
                    context.DrawText(formatted, new WpfWindow.Point(1, 0));
                RenderTargetBitmap target = new RenderTargetBitmap(width, height, 96, 96, Wpf.PixelFormats.Pbgra32);
                target.Render(visual);
                target.Freeze();

                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(target));
                using (MemoryStream stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    stream.Position = 0;
                    using (Bitmap decoded = new Bitmap(stream)) return new Bitmap(decoded);
                }
            }
            catch { return null; }
        }
    }
}

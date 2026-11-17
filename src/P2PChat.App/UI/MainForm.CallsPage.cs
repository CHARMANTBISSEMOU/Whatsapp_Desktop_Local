using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using P2PChat.App.Theme;

namespace P2PChat.App.UI
{
    public sealed partial class MainForm
    {
        private sealed class CallHistoryItem
        {
            internal string PeerId;
            internal string PeerName;
            internal string Result;
            internal bool Incoming;
            internal DateTime AtUtc;
            internal int DurationSeconds;
        }

        private readonly List<CallHistoryItem> _callHistory = new List<CallHistoryItem>();
        private string _callPagePeerId;
        private string _hoverCallPeerId;
        private bool _hoverCallButton;

        private Rectangle CallsListRect()
        {
            Rectangle side = SidebarRect();
            int top = WaTheme.S(126);
            return new Rectangle(side.X, top, side.Width, Math.Max(0, side.Bottom - top));
        }

        private Rectangle CallsRowRect(int index)
        {
            Rectangle list = CallsListRect();
            int rowH = WaTheme.S(76);
            return new Rectangle(list.X, list.Y + index * rowH - _listScroll, list.Width, rowH);
        }

        private Rectangle CallsRowButtonRect(Rectangle row)
        {
            int d = WaTheme.S(36);
            return new Rectangle(row.Right - WaTheme.S(50), row.Y + (row.Height - d) / 2, d, d);
        }

        private Rectangle CallsMainButtonRect()
        {
            Rectangle main = ChatRect();
            int width = WaTheme.S(190), height = WaTheme.S(48);
            return new Rectangle(main.X + (main.Width - width) / 2,
                main.Y + main.Height / 2 + WaTheme.S(105), width, height);
        }

        private Rectangle CallsVideoButtonRect()
        {
            Rectangle main = CallsMainButtonRect();
            int width = WaTheme.S(190), gap = WaTheme.S(12);
            return new Rectangle(main.Right + gap, main.Y, width, main.Height);
        }

        private int CallsListScrollMax()
        {
            int rowH = WaTheme.S(76);
            return Math.Max(0, CallsPagePeers().Count * rowH - CallsListRect().Height);
        }

        private List<Conversation> CallsPagePeers()
        {
            List<Conversation> result = new List<Conversation>();
            HashSet<string> added = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _callHistory.Count; i++)
            {
                Conversation c = Conv(_callHistory[i].PeerId);
                if (c != null && added.Add(c.PeerId) && MatchesCallSearch(c)) result.Add(c);
            }
            for (int i = 0; i < _order.Count; i++)
            {
                Conversation c = Conv(_order[i]);
                if (c == null || IsGroup(c.PeerId) || Cfg.Peer(c.PeerId).Archived ||
                    !added.Add(c.PeerId) || !MatchesCallSearch(c)) continue;
                result.Add(c);
            }
            return result;
        }

        private bool MatchesCallSearch(Conversation c)
        {
            if (c == null || IsGroup(c.PeerId) || Cfg.Peer(c.PeerId).Archived) return false;
            return string.IsNullOrWhiteSpace(_searchQuery) ||
                c.PeerName.IndexOf(_searchQuery, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private CallHistoryItem LastCall(string peerId)
        {
            for (int i = 0; i < _callHistory.Count; i++)
                if (_callHistory[i].PeerId == peerId) return _callHistory[i];
            return null;
        }

        private void PaintCallsPage(Graphics g)
        {
            Rectangle side = SidebarRect(), main = ChatRect();
            using (SolidBrush sb = new SolidBrush(WaTheme.SidebarBg)) g.FillRectangle(sb, side);
            using (SolidBrush mb = new SolidBrush(WaTheme.EmptyBg)) g.FillRectangle(mb, main);
            using (Pen separator = new Pen(WaTheme.Divider)) g.DrawLine(separator, side.Right - 1, 0, side.Right - 1, ClientSize.Height);

            using (Font titleFont = WaTheme.Semi(18f))
            using (SolidBrush titleBrush = new SolidBrush(WaTheme.TextDark))
                g.DrawString("Appels", titleFont, titleBrush, side.X + WaTheme.S(24), WaTheme.S(16));
            using (Font subFont = WaTheme.Text(9.2f))
            using (SolidBrush subBrush = new SolidBrush(WaTheme.TextGray))
                g.DrawString("Appels audio avec vos contacts", subFont, subBrush, side.X + WaTheme.S(24), WaTheme.S(49));

            Rectangle search = new Rectangle(side.X + WaTheme.S(12), WaTheme.S(HeaderH) + WaTheme.S(7),
                side.Width - WaTheme.S(24), WaTheme.S(42));
            using (SolidBrush searchBrush = new SolidBrush(WaTheme.SearchBg))
            using (GraphicsPath path = WaTheme.RoundRect(search.X, search.Y, search.Width, search.Height, WaTheme.S(9)))
                g.FillPath(searchBrush, path);
            WaIcons.Search(g, search.X + WaTheme.S(14), search.Y + WaTheme.S(12), WaTheme.S(16), WaTheme.IconGray);
            if (_searchBox.TextLength == 0)
            {
                using (Font f = WaTheme.Text(9.5f))
                using (SolidBrush b = new SolidBrush(WaTheme.TextGray))
                    g.DrawString("Rechercher un contact", f, b, search.X + WaTheme.S(40), search.Y + WaTheme.S(11));
            }

            Rectangle listRect = CallsListRect();
            List<Conversation> peers = CallsPagePeers();
            GraphicsState state = g.Save();
            g.SetClip(listRect);
            if (peers.Count == 0)
            {
                using (Font f = WaTheme.Text(9.5f))
                using (SolidBrush b = new SolidBrush(WaTheme.TextGray))
                {
                    string msg = _callHistory.Count == 0
                        ? "Les appels récents apparaîtront ici."
                        : "Aucun contact correspondant.";
                    SizeF sz = g.MeasureString(msg, f);
                    g.DrawString(msg, f, b, listRect.X + (listRect.Width - sz.Width) / 2f, listRect.Y + WaTheme.S(34));
                }
            }
            for (int i = 0; i < peers.Count; i++)
            {
                Rectangle row = CallsRowRect(i);
                if (row.Bottom < listRect.Top || row.Top > listRect.Bottom) continue;
                Conversation c = peers[i];
                if (c.PeerId == _callPagePeerId || c.PeerId == _hoverCallPeerId)
                {
                    using (SolidBrush fill = new SolidBrush(c.PeerId == _callPagePeerId ? WaTheme.Selected : WaTheme.Hover))
                        g.FillRectangle(fill, row);
                }
                int av = WaTheme.S(46);
                WaTheme.DrawAvatar(g, new Rectangle(row.X + WaTheme.S(14), row.Y + (row.Height - av) / 2, av, av), c.PeerName, null, c.Online);
                using (Font nameFont = WaTheme.Semi(10f))
                using (SolidBrush nameBrush = new SolidBrush(WaTheme.TextDark))
                    g.DrawString(ClipText(g, c.PeerName, nameFont, row.Width - WaTheme.S(116)), nameFont, nameBrush,
                        row.X + WaTheme.S(72), row.Y + WaTheme.S(16));
                CallHistoryItem call = LastCall(c.PeerId);
                string detail = call == null ? (c.Online ? "En ligne" : "Hors ligne") :
                    (call.Incoming ? "Appel entrant · " : "Appel sortant · ") + call.Result + " · " + call.AtUtc.ToLocalTime().ToString("HH:mm");
                if (call != null && call.DurationSeconds > 0) detail += " · " + call.DurationSeconds + " s";
                using (Font detailFont = WaTheme.Text(8.7f))
                using (SolidBrush detailBrush = new SolidBrush(c.Online && call == null ? WaTheme.Accent : WaTheme.TextGray))
                    g.DrawString(ClipText(g, detail, detailFont, row.Width - WaTheme.S(118)), detailFont, detailBrush,
                        row.X + WaTheme.S(72), row.Y + WaTheme.S(39));
                Rectangle phone = CallsRowButtonRect(row);
                if (_hoverCallButton && _hoverCallPeerId == c.PeerId)
                    using (SolidBrush hover = new SolidBrush(WaTheme.Hover)) g.FillEllipse(hover, phone);
                WaIcons.Phone(g, phone.X + WaTheme.S(9), phone.Y + WaTheme.S(9), WaTheme.S(18), WaTheme.Accent);
                using (Pen line = new Pen(WaTheme.Divider))
                    g.DrawLine(line, row.X + WaTheme.S(72), row.Bottom - 1, row.Right, row.Bottom - 1);
            }
            g.Restore(state);

            if (_callPagePeerId == null || Conv(_callPagePeerId) == null)
            {
                PaintCallsEmptyCenter(g, main);
                return;
            }
            PaintCallContactCenter(g, main, Conv(_callPagePeerId));
        }

        private void PaintCallsEmptyCenter(Graphics g, Rectangle main)
        {
            int cx = main.X + main.Width / 2, cy = main.Y + main.Height / 2;
            int d = WaTheme.S(74);
            using (SolidBrush disc = new SolidBrush(Color.FromArgb(WaTheme.Dark ? 36 : 22, WaTheme.Accent)))
                g.FillEllipse(disc, cx - d / 2, cy - WaTheme.S(110), d, d);
            WaIcons.Phone(g, cx - WaTheme.S(18), cy - WaTheme.S(92), WaTheme.S(36), WaTheme.Accent);
            using (Font f = WaTheme.Semi(17f))
            using (SolidBrush b = new SolidBrush(WaTheme.TextDark))
            {
                string title = "Passez un appel audio";
                SizeF size = g.MeasureString(title, f);
                g.DrawString(title, f, b, cx - size.Width / 2f, cy - WaTheme.S(22));
            }
            using (Font f = WaTheme.Text(10f))
            using (SolidBrush b = new SolidBrush(WaTheme.TextGray))
            {
                string body = CallsPagePeers().Count == 0
                    ? "Les contacts du réseau local apparaîtront dans la liste."
                    : "Choisissez un contact, puis lancez l’appel depuis sa fiche.";
                SizeF size = g.MeasureString(body, f);
                g.DrawString(body, f, b, cx - size.Width / 2f, cy + WaTheme.S(8));
            }
        }

        private void PaintCallContactCenter(Graphics g, Rectangle main, Conversation contact)
        {
            int cx = main.X + main.Width / 2, cy = main.Y + main.Height / 2;
            int d = WaTheme.S(96);
            WaTheme.DrawAvatar(g, new Rectangle(cx - d / 2, cy - WaTheme.S(150), d, d), contact.PeerName, null);
            using (Font f = WaTheme.Semi(18f))
            using (SolidBrush b = new SolidBrush(WaTheme.TextDark))
            {
                SizeF size = g.MeasureString(contact.PeerName, f);
                g.DrawString(contact.PeerName, f, b, cx - size.Width / 2f, cy - WaTheme.S(38));
            }
            string subtitle = contact.Online ? "En ligne" : "Hors ligne";
            using (Font f = WaTheme.Text(10f))
            using (SolidBrush b = new SolidBrush(contact.Online ? WaTheme.Accent : WaTheme.TextGray))
            {
                SizeF size = g.MeasureString(subtitle, f);
                g.DrawString(subtitle, f, b, cx - size.Width / 2f, cy - WaTheme.S(6));
            }
            Rectangle button = CallsMainButtonRect();
            using (SolidBrush b = new SolidBrush(contact.Online ? WaTheme.Accent : WaTheme.TextGray))
            using (GraphicsPath p = WaTheme.RoundRect(button.X, button.Y, button.Width, button.Height, button.Height / 2f))
                g.FillPath(b, p);
            WaIcons.Phone(g, button.X + WaTheme.S(20), button.Y + WaTheme.S(14), WaTheme.S(20), Color.White);
            using (Font f = WaTheme.Semi(10f))
            using (SolidBrush b = new SolidBrush(Color.White))
                g.DrawString("Appel audio", f, b, button.X + WaTheme.S(54), button.Y + WaTheme.S(14));
            Rectangle videoButton = CallsVideoButtonRect();
            using (SolidBrush b = new SolidBrush(contact.Online ? WaTheme.Accent : WaTheme.TextGray))
            using (GraphicsPath p = WaTheme.RoundRect(videoButton.X, videoButton.Y, videoButton.Width, videoButton.Height, videoButton.Height / 2f))
                g.FillPath(b, p);
            using (Font f = WaTheme.Semi(10f))
            using (SolidBrush b = new SolidBrush(Color.White))
            {
                g.DrawString("▣", f, b, videoButton.X + WaTheme.S(22), videoButton.Y + WaTheme.S(14));
                g.DrawString("Appel vidéo", f, b, videoButton.X + WaTheme.S(54), videoButton.Y + WaTheme.S(14));
            }
            CallHistoryItem call = LastCall(contact.PeerId);
            if (call != null)
            {
                using (Font f = WaTheme.Text(9f))
                using (SolidBrush b = new SolidBrush(WaTheme.TextGray))
                {
                    string detail = "Dernier appel : " + call.AtUtc.ToLocalTime().ToString("dd/MM/yyyy à HH:mm") + " · " + call.Result;
                    SizeF size = g.MeasureString(detail, f);
                    g.DrawString(detail, f, b, cx - size.Width / 2f, button.Bottom + WaTheme.S(18));
                }
            }
        }

        private void CallsPageMouseMove(Point p)
        {
            List<Conversation> peers = CallsPagePeers();
            string id = null;
            bool button = false;
            for (int i = 0; i < peers.Count; i++)
            {
                Rectangle row = CallsRowRect(i);
                if (!row.Contains(p)) continue;
                id = peers[i].PeerId;
                button = CallsRowButtonRect(row).Contains(p);
                break;
            }
            if (CallsMainButtonRect().Contains(p) || CallsVideoButtonRect().Contains(p)) { id = _callPagePeerId; button = true; }
            if (id != _hoverCallPeerId || button != _hoverCallButton)
            {
                _hoverCallPeerId = id;
                _hoverCallButton = button;
                Cursor = button ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        private void CallsPageMouseDown(Point p, MouseButtons button)
        {
            if (button != MouseButtons.Left) return;
            if (_callPagePeerId != null && CallsMainButtonRect().Contains(p))
            {
                StartAudioCall(_callPagePeerId);
                return;
            }
            if (_callPagePeerId != null && CallsVideoButtonRect().Contains(p))
            {
                StartVideoCall(_callPagePeerId);
                return;
            }
            List<Conversation> peers = CallsPagePeers();
            for (int i = 0; i < peers.Count; i++)
            {
                Rectangle row = CallsRowRect(i);
                if (!row.Contains(p)) continue;
                Conversation contact = peers[i];
                _callPagePeerId = contact.PeerId;
                if (CallsRowButtonRect(row).Contains(p)) StartAudioCall(contact.PeerId);
                Invalidate();
                return;
            }
        }

        private void CallsPageMouseUp(Point p, MouseButtons button) { }

        private void AddCallHistory(string peerId, string peerName, string result, bool incoming, int durationSeconds)
        {
            if (string.IsNullOrEmpty(peerId)) return;
            _callHistory.Insert(0, new CallHistoryItem
            {
                PeerId = peerId,
                PeerName = peerName ?? "Contact",
                Result = result ?? "Terminé",
                Incoming = incoming,
                AtUtc = DateTime.UtcNow,
                DurationSeconds = durationSeconds
            });
            if (_callHistory.Count > 50) _callHistory.RemoveRange(50, _callHistory.Count - 50);
            if (_activeNav == 1) Invalidate();
        }
    }
}

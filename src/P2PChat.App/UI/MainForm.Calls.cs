using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using P2PChat.App.Audio;
using P2PChat.Core.Protocol;
using SIPSorcery.Net;

namespace P2PChat.App.UI
{
    public sealed partial class MainForm
    {
        private WebRtcAudioCall _audioCall;
        private AudioCallDialog _callDialog;
        private string _callPeerId;
        private string _callId;
        private string _callPeerName;
        private DateTime? _callConnectedAt;
        private bool _callIncoming;
        private bool _callVideo;

        private void StartAudioCall()
        {
            Conversation c = Selected();
            if (c == null || IsGroup(c.PeerId)) return;
            StartAudioCall(c.PeerId);
        }

        private async void StartAudioCall(string peerId)
        { await StartCallAsync(peerId, false); }

        private async void StartVideoCall(string peerId)
        { await StartCallAsync(peerId, true); }

        private async Task StartCallAsync(string peerId, bool video)
        {
            Conversation c = Conv(peerId);
            if (c == null || IsGroup(c.PeerId)) return;
            if (!c.Online)
            {
                MessageBox.Show(this, "Ce contact est hors ligne. L’appel sera possible lorsqu’il se reconnectera.",
                    "Appel audio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_audioCall != null)
            {
                MessageBox.Show(this, "Un appel est déjà en cours.", "P2PChat",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _callPeerId = c.PeerId;
            _callPeerName = c.PeerName;
            _callId = Guid.NewGuid().ToString("N");
            _callConnectedAt = null;
            _callIncoming = false;
            _callVideo = video;
            CreateAudioCall(c.PeerName, false, video);
            try
            {
                string offer = await _audioCall.CreateOfferAsync();
                bool sent = await _net.SendCallSignalAsync(_callPeerId, new CallSignal
                { CallId = _callId, Action = "offer", Sdp = offer, Video = video });
                if (!sent) EndAudioCall(false, "Impossible de joindre ce contact.");
            }
            catch (Exception ex) { EndAudioCall(false, "L’appel n’a pas pu démarrer : " + ex.Message); }
        }

        private async void OnCallSignalReceived(string peerId, CallSignal signal)
        {
            if (_closing || signal == null) return;
            if (signal.Action == "offer")
            {
                if (_audioCall != null)
                {
                    AddCallHistory(peerId, (FindPeer(peerId) ?? new PeerInfo()).Pseudo, "Occupé", true, 0);
                    await _net.SendCallSignalAsync(peerId, new CallSignal
                    { CallId = signal.CallId, Action = "reject" });
                    return;
                }
                PeerInfo peer = FindPeer(peerId);
                string name = peer == null ? "Contact" : peer.Pseudo;
                string callType = signal.Video ? "vidéo" : "audio";
                DialogResult result = MessageBox.Show(this, name + " vous appelle en " + callType + ".\n\nVoulez-vous répondre ?",
                    "Appel " + callType + " entrant", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result != DialogResult.Yes)
                {
                    AddCallHistory(peerId, name, "Refusé", true, 0);
                    await _net.SendCallSignalAsync(peerId, new CallSignal
                    { CallId = signal.CallId, Action = "reject" });
                    return;
                }

                _callPeerId = peerId;
                _callPeerName = name;
                _callId = signal.CallId;
                _callConnectedAt = null;
                _callIncoming = true;
                _callVideo = signal.Video;
                CreateAudioCall(name, true, signal.Video);
                try
                {
                    string answer = await _audioCall.AcceptOfferAsync(signal.Sdp);
                    if (!await _net.SendCallSignalAsync(peerId, new CallSignal
                        { CallId = _callId, Action = "answer", Sdp = answer }))
                        EndAudioCall(false, "La réponse à l’appel n’a pas pu être envoyée.");
                    else if (_callDialog != null) _callDialog.SetIncomingAccepted();
                }
                catch (Exception ex) { EndAudioCall(false, "L’appel a échoué : " + ex.Message); }
                return;
            }

            if (_audioCall == null || peerId != _callPeerId || signal.CallId != _callId) return;
            if (signal.Action == "answer")
            {
                try { _audioCall.ApplyAnswer(signal.Sdp); }
                catch (Exception ex) { EndAudioCall(false, "La connexion a échoué : " + ex.Message); }
            }
            else if (signal.Action == "reject") EndAudioCall(false, "L’appel a été refusé.", "Refusé");
            else if (signal.Action == "hangup") EndAudioCall(false, null);
        }

        private void CreateAudioCall(string peerName, bool incoming, bool video)
        {
            _audioCall = new WebRtcAudioCall(video);
            _audioCall.StateChanged += OnAudioCallStateChanged;
            _audioCall.VideoFrameReceived += OnVideoFrameReceived;
            _audioCall.VideoStatusChanged += OnVideoStatusChanged;
            _callDialog = new AudioCallDialog(peerName, incoming, video);
            _callDialog.HangupRequested += OnHangupRequested;
            _callDialog.MuteToggled += OnMuteToggled;
            _callDialog.FormClosed += OnCallDialogClosed;
            _callDialog.Show(this);
        }

        private void OnVideoFrameReceived(System.Drawing.Bitmap frame)
        {
            if (IsDisposed || !IsHandleCreated) { frame.Dispose(); return; }
            try { BeginInvoke((Action)delegate
            {
                if (_callDialog == null || _callDialog.IsDisposed) { frame.Dispose(); return; }
                _callDialog.SetRemoteFrame(frame);
            }); }
            catch { frame.Dispose(); }
        }

        private void OnVideoStatusChanged(string status)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke((Action)delegate
            { if (_callDialog != null && !_callDialog.IsDisposed) _callDialog.SetVideoStatus(status); }); }
            catch { }
        }

        private void OnAudioCallStateChanged(RTCPeerConnectionState state)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke((Action)delegate
            {
                if (_callDialog == null) return;
                if (state == RTCPeerConnectionState.connected)
                {
                    _callConnectedAt = DateTime.UtcNow;
                    _callDialog.SetCallState("Connecté");
                }
                else if (state == RTCPeerConnectionState.connecting) _callDialog.SetCallState("Connexion en cours…");
                else if (state == RTCPeerConnectionState.failed) EndAudioCall(true, _callVideo ? "Connexion vidéo impossible." : "Connexion audio impossible.");
                else if (state == RTCPeerConnectionState.disconnected) _callDialog.SetCallState("Reconnexion…");
            }); }
            catch (InvalidOperationException) { }
        }

        private void OnMuteToggled()
        {
            if (_audioCall != null) _audioCall.SetMuted(_callDialog != null && _callDialog.Muted);
        }

        private void OnHangupRequested() { EndAudioCall(true, null); }
        private void OnCallDialogClosed(object sender, FormClosedEventArgs e)
        {
            if (_audioCall != null) EndAudioCall(true, null);
        }

        private async void EndAudioCall(bool notifyPeer, string message, string outcome = null)
        {
            WebRtcAudioCall call = _audioCall;
            string peer = _callPeerId, id = _callId;
            string peerName = _callPeerName;
            bool incoming = _callIncoming;
            bool wasConnected = _callConnectedAt.HasValue;
            int duration = _callConnectedAt.HasValue
                ? Math.Max(0, (int)(DateTime.UtcNow - _callConnectedAt.Value).TotalSeconds) : 0;
            if (!string.IsNullOrEmpty(peer))
            {
                if (outcome == null)
                    outcome = wasConnected ? "Terminé" : (incoming ? "Manqué" : "Annulé");
                AddCallHistory(peer, peerName, outcome, incoming, duration);
            }
            AudioCallDialog dialog = _callDialog;
            _audioCall = null;
            _callDialog = null;
            _callPeerId = null;
            _callId = null;
            _callPeerName = null;
            _callConnectedAt = null;
            _callVideo = false;
            if (call != null)
            {
                call.StateChanged -= OnAudioCallStateChanged;
                call.VideoFrameReceived -= OnVideoFrameReceived;
                call.VideoStatusChanged -= OnVideoStatusChanged;
                try { call.Dispose(); } catch { }
            }
            if (dialog != null && !dialog.IsDisposed)
            {
                dialog.HangupRequested -= OnHangupRequested;
                dialog.MuteToggled -= OnMuteToggled;
                dialog.FormClosed -= OnCallDialogClosed;
                try { dialog.Close(); } catch { }
                try { dialog.Dispose(); } catch { }
            }
            if (notifyPeer && !string.IsNullOrEmpty(peer) && !string.IsNullOrEmpty(id))
                await _net.SendCallSignalAsync(peer, new CallSignal { CallId = id, Action = "hangup" });
            if (!string.IsNullOrEmpty(message) && !_closing)
                MessageBox.Show(this, message, "Appel audio", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}

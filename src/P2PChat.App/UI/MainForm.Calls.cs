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

        private async void StartAudioCall()
        {
            Conversation c = Selected();
            if (c == null || IsGroup(c.PeerId)) return;
            if (_audioCall != null)
            {
                MessageBox.Show(this, "Un appel est déjà en cours.", "P2PChat",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _callPeerId = c.PeerId;
            _callId = Guid.NewGuid().ToString("N");
            CreateAudioCall(c.PeerName, false);
            try
            {
                string offer = await _audioCall.CreateOfferAsync();
                bool sent = await _net.SendCallSignalAsync(_callPeerId, new CallSignal
                { CallId = _callId, Action = "offer", Sdp = offer });
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
                    await _net.SendCallSignalAsync(peerId, new CallSignal
                    { CallId = signal.CallId, Action = "reject" });
                    return;
                }
                PeerInfo peer = FindPeer(peerId);
                string name = peer == null ? "Contact" : peer.Pseudo;
                DialogResult result = MessageBox.Show(this, name + " vous appelle en audio.\n\nVoulez-vous répondre ?",
                    "Appel audio entrant", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result != DialogResult.Yes)
                {
                    await _net.SendCallSignalAsync(peerId, new CallSignal
                    { CallId = signal.CallId, Action = "reject" });
                    return;
                }

                _callPeerId = peerId;
                _callId = signal.CallId;
                CreateAudioCall(name, true);
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
            else if (signal.Action == "reject") EndAudioCall(false, "L’appel a été refusé.");
            else if (signal.Action == "hangup") EndAudioCall(false, "Appel terminé.");
        }

        private void CreateAudioCall(string peerName, bool incoming)
        {
            _audioCall = new WebRtcAudioCall();
            _audioCall.StateChanged += OnAudioCallStateChanged;
            _callDialog = new AudioCallDialog(peerName, incoming);
            _callDialog.HangupRequested += OnHangupRequested;
            _callDialog.MuteToggled += OnMuteToggled;
            _callDialog.FormClosed += OnCallDialogClosed;
            _callDialog.Show(this);
        }

        private void OnAudioCallStateChanged(RTCPeerConnectionState state)
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke((Action)delegate
            {
                if (_callDialog == null) return;
                if (state == RTCPeerConnectionState.connected) _callDialog.SetCallState("Connecté");
                else if (state == RTCPeerConnectionState.connecting) _callDialog.SetCallState("Connexion en cours…");
                else if (state == RTCPeerConnectionState.failed) EndAudioCall(true, "Connexion audio impossible.");
                else if (state == RTCPeerConnectionState.disconnected) _callDialog.SetCallState("Reconnexion…");
            });
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

        private async void EndAudioCall(bool notifyPeer, string message)
        {
            WebRtcAudioCall call = _audioCall;
            string peer = _callPeerId, id = _callId;
            AudioCallDialog dialog = _callDialog;
            _audioCall = null;
            _callDialog = null;
            _callPeerId = null;
            _callId = null;
            if (call != null)
            {
                call.StateChanged -= OnAudioCallStateChanged;
                call.Dispose();
            }
            if (dialog != null && !dialog.IsDisposed)
            {
                dialog.HangupRequested -= OnHangupRequested;
                dialog.MuteToggled -= OnMuteToggled;
                dialog.FormClosed -= OnCallDialogClosed;
                dialog.Close();
                dialog.Dispose();
            }
            if (notifyPeer && !string.IsNullOrEmpty(peer) && !string.IsNullOrEmpty(id))
                await _net.SendCallSignalAsync(peer, new CallSignal { CallId = id, Action = "hangup" });
            if (!string.IsNullOrEmpty(message) && !_closing)
                MessageBox.Show(this, message, "Appel audio", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}

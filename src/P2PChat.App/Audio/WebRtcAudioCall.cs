using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

namespace P2PChat.App.Audio
{
    /// <summary>Micro, Opus et lecture audio pour un appel WebRTC individuel.</summary>
    internal sealed class WebRtcAudioCall : IDisposable
    {
        private const int SampleRate = 48000;
        private const int SamplesPerPacket = 960; // 20 ms, mono, 48 kHz
        private readonly AudioEncoder _encoder = new AudioEncoder(false, true);
        private readonly object _audioLock = new object();
        private readonly List<short> _capturePending = new List<short>(SamplesPerPacket * 2);
        private RTCPeerConnection _peer;
        private WaveInEvent _microphone;
        private WaveOutEvent _speaker;
        private BufferedWaveProvider _playback;
        private AudioFormat _sendFormat;
        private bool _hasSendFormat;
        private bool _muted;
        private int _disposeStarted;
        private volatile bool _disposed;

        internal event Action<RTCPeerConnectionState> StateChanged;

        internal bool IsMuted { get { return _muted; } }

        internal async Task<string> CreateOfferAsync()
        {
            CreatePeer();
            RTCSessionDescriptionInit offer = _peer.createOffer(null);
            await SetLocalAndWaitForIceAsync(offer).ConfigureAwait(false);
            return _peer.localDescription == null ? offer.sdp : _peer.localDescription.sdp.ToString();
        }

        internal async Task<string> AcceptOfferAsync(string remoteSdp)
        {
            CreatePeer();
            RTCSessionDescriptionInit remote = new RTCSessionDescriptionInit
            {
                type = RTCSdpType.offer,
                sdp = remoteSdp
            };
            _peer.setRemoteDescription(remote);
            RTCSessionDescriptionInit answer = _peer.createAnswer(null);
            await SetLocalAndWaitForIceAsync(answer).ConfigureAwait(false);
            return _peer.localDescription == null ? answer.sdp : _peer.localDescription.sdp.ToString();
        }

        internal void ApplyAnswer(string remoteSdp)
        {
            if (_peer == null) throw new InvalidOperationException("L'appel n'est pas initialisé.");
            _peer.setRemoteDescription(new RTCSessionDescriptionInit
            {
                type = RTCSdpType.answer,
                sdp = remoteSdp
            });
        }

        internal void SetMuted(bool muted)
        {
            _muted = muted;
        }

        private void CreatePeer()
        {
            if (_peer != null) return;
            _peer = new RTCPeerConnection();
            _peer.addTrack(new MediaStreamTrack(
                new List<AudioFormat> { AudioCommonlyUsedFormats.OpusWebRTC },
                MediaStreamStatusEnum.SendRecv));
            _peer.OnAudioFormatsNegotiated += OnAudioFormatsNegotiated;
            _peer.OnAudioFrameReceived += OnAudioFrameReceived;
            _peer.onconnectionstatechange += OnConnectionStateChanged;
        }

        private async Task SetLocalAndWaitForIceAsync(RTCSessionDescriptionInit description)
        {
            TaskCompletionSource<bool> gathered = new TaskCompletionSource<bool>();
            Action<RTCIceGatheringState> callback = null;
            callback = delegate(RTCIceGatheringState state)
            {
                if (state == RTCIceGatheringState.complete) gathered.TrySetResult(true);
            };
            _peer.onicegatheringstatechange += callback;
            try
            {
                await _peer.setLocalDescription(description).ConfigureAwait(false);
                if (_peer.iceGatheringState != RTCIceGatheringState.complete)
                    await Task.WhenAny(gathered.Task, Task.Delay(10000)).ConfigureAwait(false);
            }
            finally { _peer.onicegatheringstatechange -= callback; }
        }

        private void OnConnectionStateChanged(RTCPeerConnectionState state)
        {
            Action<RTCPeerConnectionState> handler = StateChanged;
            try { if (handler != null) handler(state); } catch { }
            if (state == RTCPeerConnectionState.connected)
            {
                try { StartAudioDevices(); }
                catch
                {
                    try { if (handler != null) handler(RTCPeerConnectionState.failed); } catch { }
                }
            }
            else if (state == RTCPeerConnectionState.failed || state == RTCPeerConnectionState.closed)
                StopAudioDevices();
        }

        private void OnAudioFormatsNegotiated(List<AudioFormat> formats)
        {
            if (_disposed || formats == null) return;
            _sendFormat = formats.FirstOrDefault(f => f.Codec == AudioCodecsEnum.OPUS);
            if (formats.Count > 0)
            {
                if (!_sendFormat.IsEmpty()) _hasSendFormat = true;
                else { _sendFormat = formats[0]; _hasSendFormat = true; }
            }
        }

        private void OnAudioFrameReceived(EncodedAudioFrame frame)
        {
            if (_disposed || frame == null) return;
            try
            {
                lock (_audioLock)
                {
                    if (_disposed || _playback == null) return;
                    short[] pcm = _encoder.DecodeAudio(frame.EncodedAudio, frame.AudioFormat);
                    byte[] bytes = new byte[pcm.Length * 2];
                    Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);
                    _playback.AddSamples(bytes, 0, bytes.Length);
                }
            }
            catch { }
        }

        private void StartAudioDevices()
        {
            lock (_audioLock)
            {
                if (_disposed || _microphone != null) return;
                try
                {
                    WaveFormat format = new WaveFormat(SampleRate, 16, 1);
                    _playback = new BufferedWaveProvider(format) { DiscardOnBufferOverflow = true };
                    _speaker = new WaveOutEvent();
                    _speaker.Init(_playback);
                    _speaker.Play();

                    _microphone = new WaveInEvent
                    {
                        WaveFormat = format,
                        BufferMilliseconds = 20,
                        NumberOfBuffers = 3
                    };
                    _microphone.DataAvailable += OnMicrophoneData;
                    _microphone.StartRecording();
                }
                catch
                {
                    StopAudioDevicesLocked();
                    throw;
                }
            }
        }

        private void OnMicrophoneData(object sender, WaveInEventArgs e)
        {
            if (_disposed || _muted || _peer == null || !_hasSendFormat || e.BytesRecorded < 2) return;
            int samples = e.BytesRecorded / 2;
            short[] pcm = new short[samples];
            Buffer.BlockCopy(e.Buffer, 0, pcm, 0, samples * 2);
            lock (_audioLock)
            {
                _capturePending.AddRange(pcm);
                while (_capturePending.Count >= SamplesPerPacket)
                {
                    short[] packet = _capturePending.GetRange(0, SamplesPerPacket).ToArray();
                    _capturePending.RemoveRange(0, SamplesPerPacket);
                    try
                    {
                        byte[] encoded = _encoder.EncodeAudio(packet, _sendFormat);
                        if (encoded != null && encoded.Length > 0) _peer.SendAudio(SamplesPerPacket, encoded);
                    }
                    catch { }
                }
            }
        }

        private void StopAudioDevices()
        {
            WaveInEvent microphone;
            WaveOutEvent speaker;
            lock (_audioLock)
            {
                microphone = _microphone;
                speaker = _speaker;
                _microphone = null;
                _speaker = null;
                _playback = null;
                _capturePending.Clear();
            }
            if (microphone != null)
            {
                microphone.DataAvailable -= OnMicrophoneData;
                try { microphone.StopRecording(); } catch { }
                try { microphone.Dispose(); } catch { }
            }
            if (speaker != null)
            {
                try { speaker.Stop(); } catch { }
                try { speaker.Dispose(); } catch { }
            }
        }

        private void StopAudioDevicesLocked()
        {
            if (_microphone != null)
            {
                _microphone.DataAvailable -= OnMicrophoneData;
                try { _microphone.StopRecording(); } catch { }
                _microphone.Dispose();
                _microphone = null;
            }
            if (_speaker != null)
            {
                try { _speaker.Stop(); } catch { }
                _speaker.Dispose();
                _speaker = null;
            }
            _playback = null;
            _capturePending.Clear();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeStarted, 1) != 0) return;
            _disposed = true;
            StopAudioDevices();
            RTCPeerConnection peer = _peer;
            _peer = null;
            if (peer != null)
            {
                try { peer.OnAudioFormatsNegotiated -= OnAudioFormatsNegotiated; } catch { }
                try { peer.OnAudioFrameReceived -= OnAudioFrameReceived; } catch { }
                try { peer.onconnectionstatechange -= OnConnectionStateChanged; } catch { }
                try { peer.close(); } catch { }
            }
            try { _encoder.Dispose(); } catch { }
        }
    }
}

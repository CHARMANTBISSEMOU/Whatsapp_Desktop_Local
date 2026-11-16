using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace P2PChat.App.Audio
{
    /// <summary>
    /// Enregistreur de messages vocaux v2 — API winmm waveIn* (robuste) :
    /// PCM 16 kHz / 16 bits / mono, fausse pas MCI (échouait sur bazı machines).
    ///  - Start(path) ouvre le micro et écrit un vrai WAV au fil de l'eau ;
    ///  - PeakLevel : VRAI niveau crête mesuré (RMS par buffer) pour l'animation ;
    ///  - Envelope() : 30 barres façon WhatsApp (min/max du signal) transmises
    ///    au pair dans le message (base64) ;
    ///  - Stop() finalise le WAV + durée réelle ; Cancel() jette tout.
    /// Thread-safe, aucun lock sous callback OS.
    /// </summary>
    public sealed class VoiceRecorder : IDisposable
    {
        #region winmm (waveIn)
        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEFORMATEX
        {
            public short wFormatTag;
            public short nChannels;
            public int nSamplesPerSec;
            public int nAvgBytesPerSec;
            public short nBlockAlign;
            public short wBitsPerSample;
            public short cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEHDR
        {
            public IntPtr lpData;
            public int dwBufferLength;
            public int dwBytesRecorded;
            public IntPtr dwUser;
            public int dwFlags;
            public int dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        private const int WAVE_MAPPER = -1;
        private const int WAVE_FORMAT_PCM = 1;
        private const int WHDR_DONE = 0x1;
        private const int CALLBACK_FUNCTION = 0x30000;

        [DllImport("winmm.dll")]
        private static extern int waveInGetNumDevs();

        [DllImport("winmm.dll")]
        private static extern int waveInOpen(out IntPtr h, int dev, ref WAVEFORMATEX fmt,
            WaveInProc proc, IntPtr instance, int flags);

        [DllImport("winmm.dll")]
        private static extern int waveInPrepareHeader(IntPtr h, IntPtr hdr, int size);

        [DllImport("winmm.dll")]
        private static extern int waveInUnprepareHeader(IntPtr h, IntPtr hdr, int size);

        [DllImport("winmm.dll")]
        private static extern int waveInAddBuffer(IntPtr h, IntPtr hdr, int size);

        [DllImport("winmm.dll")]
        private static extern int waveInStart(IntPtr h);

        [DllImport("winmm.dll")]
        private static extern int waveInStop(IntPtr h);

        [DllImport("winmm.dll")]
        private static extern int waveInClose(IntPtr h);

        [DllImport("winmm.dll")]
        private static extern int waveInGetErrorText(int err, StringBuilder txt, int len);

        private delegate void WaveInProc(IntPtr h, int msg, IntPtr instance, IntPtr wavhdr, IntPtr p2);
        #endregion

        public const int SamplesPerSec = 16000;
        public const int BytesPerSec = SamplesPerSec * 2; // 16 bits mono
        private const int BufCount = 6;
        private const int BufSamples = SamplesPerSec / 10; // 100 ms par buffer
        private const int BufBytes = BufSamples * 2;

        private readonly object _gate = new object();
        private IntPtr _waveIn = IntPtr.Zero;
        private readonly IntPtr[] _headers = new IntPtr[BufCount];
        private readonly IntPtr[] _buffers = new IntPtr[BufCount];
        private WaveInProc _proc; // garde une référence : sinon GC du délégué
        private FileStream _file;
        private long _dataBytes;
        private volatile bool _recording;
        private string _path;
        private volatile int _peak;
        private readonly List<byte> _envelopeHi = new List<byte>(256);
        private readonly object _envLock = new object();
        private static readonly byte[] WavHeader = BuildWavHeader();

        public bool IsRecording { get { return _recording; } }

        /// <summary>Niveau crête 0..100 mesuré (pour l'animation de la saisie).</summary>
        public int PeakLevel { get { return _peak; } }

        /// <summary>Durée enregistrée jusqu'ici (secondes).</summary>
        public int ElapsedSeconds { get { return (int)(_dataBytes / (long)BytesPerSec); } }

        /// <summary>Ouvre le micro et démarre l'enregistrement vers wavOutputPath.</summary>
        public void Start(string wavOutputPath)
        {
            lock (_gate)
            {
                if (_recording)
                {
                    throw new InvalidOperationException("Un enregistrement est déjà en cours.");
                }
                if (wavOutputPath == null || wavOutputPath.Length == 0)
                {
                    throw new ArgumentException("Chemin de sortie absent.");
                }
                if (waveInGetNumDevs() == 0)
                {
                    throw new InvalidOperationException(
                        "Aucun microphone n'est disponible sur cette machine. Branchez-en un ou vérifiez Windows (Paramètres → Confidentialité → Micro).");
                }

                try
                {
                    string dir = Path.GetDirectoryName(wavOutputPath);
                    if (dir != null && dir.Length > 0) Directory.CreateDirectory(dir);
                    if (File.Exists(wavOutputPath)) File.Delete(wavOutputPath);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Impossible de créer le fichier audio : " + ex.Message, ex);
                }

                WAVEFORMATEX fmt = new WAVEFORMATEX();
                fmt.wFormatTag = WAVE_FORMAT_PCM;
                fmt.nChannels = 1;
                fmt.nSamplesPerSec = SamplesPerSec;
                fmt.wBitsPerSample = 16;
                fmt.nBlockAlign = 2;
                fmt.nAvgBytesPerSec = BytesPerSec;
                fmt.cbSize = 0;

                _proc = new WaveInProc(OnWaveIn);
                IntPtr h;
                int err = waveInOpen(out h, WAVE_MAPPER, ref fmt, _proc, IntPtr.Zero, CALLBACK_FUNCTION);
                if (err != 0)
                {
                    _proc = null;
                    throw new InvalidOperationException("Ouverture du micro impossible (erreur " + err + " : " + ErrText(err) + ").");
                }
                _waveIn = h;

                _path = wavOutputPath;
                _dataBytes = 0;
                _peak = 0;
                lock (_envLock) _envelopeHi.Clear();

                try
                {
                    _file = new FileStream(wavOutputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                    _file.Write(WavHeader, 0, WavHeader.Length);
                }
                catch (Exception ex)
                {
                    CleanupDevice();
                    throw new InvalidOperationException("Écriture du fichier audio impossible : " + ex.Message, ex);
                }

                for (int i = 0; i < BufCount; i++)
                {
                    _headers[i] = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WAVEHDR)));
                    _buffers[i] = Marshal.AllocHGlobal(BufBytes);
                    WAVEHDR hdr = new WAVEHDR();
                    hdr.lpData = _buffers[i];
                    hdr.dwBufferLength = BufBytes;
                    Marshal.StructureToPtr(hdr, _headers[i], false);
                    int p = waveInPrepareHeader(_waveIn, _headers[i], Marshal.SizeOf(typeof(WAVEHDR)));
                    if (p != 0)
                    {
                        CleanupDevice();
                        throw new InvalidOperationException("Préparation du tampon impossible (erreur " + p + ").");
                    }
                    int a = waveInAddBuffer(_waveIn, _headers[i], Marshal.SizeOf(typeof(WAVEHDR)));
                    if (a != 0)
                    {
                        CleanupDevice();
                        throw new InvalidOperationException("File d'enregistrement impossible (erreur " + a + ").");
                    }
                }

                int s = waveInStart(_waveIn);
                if (s != 0)
                {
                    CleanupDevice();
                    throw new InvalidOperationException("Démarrage de l'enregistrement impossible (erreur " + s + ").");
                }
                _recording = true;
            }
        }

        // callback winmm : jamais d'exception, aucun lock long
        private void OnWaveIn(IntPtr h, int msg, IntPtr instance, IntPtr wavhdr, IntPtr p2)
        {
            const int WIM_DATA = 0x3C0;
            if (msg != WIM_DATA || wavhdr == IntPtr.Zero) return;
            try
            {
                WAVEHDR hdr = (WAVEHDR)Marshal.PtrToStructure(wavhdr, typeof(WAVEHDR));
                int n = hdr.dwBytesRecorded;
                if (n > 0 && _recording)
                {
                    byte[] copy = new byte[n];
                    Marshal.Copy(hdr.lpData, copy, 0, n);
                    _dataBytes += n;
                    _peak = Math.Min(100, SquarePeak(copy, n));
                    lock (_envLock)
                    {
                        for (int i = 0; i + 1 < n; i += 2)
                        {
                            short v = (short)(copy[i] | (copy[i + 1] << 8));
                            int a = Math.Abs(v) / 128;
                            if (a > 255) a = 255;
                            if (_envelopeHi.Count < 900) _envelopeHi.Add((byte)a);
                        }
                    }
                    FileStream f = _file;
                    if (f != null)
                    {
                        f.BeginWrite(copy, 0, n, delegate(IAsyncResult ar)
                        {
                            try { f.EndWrite(ar); } catch (Exception) { }
                        }, null);
                    }
                }
                if (_recording)
                {
                    waveInAddBuffer(h, wavhdr, Marshal.SizeOf(typeof(WAVEHDR)));
                }
            }
            catch (Exception)
            {
            }
        }

        private static int SquarePeak(byte[] buf, int n)
        {
            int max = 0;
            for (int i = 0; i + 1 < n; i += 2)
            {
                short v = (short)(buf[i] | (buf[i + 1] << 8));
                int a = Math.Abs(v);
                if (a > max) max = a;
            }
            return (max * 100) / short.MaxValue;
        }

        /// <summary>Arrête, finalise le WAV, renvoie la durée en secondes réelles.</summary>
        public int Stop()
        {
            lock (_gate)
            {
                if (!_recording) return 0;
                _recording = false;
                int secs = (int)(_dataBytes / (long)BytesPerSec);

                if (_waveIn != IntPtr.Zero)
                {
                    try { waveInStop(_waveIn); } catch (Exception) { }
                }
                // laisser finir les écritures asynchrones
                Thread.Sleep(120);
                CleanupDevice();

                FileStream f = _file;
                _file = null;
                if (f != null)
                {
                    try
                    {
                        f.Flush();
                        f.Close();
                        FixWavHeaderLength(_path, _dataBytes);
                    }
                    catch (Exception) { }
                }
                return secs;
            }
        }

        /// <summary>Annule : ferme le micro et supprime le fichier.</summary>
        public void Cancel()
        {
            lock (_gate)
            {
                if (!_recording) return;
                _recording = false;
                string path = _path;
                CleanupDevice();
                FileStream f = _file;
                _file = null;
                if (f != null)
                {
                    try { f.Close(); } catch (Exception) { }
                }
                if (path != null)
                {
                    try { if (File.Exists(path)) File.Delete(path); } catch (Exception) { }
                }
                _path = null;
            }
        }

        /// <summary>30 barres 0-255 façon WhatsApp (base64), pour le bulle vocale.</summary>
        public string EnvelopeB64()
        {
            byte[] bars;
            lock (_envLock)
            {
                bars = new byte[30];
                if (_envelopeHi.Count == 0) return null;
                int perChunk = Math.Max(1, _envelopeHi.Count / 30);
                for (int b = 0; b < 30; b++)
                {
                    int from = b * (_envelopeHi.Count / 30);
                    int to = Math.Min(_envelopeHi.Count, from + perChunk);
                    int max = 8; // plancher visible
                    for (int i = from; i < to; i++)
                    {
                        if (_envelopeHi[i] > max) max = _envelopeHi[i];
                    }
                    bars[b] = (byte)Math.Min(255, max);
                }
            }
            return Convert.ToBase64String(bars);
        }

        private static string ErrText(int err)
        {
            StringBuilder sb = new StringBuilder(256);
            try
            {
                if (waveInGetErrorText(err, sb, 256) == 0 && sb.Length > 0) return sb.ToString();
            }
            catch (Exception) { }
            return "erreur audio";
        }

        private void CleanupDevice()
        {
            if (_waveIn != IntPtr.Zero)
            {
                try { waveInStop(_waveIn); } catch (Exception) { }
                for (int i = 0; i < BufCount; i++)
                {
                    if (_headers[i] != IntPtr.Zero)
                    {
                        try { waveInUnprepareHeader(_waveIn, _headers[i], Marshal.SizeOf(typeof(WAVEHDR))); }
                        catch (Exception) { }
                    }
                }
                try { waveInClose(_waveIn); } catch (Exception) { }
                _waveIn = IntPtr.Zero;
            }
            for (int i = 0; i < BufCount; i++)
            {
                if (_headers[i] != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_headers[i]);
                    _headers[i] = IntPtr.Zero;
                }
                if (_buffers[i] != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_buffers[i]);
                    _buffers[i] = IntPtr.Zero;
                }
            }
            _proc = null;
        }

        private static byte[] BuildWavHeader()
        {
            byte[] h = new byte[44];
            h[0] = (byte)'R'; h[1] = (byte)'I'; h[2] = (byte)'F'; h[3] = (byte)'F';
            h[8] = (byte)'W'; h[9] = (byte)'A'; h[10] = (byte)'V'; h[11] = (byte)'E';
            h[12] = (byte)'f'; h[13] = (byte)'m'; h[14] = (byte)'t'; h[15] = (byte)' ';
            h[16] = 16; // taille chunk fmt
            h[20] = 1; // PCM
            h[22] = 1; // mono
            h[24] = (byte)(SamplesPerSec & 0xFF);
            h[25] = (byte)((SamplesPerSec >> 8) & 0xFF);
            h[26] = (byte)((SamplesPerSec >> 16) & 0xFF);
            h[27] = (byte)((SamplesPerSec >> 24) & 0xFF);
            int bps = BytesPerSec;
            h[28] = (byte)(bps & 0xFF);
            h[29] = (byte)((bps >> 8) & 0xFF);
            h[30] = (byte)((bps >> 16) & 0xFF);
            h[31] = (byte)((bps >> 24) & 0xFF);
            h[32] = 2; // block align
            h[34] = 16; // bits
            h[36] = (byte)'d'; h[37] = (byte)'a'; h[38] = (byte)'t'; h[39] = (byte)'a';
            return h;
        }

        private static void FixWavHeaderLength(string path, long dataBytes)
        {
            if (path == null) return;
            try
            {
                using (FileStream f = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read))
                {
                    byte[] riff = BitConverter.GetBytes((int)(36 + dataBytes));
                    byte[] data = BitConverter.GetBytes((int)dataBytes);
                    f.Seek(4, SeekOrigin.Begin);
                    f.Write(riff, 0, 4);
                    f.Seek(40, SeekOrigin.Begin);
                    f.Write(data, 0, 4);
                }
            }
            catch (Exception) { }
        }

        public void Dispose()
        {
            try { Cancel(); } catch (Exception) { }
        }
    }
}

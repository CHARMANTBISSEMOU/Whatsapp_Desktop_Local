using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace P2PChat.App.Audio
{
    /// <summary>
    /// Lecteur de messages vocaux v2 — API winmm waveOut :
    ///  - lecture PCM WAV (16/24/32 bits) via propre file de tampons ;
    ///  - Pause()/Resume() comme WhatsApp desktop ;
    ///  - ProgressMs : position EXACTE (waveOutGetPosition) pour dessiner
    ///    l'arc vert autour du bouton lecture ;
    ///  - LengthMs : durée déduite de l'en-tête WAV ;
    ///  - PlaybackDone TOUJOURS levé (fin naturelle, Stop, nouveau Play).
    /// </summary>
    public sealed class VoicePlayer : IDisposable
    {
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

        [StructLayout(LayoutKind.Sequential)]
        private struct MMTIME
        {
            public int wType;
            public int u; // samples (type TIME_SAMPLES = 1)
            public int cb;
        }

        private const int WAVE_MAPPER = -1;
        private const int TIME_SAMPLES = 1;
        private const int WHDR_DONE = 0x1;
        private const int QUEUE = 4;

        [DllImport("winmm.dll")]
        private static extern int waveOutOpen(out IntPtr h, int dev, ref WAVEFORMATEX fmt,
            IntPtr callback, IntPtr instance, int flags);

        [DllImport("winmm.dll")]
        private static extern int waveOutWrite(IntPtr h, IntPtr hdr, int size);

        [DllImport("winmm.dll")]
        private static extern int waveOutPrepareHeader(IntPtr h, IntPtr hdr, int size);

        [DllImport("winmm.dll")]
        private static extern int waveOutUnprepareHeader(IntPtr h, IntPtr hdr, int size);

        [DllImport("winmm.dll")]
        private static extern int waveOutPause(IntPtr h);

        [DllImport("winmm.dll")]
        private static extern int waveOutRestart(IntPtr h);

        [DllImport("winmm.dll")]
        private static extern int waveOutReset(IntPtr h);

        [DllImport("winmm.dll")]
        private static extern int waveOutClose(IntPtr h);

        [DllImport("winmm.dll")]
        private static extern int waveOutGetPosition(IntPtr h, ref MMTIME mmt, int size);

        private readonly object _gate = new object();
        private IntPtr _hOut = IntPtr.Zero;
        private Thread _thread;
        private volatile bool _playing;
        private volatile bool _paused;
        private volatile bool _stopRequested;
        private string _wavPath;
        private WAVEFORMATEX _fmt;
        private long _dataBytes;
        private int _dataOffset;

        /// <summary>Fin de lecture (toujours levé via le thread de lecture).</summary>
        public event EventHandler PlaybackDone;

        public bool IsPlaying { get { return _playing; } }
        public bool IsPaused { get { return _playing && _paused; } }

        /// <summary>Durée totale du dernier son chargé (millisecondes).</summary>
        public int LengthMs { get; private set; }

        /// <summary>Position de lecture exacte en millisecondes.</summary>
        public int ProgressMs
        {
            get
            {
                IntPtr h = _hOut;
                if (h == IntPtr.Zero || !_playing) return 0;
                try
                {
                    MMTIME mm = new MMTIME();
                    mm.wType = TIME_SAMPLES;
                    if (waveOutGetPosition(h, ref mm, Marshal.SizeOf(typeof(MMTIME))) == 0
                        && mm.wType == TIME_SAMPLES && _fmt.nSamplesPerSec > 0)
                    {
                        return (int)(mm.u * 1000L / _fmt.nSamplesPerSec);
                    }
                }
                catch (Exception) { }
                return 0;
            }
        }

        /// <summary>Lance la lecture (interrompt la précédente). Reprise si même son en pause.</summary>
        public void Play(string wavPath)
        {
            if (wavPath == null || !File.Exists(wavPath)) return;

            lock (_gate)
            {
                if (_playing && _paused && string.Equals(wavPath, _wavPath, StringComparison.OrdinalIgnoreCase))
                {
                    _paused = false;
                    if (_hOut != IntPtr.Zero) try { waveOutRestart(_hOut); } catch (Exception) { }
                    return;
                }
                StopInternal();
                if (!ParseWav(wavPath)) return;

                IntPtr h;
                WAVEFORMATEX fmt = _fmt;
                int err = waveOutOpen(out h, WAVE_MAPPER, ref fmt, IntPtr.Zero, IntPtr.Zero, 0);
                if (err != 0)
                {
                    _playing = false;
                    return;
                }
                _hOut = h;
                _wavPath = wavPath;
                _paused = false;
                _stopRequested = false;
                _playing = true;
                _thread = new Thread(PlayLoop);
                _thread.IsBackground = true;
                _thread.Name = "VoicePlayer.Play";
                _thread.Start(wavPath);
            }
        }

        /// <summary>Met en pause (reprise par Play, comme WhatsApp desktop).</summary>
        public void Pause()
        {
            if (!_playing || _paused) return;
            _paused = true;
            IntPtr h = _hOut;
            if (h != IntPtr.Zero) try { waveOutPause(h); } catch (Exception) { }
        }

        /// <summary>Arrête tout (idempotent). PlaybackDone levé une unique fois.</summary>
        public void Stop()
        {
            lock (_gate)
            {
                StopInternal();
            }
        }

        private void StopInternal()
        {
            _stopRequested = true;
            _paused = false;
            Thread t = _thread;
            _thread = null;
            if (t != null && t.IsAlive)
            {
                try { t.Join(900); } catch (Exception) { }
            }
            CloseDevice();
            if (_playing)
            {
                _playing = false;
                EventHandler h = PlaybackDone;
                if (h != null)
                {
                    try { h(this, EventArgs.Empty); }
                    catch (Exception) { }
                }
            }
            _playing = false;
        }

        private void CloseDevice()
        {
            IntPtr h = _hOut;
            _hOut = IntPtr.Zero;
            if (h != IntPtr.Zero)
            {
                try { waveOutReset(h); } catch (Exception) { }
                try { waveOutClose(h); } catch (Exception) { }
            }
        }

        private bool _disposed;

        private void PlayLoop(object state)
        {
            string path = (string)state;
            IntPtr[] hdrs = new IntPtr[QUEUE];
            IntPtr[] bufs = new IntPtr[QUEUE];
            int bufBytes = Math.Max(3200, _fmt.nAvgBytesPerSec / 5); // ~200 ms
            bool[] queued = new bool[QUEUE];
            try
            {
                for (int i = 0; i < QUEUE; i++)
                {
                    hdrs[i] = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WAVEHDR)));
                    bufs[i] = Marshal.AllocHGlobal(bufBytes);
                }

                using (FileStream f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (_dataOffset > 0) f.Seek(_dataOffset, SeekOrigin.Begin);
                    long left = _dataBytes > 0 ? _dataBytes : long.MaxValue;
                    while (!_stopRequested && !_disposed)
                    {
                        while (_paused && !_stopRequested)
                        {
                            Thread.Sleep(40);
                        }
                        if (_stopRequested) break;

                        int slot = -1;
                        for (int i = 0; i < QUEUE; i++)
                        {
                            if (!queued[i]) { slot = i; break; }
                            WAVEHDR h = (WAVEHDR)Marshal.PtrToStructure(hdrs[i], typeof(WAVEHDR));
                            if ((h.dwFlags & WHDR_DONE) != 0)
                            {
                                try { waveOutUnprepareHeader(_hOut, hdrs[i], Marshal.SizeOf(typeof(WAVEHDR))); }
                                catch (Exception) { }
                                queued[i] = false;
                                slot = i;
                                break;
                            }
                        }

                        if (slot < 0)
                        {
                            Thread.Sleep(25);
                            continue;
                        }

                        if (left <= 0)
                        {
                            bool anyPending = false;
                            for (int i = 0; i < QUEUE; i++)
                            {
                                if (!queued[i]) continue;
                                WAVEHDR h = (WAVEHDR)Marshal.PtrToStructure(hdrs[i], typeof(WAVEHDR));
                                if ((h.dwFlags & WHDR_DONE) == 0) { anyPending = true; break; }
                            }
                            if (anyPending) { Thread.Sleep(25); continue; }
                            break; // fin naturelle
                        }

                        int toRead = (int)Math.Min(bufBytes, left);
                        byte[] data = new byte[toRead];
                        int n = f.Read(data, 0, toRead);
                        if (n <= 0) break;
                        left -= n;

                        Marshal.Copy(data, 0, bufs[slot], n);
                        WAVEHDR hdr = new WAVEHDR();
                        hdr.lpData = bufs[slot];
                        hdr.dwBufferLength = n;
                        Marshal.StructureToPtr(hdr, hdrs[slot], false);
                        if (waveOutPrepareHeader(_hOut, hdrs[slot], Marshal.SizeOf(typeof(WAVEHDR))) == 0)
                        {
                            if (waveOutWrite(_hOut, hdrs[slot], Marshal.SizeOf(typeof(WAVEHDR))) == 0)
                            {
                                queued[slot] = true;
                            }
                            else
                            {
                                try { waveOutUnprepareHeader(_hOut, hdrs[slot], Marshal.SizeOf(typeof(WAVEHDR))); }
                                catch (Exception) { }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                for (int i = 0; i < QUEUE; i++)
                {
                    if (hdrs[i] != IntPtr.Zero) Marshal.FreeHGlobal(hdrs[i]);
                    if (bufs[i] != IntPtr.Zero) Marshal.FreeHGlobal(bufs[i]);
                }
                bool natural = false;
                lock (_gate)
                {
                    CloseDevice();
                    if (_playing && !_stopRequested)
                    {
                        _playing = false;
                        natural = true;
                    }
                    _playing = false;
                }
                if (natural)
                {
                    EventHandler h = PlaybackDone;
                    if (h != null)
                    {
                        try { h(this, EventArgs.Empty); }
                        catch (Exception) { }
                    }
                }
            }
        }

        /// <summary>Parse l'en-tête RIFF/WAVE (PCM uniquement). Remplit _fmt/_dataBytes.</summary>
        private bool ParseWav(string path)
        {
            try
            {
                using (FileStream f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader r = new BinaryReader(f))
                {
                    if (f.Length < 44) return false;
                    if (r.ReadUInt32() != 0x46464952) return false; // "RIFF"
                    r.ReadUInt32(); // taille
                    if (r.ReadUInt32() != 0x45564157) return false; // "WAVE"

                    int sampleRate = 16000, bits = 16, channels = 1;
                    long dataBytes = 0, dataOffset = 0;

                    for (;;)
                    {
                        if (f.Position + 8 > f.Length) break;
                        uint id = r.ReadUInt32();
                        uint size = r.ReadUInt32();
                        if (id == 0x20746D66) // "fmt "
                        {
                            short tag = r.ReadInt16();
                            channels = r.ReadInt16();
                            sampleRate = r.ReadInt32();
                            r.ReadInt32(); // avg bytes per sec
                            r.ReadInt16(); // block align
                            bits = r.ReadInt16();
                            if (size > 16 && size < 4096) f.Seek(size - 16, SeekOrigin.Current);
                        }
                        else if (id == 0x61746164) // "data"
                        {
                            dataBytes = size;
                            dataOffset = f.Position;
                            if (dataBytes == 0 || dataOffset + dataBytes > f.Length)
                            {
                                dataBytes = f.Length - dataOffset;
                            }
                            break;
                        }
                        else
                        {
                            if (size > 0 && size < 1 << 20) f.Seek(size, SeekOrigin.Current);
                        }
                    }

                    if (dataOffset <= 0) return false;
                    _fmt = new WAVEFORMATEX();
                    _fmt.wFormatTag = 1;
                    _fmt.nChannels = (short)channels;
                    _fmt.nSamplesPerSec = sampleRate;
                    _fmt.wBitsPerSample = (short)bits;
                    _fmt.nBlockAlign = (short)(channels * bits / 8);
                    _fmt.nAvgBytesPerSec = sampleRate * _fmt.nBlockAlign;
                    _fmt.cbSize = 0;
                    _dataBytes = dataBytes;
                    _dataOffset = (int)dataOffset;
                    LengthMs = (int)(dataBytes * 1000L / Math.Max(1, _fmt.nAvgBytesPerSec));
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                StopInternal();
            }
        }
    }
}

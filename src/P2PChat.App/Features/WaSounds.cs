using System;
using System.IO;

namespace P2PChat.App.Features
{
    /// <summary>
    /// Sons de notification générés par synthèse (aucun fichier externe) :
    /// « ding » deux tons pour un message entrant, « pop » discret à l'envoi,
    /// arpège quand un pair apparaît. Cache disque + SoundPlayer.
    /// </summary>
    public static class WaSounds
    {
        private static System.Media.SoundPlayer _incoming;
        private static System.Media.SoundPlayer _sent;
        private static System.Media.SoundPlayer _peer;
        private static readonly object Gate = new object();

        private static string CacheDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "P2PChat", "sounds");
            }
        }

        /// <summary>Message entrant : ding deux tons (987 Hz → 659 Hz).</summary>
        public static void PlayIncoming()
        {
            if (_incoming == null) _incoming = Build("incoming", BuildDing);
            try { _incoming.Play(); } catch (Exception) { }
        }

        /// <summary>Message envoyé : pop très bref.</summary>
        public static void PlaySent()
        {
            if (_sent == null) _sent = Build("sent", BuildPop);
            try { _sent.Play(); } catch (Exception) { }
        }

        /// <summary>Pair apparu : arpège discret 3 notes.</summary>
        public static void PlayPeer()
        {
            if (_peer == null) _peer = Build("peer", BuildArpeggio);
            try { _peer.Play(); } catch (Exception) { }
        }

        private static System.Media.SoundPlayer Build(string name, Func<byte[]> synth)
        {
            lock (Gate)
            {
                string dir = CacheDir;
                string file = Path.Combine(dir, name + ".wav");
                try
                {
                    if (!File.Exists(file) || new FileInfo(file).Length < 100)
                    {
                        Directory.CreateDirectory(dir);
                        File.WriteAllBytes(file, synth());
                    }
                }
                catch (Exception)
                {
                    file = Path.Combine(Path.GetTempPath(), "p2pchat_" + name + ".wav");
                    try { File.WriteAllBytes(file, synth()); } catch (Exception) { }
                }
                System.Media.SoundPlayer p = new System.Media.SoundPlayer();
                try
                {
                    p.SoundLocation = file;
                    p.Load();
                }
                catch (Exception)
                {
                    try { p.Stream = new MemoryStream(synth()); } catch (Exception) { }
                }
                return p;
            }
        }

        // ------------------------------------------------------------ synthèse
        private static byte[] Wav(byte[] pcm16, int rate)
        {
            byte[] wav = new byte[44 + pcm16.Length];
            wav[0] = (byte)'R'; wav[1] = (byte)'I'; wav[2] = (byte)'F'; wav[3] = (byte)'F';
            WriteInt(wav, 4, 36 + pcm16.Length);
            wav[8] = (byte)'W'; wav[9] = (byte)'A'; wav[10] = (byte)'V'; wav[11] = (byte)'E';
            wav[12] = (byte)'f'; wav[13] = (byte)'m'; wav[14] = (byte)'t'; wav[15] = (byte)' ';
            WriteInt(wav, 16, 16);
            wav[20] = 1; wav[22] = 1;
            WriteInt(wav, 24, rate);
            WriteInt(wav, 28, rate * 2);
            wav[32] = 2; wav[34] = 16;
            wav[36] = (byte)'d'; wav[37] = (byte)'a'; wav[38] = (byte)'t'; wav[39] = (byte)'a';
            WriteInt(wav, 40, pcm16.Length);
            Buffer.BlockCopy(pcm16, 0, wav, 44, pcm16.Length);
            return wav;
        }

        private static void WriteInt(byte[] b, int at, int v)
        {
            b[at] = (byte)v; b[at + 1] = (byte)(v >> 8);
            b[at + 2] = (byte)(v >> 16); b[at + 3] = (byte)(v >> 24);
        }

        /// <summary>Génère un signal à segments [freq, ms, volume].</summary>
        private static byte[] Segments(int rate, double[][] segs)
        {
            int total = 0;
            foreach (double[] s in segs) total += (int)(s[1] * rate / 1000.0);
            byte[] pcm = new byte[total * 2];
            int pos = 0;
            double phase = 0;
            foreach (double[] s in segs)
            {
                int n = (int)(s[1] * rate / 1000.0);
                double freq = s[0], vol = s[2];
                for (int i = 0; i < n; i++)
                {
                    double t = (double)i / n;
                    double env = Math.Sin(Math.PI * t);              // fondu entrée/sortie
                    double smooth = i < 80 ? (double)i / 80 : 1.0;  // anti-clic
                    double v = Math.Sin(phase) * vol * env * smooth;
                    short sv = (short)(v * short.MaxValue);
                    pcm[(pos + i) * 2] = (byte)(sv & 0xFF);
                    pcm[(pos + i) * 2 + 1] = (byte)((sv >> 8) & 0xFF);
                    phase += 2 * Math.PI * freq / rate;
                }
                pos += n;
            }
            return Wav(pcm, rate);
        }

        private static byte[] BuildDing()
        {
            return Segments(44100, new double[][]
            {
                new double[] { 987, 130, 0.30 },
                new double[] { 659, 210, 0.26 },
            });
        }

        private static byte[] BuildPop()
        {
            return Segments(44100, new double[][]
            {
                new double[] { 740, 55, 0.18 },
                new double[] { 988, 45, 0.14 },
            });
        }

        private static byte[] BuildArpeggio()
        {
            return Segments(44100, new double[][]
            {
                new double[] { 523, 90, 0.16 },
                new double[] { 659, 90, 0.16 },
                new double[] { 784, 140, 0.16 },
            });
        }
    }
}

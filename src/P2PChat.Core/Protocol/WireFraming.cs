using System;
using System.IO;

namespace P2PChat.Core.Protocol
{
    /// <summary>
    /// Cadrage TCP : [longueur 4 octets little-endian][données].
    /// Utilisé par toutes les trames chiffrées.
    /// </summary>
    public static class WireFraming
    {
        /// <summary>Taille maximale acceptée d'une trame (8 Mo) - garde-fou sécurité.</summary>
        public const int MaxFrameBytes = 8 * 1024 * 1024;

        /// <summary>Taille du préfixe de longueur.</summary>
        public const int PrefixBytes = 4;

        /// <summary>Écrit la longueur en little-endian dans buf[0..3].</summary>
        public static void WritePrefix(byte[] buf, int len)
        {
            buf[0] = (byte)(len & 255);
            buf[1] = (byte)((len >> 8) & 255);
            buf[2] = (byte)((len >> 16) & 255);
            buf[3] = (byte)((len >> 24) & 255);
        }

        /// <summary>Lit la longueur little-endian.</summary>
        public static int ReadPrefix(byte[] buf, int offset)
        {
            return (buf[offset] & 255)
                | ((buf[offset + 1] & 255) << 8)
                | ((buf[offset + 2] & 255) << 16)
                | ((buf[offset + 3] & 255) << 24);
        }

        /// <summary>Lève InvalidDataException si la longueur annoncée est aberrante (anti-DoS).</summary>
        public static void ValidateLength(int len)
        {
            if (len < 1 || len > MaxFrameBytes)
            {
                throw new InvalidDataException("Taille de trame invalide : " + len);
            }
        }
    }
}

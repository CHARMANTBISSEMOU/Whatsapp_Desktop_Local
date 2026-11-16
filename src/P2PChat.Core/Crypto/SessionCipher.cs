using System;
using System.Security.Cryptography;
using System.Text;

namespace P2PChat.Core.Crypto
{
    /// <summary>
    /// Chiffreur symétrique d'une session - implémentation dev-core.
    /// Schéma imposé (encrypt-then-MAC) :
    ///   trame = [ IV aléatoire 16 o ][ texte chiffré AES-256-CBC/PKCS7 ][ MAC HMAC-SHA256 32 o sur IV||chiffré ]
    ///
    /// Dérivation depuis le secret ECDH partagé (32 o) :
    ///   clé AES   = SHA-256( octets UTF-8 de "p2pchat-enc" || secret )
    ///   clé HMAC  = SHA-256( octets UTF-8 de "p2pchat-mac" || secret )
    ///
    /// Decrypt :
    ///   1. refuse les trames trop courtes ( < IV 16 + 1 bloc AES 16 + MAC 32 = 64 octets ) ;
    ///   2. vérifie le MAC avec ConstantTimeEquals AVANT tout déchiffrement
    ///      -> CryptographicException("MAC invalide") si falsifié ;
    ///   3. AES : IV = frame[0..15] ; padding PKCS7 cassé -> CryptographicException.
    ///
    /// Remarques : Encrypt accepte un clair de longueur nulle (le padding remplit
    /// alors un bloc complet) et Decrypt restitue un tableau vide. L'instance est
    /// thread-safe (verrou interne) ; Dispose efface les clés de la mémoire.
    /// </summary>
    public sealed class SessionCipher : IDisposable
    {
        /// <summary>IV (16) + HMAC (32) = octets ajoutés au clair.</summary>
        public const int OverheadBytes = 16 + 32;

        private const int IvBytes = 16;       // IV AES = 16 octets
        private const int AesBlockBytes = 16; // un bloc AES-CBC fait toujours 16 octets
        private const int MacBytes = 32;       // HMAC-SHA256 = 32 octets

        private byte[] _aesKey; // 32 octets (AES-256)
        private byte[] _macKey; // 32 octets (HMAC-SHA256)
        private readonly object _sync = new object();
        private readonly RandomNumberGenerator _rng = new RNGCryptoServiceProvider();
        private bool _disposed;

        private SessionCipher(byte[] aesKey, byte[] macKey)
        {
            _aesKey = aesKey;
            _macKey = macKey;
        }

        /// <summary>Construit un chiffreur depuis le secret partagé ECDH brut (32 octets).</summary>
        public static SessionCipher FromSharedSecret(byte[] sharedSecret)
        {
            if (sharedSecret == null)
            {
                throw new ArgumentNullException("sharedSecret");
            }
            if (sharedSecret.Length == 0)
            {
                throw new ArgumentException("Le secret partagé ECDH est vide.", "sharedSecret");
            }

            byte[] encMaterial = Concat(Encoding.UTF8.GetBytes("p2pchat-enc"), sharedSecret);
            byte[] macMaterial = Concat(Encoding.UTF8.GetBytes("p2pchat-mac"), sharedSecret);
            byte[] aesKey;
            byte[] macKey;
            try
            {
                aesKey = CryptoSuite.Sha256(encMaterial); // AES-256 = exactement 32 octets
                macKey = CryptoSuite.Sha256(macMaterial); // HMAC-SHA256 = 32 octets
            }
            finally
            {
                Array.Clear(encMaterial, 0, encMaterial.Length);
                Array.Clear(macMaterial, 0, macMaterial.Length);
            }
            return new SessionCipher(aesKey, macKey);
        }

        /// <summary>Chiffre un message. Retourne trame = IV+chiffré+MAC.</summary>
        public byte[] Encrypt(byte[] plaintext)
        {
            if (plaintext == null)
            {
                throw new ArgumentNullException("plaintext");
            }
            lock (_sync)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException("SessionCipher");
                }

                byte[] iv = new byte[IvBytes];
                byte[] cipher = null;
                try
                {
                    _rng.GetBytes(iv); // IV cryptographiquement aléatoire

                    using (AesManaged aes = NewAes(_aesKey, iv))
                    {
                        using (ICryptoTransform enc = aes.CreateEncryptor())
                        {
                            cipher = enc.TransformFinalBlock(plaintext, 0, plaintext.Length);
                        }
                    }

                    byte[] mac = ComputeMac(_macKey, iv, cipher);
                    byte[] frame = new byte[IvBytes + cipher.Length + MacBytes];
                    Buffer.BlockCopy(iv, 0, frame, 0, IvBytes);
                    Buffer.BlockCopy(cipher, 0, frame, IvBytes, cipher.Length);
                    Buffer.BlockCopy(mac, 0, frame, IvBytes + cipher.Length, MacBytes);
                    return frame;
                }
                finally
                {
                    if (cipher != null)
                    {
                        Array.Clear(cipher, 0, cipher.Length);
                    }
                    Array.Clear(iv, 0, iv.Length);
                }
            }
        }
        /// <summary>Déchiffre une trame. CryptographicException si falsifiée ou corrompue.</summary>
        public byte[] Decrypt(byte[] frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException("frame");
            }
            lock (_sync)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException("SessionCipher");
                }

                // 1) Trop courte : exit direct (IV + 1 bloc AES + MAC au minimum).
                if (frame.Length < IvBytes + AesBlockBytes + MacBytes)
                {
                    throw new CryptographicException("Trame trop courte (" + frame.Length
                        + " octets, minimum requis = " + (IvBytes + AesBlockBytes + MacBytes) + ").");
                }

                // 2) Le ciphertext doit être un multiple de la taille de bloc AES.
                int cipherLen = frame.Length - IvBytes - MacBytes;
                if (cipherLen < AesBlockBytes || (cipherLen % AesBlockBytes) != 0)
                {
                    throw new CryptographicException("Longueur de trame incohérente (ciphertext de " + cipherLen + " octets).");
                }

                // 3) MAC AVANT tout déchiffrement (encrypt-then-MAC), temps constant.
                int macOffset = frame.Length - MacBytes;
                using (HMACSHA256 mac = new HMACSHA256(_macKey))
                {
                    byte[] expected = mac.ComputeHash(frame, 0, macOffset); // MAC de IV||chiffré
                    byte[] received = new byte[MacBytes];
                    Buffer.BlockCopy(frame, macOffset, received, 0, MacBytes);
                    bool ok = CryptoSuite.ConstantTimeEquals(expected, received);
                    Array.Clear(expected, 0, expected.Length);
                    Array.Clear(received, 0, received.Length);
                    if (!ok)
                    {
                        throw new CryptographicException("MAC invalide (trame falsifiée ou session étrangère).");
                    }
                }

                // 4) Déchiffrement AES ; padding cassé -> CryptographicException.
                byte[] iv = new byte[IvBytes];
                Buffer.BlockCopy(frame, 0, iv, 0, IvBytes);
                byte[] plaintext = null;
                try
                {
                    using (AesManaged aes = NewAes(_aesKey, iv))
                    {
                        using (ICryptoTransform dec = aes.CreateDecryptor())
                        {
                            plaintext = dec.TransformFinalBlock(frame, IvBytes, cipherLen);
                        }
                    }
                    return plaintext;
                }
                catch (CryptographicException ex)
                {
                    if (plaintext != null)
                    {
                        Array.Clear(plaintext, 0, plaintext.Length);
                    }
                    throw new CryptographicException("Padding AES invalide (trame corrompue).", ex);
                }
                finally
                {
                    Array.Clear(iv, 0, iv.Length);
                }
            }
        }

        /// <summary>Élimine les clés de la mémoire dans la mesure du possible.</summary>
        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
                if (_aesKey != null)
                {
                    Array.Clear(_aesKey, 0, _aesKey.Length);
                    _aesKey = null;
                }
                if (_macKey != null)
                {
                    Array.Clear(_macKey, 0, _macKey.Length);
                    _macKey = null;
                }
                ((IDisposable)_rng).Dispose();
            }
        }

        // ---------------- Internes ----------------

        private static AesManaged NewAes(byte[] key, byte[] iv)
        {
            AesManaged aes = new AesManaged();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key;
            aes.IV = iv;
            return aes;
        }

        private static byte[] ComputeMac(byte[] macKey, byte[] iv, byte[] cipher)
        {
            // HMAC-SHA256 sur IV||chiffré via ComputeHash : les surcharges Transform* ne
            // garantissent pas la valeur de retour (HashAlgorithm ne produit aucun octet
            // en sortie) ; ComputeHash retourne toujours le digest (32 octets).
            byte[] buffer = new byte[iv.Length + cipher.Length];
            Buffer.BlockCopy(iv, 0, buffer, 0, iv.Length);
            Buffer.BlockCopy(cipher, 0, buffer, iv.Length, cipher.Length);
            using (HMACSHA256 mac = new HMACSHA256(macKey))
            {
                return mac.ComputeHash(buffer);
            }
        }

        private static byte[] Concat(byte[] prefix, byte[] suffix)
        {
            byte[] r = new byte[prefix.Length + suffix.Length];
            Buffer.BlockCopy(prefix, 0, r, 0, prefix.Length);
            Buffer.BlockCopy(suffix, 0, r, prefix.Length, suffix.Length);
            return r;
        }
    }
}

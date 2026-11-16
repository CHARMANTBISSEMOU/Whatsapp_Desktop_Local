using System;
using System.Security.Cryptography;

namespace P2PChat.Core.Crypto
{
    /// <summary>
    /// Paire de clés éphémères ECDH P-256 propre à une session TCP.
    /// Le handshake échange uniquement PublicKeyB ; la clé privée ne quitte jamais ce processus.
    /// </summary>
    public sealed class EcdhKeyPair
    {
        /// <summary>Clé publique au format CNG EccPublicBlob (à envoyer dans le handshake TCP).</summary>
        public byte[] PublicKeyB;

        /// <summary>Clé privée CNG - accès interne au noyau uniquement.</summary>
        internal ECDiffieHellmanCng Private;
    }

    /// <summary>
    /// Boîte à outils cryptographique de P2PChat - implémentation dev-core.
    ///  - CreateKeyPair() : ECDiffieHellmanCng 256 bits (P-256), KDF Hash+SHA-256,
    ///    PublicKeyB = cng.PublicKey.ToByteArray() (blob CNG EccPublicBlob, 72 octets) ;
    ///  - DeriveSharedSecret(mine, otherPub) : CngKey.Import(otherPub, EccPublicBlob)
    ///    puis DeriveKeyMaterial -> 32 octets ; CryptographicException si clé invalide ;
    ///  - Sha256 : SHA-256 brut (provider natif, compatible FIPS) ;
    ///  - ConstantTimeEquals : comparaison en temps constant SANS sortie anticipée ;
    ///  - ToBase64/FromBase64 : Convert.* classique (FormatException si Base64 corrompu).
    /// </summary>
    public static class CryptoSuite
    {
        /// <summary>Génère une paire de clés ECDH P-256 (256 bits, courbe NIST P-256).</summary>
        public static EcdhKeyPair CreateKeyPair()
        {
            ECDiffieHellmanCng cng = new ECDiffieHellmanCng(256);
            // KDF déterministe (Hash + SHA-256) pour que les deux pairs produisent
            // exactement le même secret partagé, de 32 octets, des deux côtés.
            cng.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash;
            cng.HashAlgorithm = CngAlgorithm.Sha256;

            EcdhKeyPair pair = new EcdhKeyPair();
            pair.Private = cng;
            pair.PublicKeyB = cng.PublicKey.ToByteArray(); // EccPublicBlob
            return pair;
        }
        /// <summary>
        /// Calcule le secret partagé ECDH (32 octets) entre MA clé privée et
        /// la clé publique distante fournie au format EccPublicBlob.
        /// Lève CryptographicException si la clé distante est invalide.
        /// </summary>
        public static byte[] DeriveSharedSecret(EcdhKeyPair mine, byte[] otherPublicKeyB)
        {
            if (mine == null || mine.Private == null)
            {
                throw new CryptographicException("Paire de clés locale absente ou invalide (EcdhKeyPair.Private).");
            }
            if (otherPublicKeyB == null || otherPublicKeyB.Length == 0)
            {
                throw new CryptographicException("Clé publique distante absente ou vide.");
            }

            byte[] secret;
            try
            {
                using (CngKey otherKey = CngKey.Import(otherPublicKeyB, CngKeyBlobFormat.EccPublicBlob))
                {
                    // Pattern .NET Framework documenté : DeriveKeyMaterial(CngKey) applique
                    // le KDF posé dans CreateKeyPair (Hash + SHA-256) -> 32 octets,
                    // identique des deux côtés de la connexion.
                    secret = mine.Private.DeriveKeyMaterial(otherKey);
                }
            }
            catch (Exception ex)
            {
                // Blob illisible, mauvais format, courbe non supportée ... -> contrat du stub.
                throw new CryptographicException("Impossible de dériver le secret ECDH (clé publique distante invalide ?).", ex);
            }

            if (secret == null || secret.Length != 32)
            {
                throw new CryptographicException("Le secret ECDH n'a pas la taille attendue (32 octets) - courbe distante incohérente ?");
            }
            return secret;
        }

        /// <summary>SHA-256 brut (32 octets).</summary>
        public static byte[] Sha256(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }
            using (SHA256CryptoServiceProvider sha = new SHA256CryptoServiceProvider())
            {
                return sha.ComputeHash(data);
            }
        }

        /// <summary>
        /// Comparaison en temps constant - true si identiques.
        /// AUCUNE sortie anticipée : le XOR des octets est cumulé sur la longueur
        /// maximale des deux tableaux, y compris quand les longueurs diffèrent.
        /// </summary>
        public static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            int lenA = (a == null) ? 0 : a.Length;
            int lenB = (b == null) ? 0 : b.Length;
            int diff = lenA ^ lenB;             // longueurs différentes => diff != 0 d'office
            int max = (lenA >= lenB) ? lenA : lenB;
            for (int i = 0; i < max; i++)
            {
                int x = (i < lenA) ? a[i] : 0;
                int y = (i < lenB) ? b[i] : 0;
                diff |= x ^ y;                   // cumul, jamais de retour prématuré
            }
            return diff == 0;
        }

        /// <summary>Octets -> Base64.</summary>
        public static string ToBase64(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }
            return Convert.ToBase64String(data);
        }

        /// <summary>Base64 -> octets ; lève FormatException si invalide.</summary>
        public static byte[] FromBase64(string s)
        {
            if (s == null)
            {
                throw new ArgumentNullException("s");
            }
            try
            {
                return Convert.FromBase64String(s);
            }
            catch (Exception ex)
            {
                // Normalise toute erreur de décodage en FormatException (contrat du stub).
                throw new FormatException("Chaîne Base64 invalide.", ex);
            }
        }
    }
}

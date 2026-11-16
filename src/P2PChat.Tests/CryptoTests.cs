using System;
using System.Security.Cryptography;
using System.Text;
using P2PChat.Core.Crypto;

namespace P2PChat.Tests
{
    /// <summary>
    /// Tests du noyau cryptographique P2PChat (dev-core) :
    /// SHA-256, Base64, comparaison en temps constant, ECDH P-256 et
    /// SessionCipher (AES-256-CBC/PKCS7 + HMAC-SHA256, encrypt-then-MAC).
    /// Chaque test rend son verdict via check(nom, reussi).
    /// Aucune dépendance externe : System / System.Core uniquement.
    /// </summary>
    public static class CryptoTests
    {
        /// <summary>Exécute tous les crypto-tests ; check(nom, ok) affiche et agrège chaque verdict.</summary>
        public static void Run(System.Action<string, bool> check)
        {
            if (check == null)
            {
                throw new ArgumentNullException("check");
            }
            TestHashAndBase64(check);
            TestConstantTimeEquals(check);
            TestEcdh(check);
            TestSessionCipher(check);
        }

        private static void TestHashAndBase64(Action<string, bool> check)
        {
            check("Crypto : SHA-256 vecteur 'abc'",
                ToHex(CryptoSuite.Sha256(Encoding.UTF8.GetBytes("abc")))
                    == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
            check("Crypto : SHA-256 entree vide",
                ToHex(CryptoSuite.Sha256(new byte[0]))
                    == "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");

            byte[] raw = new byte[] { 0x00, 0x01, 0x2A, 0x7F, 0x80, 0xFF };
            string b64 = CryptoSuite.ToBase64(raw);
            check("Crypto : Base64 conforme a Convert", b64 == Convert.ToBase64String(raw));
            check("Crypto : Base64 aller-retour",
                CryptoSuite.ConstantTimeEquals(CryptoSuite.FromBase64(b64), raw));
            check("Crypto : Base64 invalide rejete",
                Throws<FormatException>(() => CryptoSuite.FromBase64("!!! pas du base64 ~~~")));
            check("Crypto : Base64 null rejete",
                Throws<ArgumentNullException>(() => CryptoSuite.FromBase64(null)));
        }

        private static void TestConstantTimeEquals(Action<string, bool> check)
        {
            check("Crypto : ConstantTimeEquals egaux",
                CryptoSuite.ConstantTimeEquals(new byte[] { 1, 2, 3, 4 }, new byte[] { 1, 2, 3, 4 }));
            check("Crypto : ConstantTimeEquals differents",
                !CryptoSuite.ConstantTimeEquals(new byte[] { 1, 2, 3, 4 }, new byte[] { 1, 2, 3, 5 }));
            check("Crypto : ConstantTimeEquals longueurs differentes",
                !CryptoSuite.ConstantTimeEquals(new byte[] { 1, 2 }, new byte[] { 1, 2, 0 })
                && !CryptoSuite.ConstantTimeEquals(new byte[0], new byte[] { 0 }));
            check("Crypto : ConstantTimeEquals deux vides",
                CryptoSuite.ConstantTimeEquals(new byte[0], new byte[0]));
            byte[] h1 = CryptoSuite.Sha256(Encoding.UTF8.GetBytes("droit"));
            byte[] h1Bis = CryptoSuite.Sha256(Encoding.UTF8.GetBytes("droit"));
            byte[] h2 = CryptoSuite.Sha256(Encoding.UTF8.GetBytes("tord"));
            check("Crypto : ConstantTimeEquals 32 octets",
                CryptoSuite.ConstantTimeEquals(h1, h1Bis) && !CryptoSuite.ConstantTimeEquals(h1, h2));
        }

        private static void TestEcdh(Action<string, bool> check)
        {
            EcdhKeyPair alice = CryptoSuite.CreateKeyPair();
            EcdhKeyPair bob = CryptoSuite.CreateKeyPair();

            check("Crypto : ECDH cles publiques P-256 (EccPublicBlob 72 o)",
                alice.PublicKeyB != null && alice.PublicKeyB.Length == 72
                && bob.PublicKeyB != null && bob.PublicKeyB.Length == 72);
            check("Crypto : ECDH cles publiques distinctes",
                !CryptoSuite.ConstantTimeEquals(alice.PublicKeyB, bob.PublicKeyB));

            byte[] secretAlice = CryptoSuite.DeriveSharedSecret(alice, bob.PublicKeyB);
            byte[] secretBob = CryptoSuite.DeriveSharedSecret(bob, alice.PublicKeyB);

            check("Crypto : ECDH secret de 32 octets des deux cotes",
                secretAlice.Length == 32 && secretBob.Length == 32);
            check("Crypto : ECDH meme secret des deux cotes",
                CryptoSuite.ConstantTimeEquals(secretAlice, secretBob));
            check("Crypto : ECDH secret non trivial", !AllZero(secretAlice));

            EcdhKeyPair third = CryptoSuite.CreateKeyPair();
            byte[] secretThird = CryptoSuite.DeriveSharedSecret(alice, third.PublicKeyB);
            check("Crypto : ECDH une autre paire donne un autre secret",
                !CryptoSuite.ConstantTimeEquals(secretThird, secretAlice));

            byte[] bobPubFromB64 = CryptoSuite.FromBase64(CryptoSuite.ToBase64(bob.PublicKeyB));
            byte[] secretViaB64 = CryptoSuite.DeriveSharedSecret(alice, bobPubFromB64);
            check("Crypto : ECDH meme secret via cle publique Base64",
                CryptoSuite.ConstantTimeEquals(secretViaB64, secretAlice));

            check("Crypto : ECDH blob distant invalide rejete",
                Throws<CryptographicException>(() => CryptoSuite.DeriveSharedSecret(alice, new byte[71])));
            check("Crypto : ECDH blob distant vide rejete",
                Throws<CryptographicException>(() => CryptoSuite.DeriveSharedSecret(alice, new byte[0])));
            check("Crypto : ECDH paire locale absente rejete",
                Throws<CryptographicException>(() => CryptoSuite.DeriveSharedSecret(null, bob.PublicKeyB)));
        }

        private static void TestSessionCipher(Action<string, bool> check)
        {
            byte[] secret = CryptoSuite.Sha256(Encoding.UTF8.GetBytes("secret de session pour les tests"));

            // Cles derivees identiques : secret calcule cote A == secret calcule cote B.
            EcdhKeyPair pairA = CryptoSuite.CreateKeyPair();
            EcdhKeyPair pairB = CryptoSuite.CreateKeyPair();
            byte[] secretAtA = CryptoSuite.DeriveSharedSecret(pairA, pairB.PublicKeyB);
            byte[] secretAtB = CryptoSuite.DeriveSharedSecret(pairB, pairA.PublicKeyB);
            check("Crypto : Cipher ECDH secret cote A == cote B",
                CryptoSuite.ConstantTimeEquals(secretAtA, secretAtB));

            using (SessionCipher cipherA = SessionCipher.FromSharedSecret(secretAtA))
            using (SessionCipher cipherB = SessionCipher.FromSharedSecret(secretAtB))
            {
                byte[] msgA = Encoding.UTF8.GetBytes("message chiffre par A pour B");
                byte[] backAtB = cipherB.Decrypt(cipherA.Encrypt(msgA));
                check("Crypto : Cipher croise A chiffre / B dechiffre",
                    CryptoSuite.ConstantTimeEquals(msgA, backAtB));

                byte[] msgB = Encoding.UTF8.GetBytes("reponse chiffree par B pour A");
                byte[] backAtA = cipherA.Decrypt(cipherB.Encrypt(msgB));
                check("Crypto : Cipher croise B chiffre / A dechiffre",
                    CryptoSuite.ConstantTimeEquals(msgB, backAtA));
            }

            using (SessionCipher c = SessionCipher.FromSharedSecret(secret))
            {
                // 1) Roundtrip pour toute une gamme de tailles (bornes de bloc incluses).
                Random rnd = new Random(0x5EED);
                int[] sizes = new int[] { 0, 1, 15, 16, 17, 1000, 100000 };
                for (int i = 0; i < sizes.Length; i++)
                {
                    int len = sizes[i];
                    byte[] plain = new byte[len];
                    if (len > 0)
                    {
                        rnd.NextBytes(plain);
                    }
                    byte[] frame = c.Encrypt(plain);
                    byte[] back = c.Decrypt(frame);
                    int frameLen = 16 + ((len / 16) + 1) * 16 + 32; // IV + padding AES + MAC
                    check("Crypto : Cipher roundtrip " + len + " octets",
                        frame.Length == frameLen && back.Length == len
                        && CryptoSuite.ConstantTimeEquals(plain, back));
                }

                // 2) IV aleatoire : deux trames d'un meme clair doivent differer.
                byte[] twice = Encoding.UTF8.GetBytes("deux fois le meme clair");
                byte[] f1 = c.Encrypt(twice);
                byte[] f2 = c.Encrypt(twice);
                check("Crypto : Cipher IV aleatoire (trames distinctes)",
                    f1.Length == f2.Length && !CryptoSuite.ConstantTimeEquals(f1, f2));

                // 3) Arguments invalides.
                check("Crypto : Cipher clair null rejete",
                    Throws<ArgumentNullException>(() => c.Encrypt(null)));
                check("Crypto : Cipher trame null rejetee",
                    Throws<ArgumentNullException>(() => c.Decrypt(null)));
                check("Crypto : Cipher trame vide rejetee",
                    Throws<CryptographicException>(() => c.Decrypt(new byte[0])));
                check("Crypto : Cipher secret nul rejete",
                    Throws<ArgumentNullException>(() => SessionCipher.FromSharedSecret(null)));
                check("Crypto : Cipher secret vide rejete",
                    Throws<ArgumentException>(() => SessionCipher.FromSharedSecret(new byte[0])));

                // 4) Trame falsifiee : un seul bit change -> rejet MAC.
                byte[] tamperedCipher = (byte[])f1.Clone();
                tamperedCipher[21] ^= 0x01;
                check("Crypto : Cipher trame falsifiee (1 bit) rejetee",
                    Throws<CryptographicException>(() => c.Decrypt(tamperedCipher)));

                byte[] tamperedIv = (byte[])f1.Clone();
                tamperedIv[0] ^= 0x80;
                check("Crypto : Cipher IV modifie rejete",
                    Throws<CryptographicException>(() => c.Decrypt(tamperedIv)));

                byte[] tamperedMac = (byte[])f1.Clone();
                tamperedMac[tamperedMac.Length - 1] ^= 0x01;
                check("Crypto : Cipher MAC modifie rejete",
                    Throws<CryptographicException>(() => c.Decrypt(tamperedMac)));


                // 5) Trames tronquees ou absurdes.
                byte[] truncatedOne = new byte[f1.Length - 1];
                Array.Copy(f1, truncatedOne, truncatedOne.Length);
                check("Crypto : Cipher trame tronquee (1 o) rejetee",
                    Throws<CryptographicException>(() => c.Decrypt(truncatedOne)));

                byte[] truncatedHalf = new byte[f1.Length / 2];
                Array.Copy(f1, truncatedHalf, truncatedHalf.Length);
                check("Crypto : Cipher trame tronquee de moitie rejetee",
                    Throws<CryptographicException>(() => c.Decrypt(truncatedHalf)));

                check("Crypto : Cipher trame de 10 octets rejetee",
                    Throws<CryptographicException>(() => c.Decrypt(new byte[10])));

                byte[] garbage = new byte[64];
                rnd.NextBytes(garbage);
                check("Crypto : Cipher ordure de 64 octets rejetee",
                    Throws<CryptographicException>(() => c.Decrypt(garbage)));

                byte[] garbageBig = new byte[16 + 16 * 5 + 32];
                rnd.NextBytes(garbageBig);
                check("Crypto : Cipher ordure de 128 octets rejetee",
                    Throws<CryptographicException>(() => c.Decrypt(garbageBig)));

                // 6) MAC d'une autre session (secret different) : rejet dans les deux sens.
                using (SessionCipher impostor = SessionCipher.FromSharedSecret(
                    CryptoSuite.Sha256(Encoding.UTF8.GetBytes("secret d'une autre session"))))
                {
                    check("Crypto : Cipher MAC d'une autre session rejete",
                        Throws<CryptographicException>(() => impostor.Decrypt(f1)));
                    byte[] impostorFrame = impostor.Encrypt(Encoding.UTF8.GetBytes("spoof"));
                    check("Crypto : Cipher trame d'une session etrangere rejetee",
                        Throws<CryptographicException>(() => c.Decrypt(impostorFrame)));
                }
            }

            // 7) Cycle de vie : Dispose efface les cles et interdit tout nouvel usage.
            SessionCipher disposable = SessionCipher.FromSharedSecret(
                CryptoSuite.Sha256(Encoding.UTF8.GetBytes("cle a detruire")));
            disposable.Dispose();
            check("Crypto : Cipher chiffrement refuse apres Dispose",
                Throws<ObjectDisposedException>(() => disposable.Encrypt(new byte[] { 1 })));
            check("Crypto : Cipher dechiffrement refuse apres Dispose",
                Throws<ObjectDisposedException>(() => disposable.Decrypt(new byte[] { 1 })));
            bool doubleDisposeSafe;
            try
            {
                disposable.Dispose();
                doubleDisposeSafe = true;
            }
            catch (Exception)
            {
                doubleDisposeSafe = false;
            }
            check("Crypto : Cipher double Dispose sans erreur", doubleDisposeSafe);
        }

        // ---------------- Aides ----------------

        private static bool Throws<TException>(Action action) where TException : Exception
        {
            try
            {
                action();
                return false;
            }
            catch (TException)
            {
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool AllZero(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] != 0)
                {
                    return false;
                }
            }
            return true;
        }

        private static string ToHex(byte[] data)
        {
            StringBuilder sb = new StringBuilder(data.Length * 2);
            for (int i = 0; i < data.Length; i++)
            {
                sb.Append(data[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }
}

// Cypher (c) by Tangram Inc
// 
// Cypher is licensed under a
// Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International License.
// 
// You should have received a copy of the license along with this
// work. If not, see <http://creativecommons.org/licenses/by-nc-nd/4.0/>.

using System;
using System.Security;
using System.Text;
using Sodium;

namespace TangramCypher.Helper.LibSodium
{
    public static class Cryptography
    {
        /// <summary>
        /// Seal box.
        /// </summary>
        /// <returns>The seal.</returns>
        /// <param name="message">Message.</param>
        /// <param name="pk">Pk.</param>
        public static byte[] BoxSeal(string message, byte[] pk)
        {
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("message", nameof(message));

            if (pk == null)
                throw new ArgumentNullException(nameof(pk));

            var encrypted = SealedPublicKeyBox.Create(Encoding.UTF8.GetBytes(message), pk);
            return encrypted;
        }

        /// <summary>
        /// Creates a generics hash.
        /// </summary>
        /// <returns>The hash no key.</returns>
        /// <param name="message">Message.</param>
        /// <param name="bytes">Bytes.</param>
        public static byte[] GenericHashNoKey(string message, int bytes = 32)
        {
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Message cannot be null or empty!", nameof(message));

            return GenericHash.Hash(Encoding.UTF8.GetBytes(message), null, bytes);
        }

        public static byte[] GenericHashNoKey(byte[] message, int bytes = 32)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            return GenericHash.Hash(message, null, bytes);
        }

        /// <summary>
        /// Creates generics hash with key.
        /// </summary>
        /// <returns>The hash with key.</returns>
        /// <param name="message">Message.</param>
        /// <param name="key">Key.</param>
        /// <param name="bytes">Bytes.</param>
        public static byte[] GenericHashWithKey(string message, byte[] key, int bytes = 32)
        {
            if (string.IsNullOrEmpty(message))
                throw new ArgumentException("Message cannot be null or empty!", nameof(message));

            return GenericHash.Hash(Encoding.UTF8.GetBytes(message), key, bytes);
        }

        /// <summary>
        /// Argons hash password.
        /// </summary>
        /// <returns>The hash password.</returns>
        /// <param name="password">Password.</param>
        public static byte[] ArgonHashPassword(SecureString password)
        {
            if (password == null)
                throw new ArgumentNullException(nameof(password));

            const long OPS_LIMIT = 4;
            const int MEM_LIMIT = 33554432;
            string hash;

            using (var insecurePassword = password.Insecure())
            {
                hash = PasswordHash.ArgonHashString(insecurePassword.Value, OPS_LIMIT, MEM_LIMIT);
            }

            return Encoding.UTF8.GetBytes(hash);
        }

        /// <summary>
        /// Create public static key box.
        /// </summary>
        /// <returns>The pair.</returns>
        public static KeyPairDto KeyPair()
        {
            var kp = PublicKeyBox.GenerateKeyPair();
            return new KeyPairDto() { PublicKey = kp.PublicKey, SecretKey = kp.PrivateKey };
        }

        public static string OpenBoxSeal(byte[] cipher, KeyPair keyPair)
        {
            if (cipher == null)
                throw new ArgumentNullException(nameof(cipher));

            if (keyPair == null)
                throw new ArgumentNullException(nameof(keyPair));

            var decrypted = SealedPublicKeyBox.Open(cipher, keyPair);
            return Encoding.UTF8.GetString(decrypted);
        }

        /// <summary>
        /// Random bytes.
        /// </summary>
        /// <returns>The bytes.</returns>
        /// <param name="bytes">Bytes.</param>
        public static byte[] RandomBytes(int bytes = 32)
        {
            return SodiumCore.GetRandomBytes(bytes);
        }

        /// <summary>
        /// Random the key.
        /// </summary>
        /// <returns>The key.</returns>
        public static byte[] RandomKey()
        {
            return GenericHash.GenerateKey();
        }

        /// <summary>
        /// Random number.
        /// </summary>
        /// <returns>The numbers.</returns>
        /// <param name="n">N.</param>
        public static int RandomNumbers(int n)
        {
            return SodiumCore.GetRandomNumber(n);
        }

        /// <summary>
        /// Scalars the base.
        /// </summary>
        /// <returns>The base.</returns>
        /// <param name="sk">Sk.</param>
        public static byte[] ScalarBase(byte[] sk)
        {
            if (sk == null)
                throw new ArgumentNullException(nameof(sk));

            return Sodium.ScalarMult.Base(sk);
        }

        /// <summary>
        /// Scalars the mult.
        /// </summary>
        /// <returns>The mult.</returns>
        /// <param name="sk">Sk.</param>
        /// <param name="pk">Pk.</param>
        public static byte[] ScalarMult(byte[] sk, byte[] pk)
        {
            if (sk == null)
                throw new ArgumentNullException(nameof(sk));

            if (pk == null)
                throw new ArgumentNullException(nameof(pk));

            return Sodium.ScalarMult.Mult(sk, pk);
        }

        /// <summary>
        /// Short hash.
        /// </summary>
        /// <returns>The hash.</returns>
        /// <param name="message">Message.</param>
        /// <param name="key">Key.</param>
        public static byte[] ShortHash(string message, byte[] key)
        {
            return Sodium.ShortHash.Hash(message, key);
        }

        /// <summary>
        /// Verify Argon Hash password.
        /// </summary>
        /// <returns><c>true</c>, if pwd was verifiyed, <c>false</c> otherwise.</returns>
        /// <param name="hash">Hash.</param>
        /// <param name="pwd">Pwd.</param>
        public static bool VerifiyPwd(byte[] hash, byte[] pwd)
        {
            if (hash == null)
                throw new ArgumentNullException(nameof(hash));

            if (pwd == null)
                throw new ArgumentNullException(nameof(pwd));

            return PasswordHash.ArgonHashStringVerify(hash, pwd);
        }

        /// <summary>
        /// Adds padding data to a buffer buf whose original size 
        /// is unpadded_buflen in order to extend its total length 
        /// to a multiple of blocksize.
        /// </summary>
        /// <returns>The pad.</returns>
        /// <param name="text">Text.</param>
        public static byte[] Pad(string text)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException("message", nameof(text));

            var buf = Encoding.UTF8.GetBytes(text);             var bufOrigialSize = buf.Length;             ulong paddedBufLenp = 0;              Array.Resize(ref buf, 540);              SodiumPadding.Pad(ref paddedBufLenp, buf, (ulong)buf.Length, 32, int.MaxValue);              Array.Resize(ref buf, (int)paddedBufLenp);              paddedBufLenp = 0;              for (int i = bufOrigialSize; i < buf.Length; i++)             {                 SodiumPadding.Pad(ref paddedBufLenp, buf, (ulong)i, 32, int.MaxValue);             }              return buf;
        }

        /// <summary>
        /// Computes the original, unpadded length of a message previously padded
        /// using sodium_pad(). The original length is put into unpadded_buflen_p
        /// </summary>
        /// <returns>The pad.</returns>
        /// <param name="data">Data.</param>
        public static byte[] Unpad(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var text = Encoding.UTF8.GetString(data);
            var json = text.Substring(0, text.LastIndexOf("}", StringComparison.CurrentCulture) + 1);

            return Encoding.UTF8.GetBytes(json);

            //ulong unpaddedBuflenp = 0;
            //int dataSize = 544;

            //Array.Resize(ref data, dataSize);
             //for (int i = dataSize; i >= 0; i--)             //{             //    var u = SodiumPadding.Unpad(ref unpaddedBuflenp, data, (ulong)i, 32);             //    if (u.Equals(0))             //        Array.Resize(ref data, data.Length - 1);             //}              //return data;
        }

    }
}
using System.Security.Cryptography;
using System.Text;

namespace NetProxy.Service.Routing
{
    public class Keyset
    {
        private readonly byte[] _bytes;
        private readonly byte[] _iv;
        private readonly SymmetricAlgorithm _algorithm;

        public ICryptoTransform GetEncryptor()
        {
            return _algorithm.CreateEncryptor(_bytes, _iv);
        }
        public ICryptoTransform CreateDecryptor()
        {
            return _algorithm.CreateDecryptor(_bytes, _iv);
        }

        public Keyset(string textKey, string salt)
        {
            using (Rfc2898DeriveBytes k2 = new Rfc2898DeriveBytes(textKey, Encoding.Unicode.GetBytes(salt)))
            {
                this._bytes = k2.GetBytes(32);
                this._iv = k2.GetBytes(16);
                this._algorithm = Aes.Create();
            }
        }
    }
}

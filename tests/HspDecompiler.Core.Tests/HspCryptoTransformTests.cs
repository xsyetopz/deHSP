using System;
using Xunit;
using HspDecompiler.Core.DpmToAx.Crypto;

namespace HspDecompiler.Core.Tests
{
    public class HspCryptoTransformTests
    {
        [Fact]
        public void Test_Encryption_Decryption_Roundtrip()
        {
            var transform = new HspCryptoTransform();
            transform.XorAdd = new XorAddTransform
            {
                XorByte = 0xAB,
                AddByte = 0x34,
                XorSum = false
            };

            byte[] plaintext = new byte[] { 0x48, 0x53, 0x50, 0x33, 0x00, 0xFF, 0x80 };
            byte[] encrypted = transform.Encryption(plaintext);
            byte[] decrypted = transform.Decryption(encrypted);

            Assert.Equal(plaintext, decrypted);
        }

        [Fact]
        public void Test_Encryption_Produces_Different_Output()
        {
            var transform = new HspCryptoTransform();
            transform.XorAdd = new XorAddTransform
            {
                XorByte = 0xAB,
                AddByte = 0x34,
                XorSum = false
            };

            byte[] plaintext = new byte[] { 0x48, 0x53, 0x50, 0x33 };
            byte[] encrypted = transform.Encryption(plaintext);

            bool allSame = true;
            for (int i = 0; i < plaintext.Length; i++)
                if (plaintext[i] != encrypted[i]) { allSame = false; break; }
            Assert.False(allSame);
        }

        [Fact]
        public void Test_CrackEncryption_Finds_Key_For_HSP3_Header()
        {
            var originalTransform = new HspCryptoTransform();
            originalTransform.XorAdd = new XorAddTransform
            {
                XorByte = 0x42,
                AddByte = 0x17,
                XorSum = false
            };

            byte[] plaintext = new byte[] { 0x48, 0x53, 0x50, 0x33, 0x01, 0x02, 0x03, 0x04 };
            byte[] encrypted = originalTransform.Encryption(plaintext);

            Func<byte[], bool> validator = data =>
                data.Length >= 4 && data[0] == 0x48 && data[1] == 0x53 && data[2] == 0x50
                && (data[3] == 0x33 || data[3] == 0x32);

            var cracked = HspCryptoTransform.CrackEncryption(encrypted, validator);

            Assert.NotNull(cracked);
            byte[] decrypted = cracked.Decryption(encrypted);
            Assert.Equal(plaintext, decrypted);
        }
    }
}

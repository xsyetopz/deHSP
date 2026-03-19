using HspDecompiler.Core.DpmToAx.Crypto;
using Xunit;

namespace HspDecompiler.Core.Tests
{
    public class XorAddTransformTests
    {
        [Fact]
        public void EncodeDecodeRoundtripReturnsOriginal()
        {
            var transform = new XorAddTransform
            {
                XorByte = 0xAB,
                AddByte = 0x34,
                XorSum = false
            };

            byte original = 0x48;
            byte encoded = transform.Encode(original);
            byte decoded = transform.Decode(encoded);
            Assert.Equal(original, decoded);
        }

        [Fact]
        public void EncodeDecodeRoundtripXorSumReturnsOriginal()
        {
            var transform = new XorAddTransform
            {
                XorByte = 0xCD,
                AddByte = 0x12,
                XorSum = true
            };

            byte original = 0x53;
            byte encoded = transform.Encode(original);
            byte decoded = transform.Decode(encoded);
            Assert.Equal(original, decoded);
        }

        [Fact]
        public void EncodeDecodeAllByteValuesRoundtrip()
        {
            var transform = new XorAddTransform
            {
                XorByte = 0xFF,
                AddByte = 0x7F,
                XorSum = false
            };

            for (int i = 0; i < 256; i++)
            {
                byte original = (byte)i;
                byte encoded = transform.Encode(original);
                byte decoded = transform.Decode(encoded);
                Assert.Equal(original, decoded);
            }
        }

        [Fact]
        public void SumWrapsAt256()
        {
            Assert.Equal((byte)0x00, XorAddTransform.Sum(0x80, 0x80));
            Assert.Equal((byte)0x01, XorAddTransform.Sum(0xFF, 0x02));
        }

        [Fact]
        public void DifWrapsBelowZero()
        {
            Assert.Equal((byte)0xFF, XorAddTransform.Dif(0x00, 0x01));
            Assert.Equal((byte)0x80, XorAddTransform.Dif(0x00, 0x80));
        }

        [Fact]
        public void XorIsSelfInverse()
        {
            byte a = 0x48, b = 0xAB;
            Assert.Equal(a, XorAddTransform.Xor(XorAddTransform.Xor(a, b), b));
        }

        [Fact]
        public void GetXorByteProducesCorrectEncode()
        {
            byte add = 0x34;
            byte plain = 0x48;
            byte encrypted = 0x99;
            bool xorSum = false;
            byte xorByte = XorAddTransform.GetXorByte(add, plain, encrypted, xorSum);
            var transform = new XorAddTransform { XorByte = xorByte, AddByte = add, XorSum = xorSum };
            Assert.Equal(encrypted, transform.Encode(plain));
        }
    }
}

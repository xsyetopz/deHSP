using System;
using HspDecompiler.Core.Resources;

namespace HspDecompiler.Core.DpmToAx.Crypto;

internal class HspCryptoTransform
{
    private const string Hsp2Magic = "HSP2";
    private const string Hsp3Magic = "HSP3";

    private XorAddTransform _xorAdd;

    internal XorAddTransform XorAdd
    {
        get => _xorAdd;
        set => _xorAdd = value;
    }

    public override string ToString() => _xorAdd.ToString();

    internal byte[] Encryption(byte[] plain)
    {
        byte[] encrypted = new byte[plain.Length];
        byte prevByte = 0;
        for (int i = 0; i < encrypted.Length; i++)
        {
            encrypted[i] = _xorAdd.Encode(XorAddTransform.Dif(plain[i], prevByte));
            prevByte = plain[i];
        }
        return encrypted;
    }

    internal byte[] Decryption(byte[] encrypted)
    {
        byte[] plain = new byte[encrypted.Length];
        byte prevByte = 0;
        for (int i = 0; i < encrypted.Length; i++)
        {
            byte plainByte = _xorAdd.Decode(encrypted[i]);
            plain[i] = XorAddTransform.Sum(plainByte, prevByte);
            prevByte = plain[i];
        }
        return plain;
    }

    internal static HspCryptoTransform? CrackEncryption(
        byte[] encrypted,
        Func<byte[], bool> validator
    ) =>
        CrackEncryption(System.Text.Encoding.ASCII.GetBytes(Hsp3Magic), encrypted, validator)
        ?? CrackEncryption(System.Text.Encoding.ASCII.GetBytes(Hsp2Magic), encrypted, validator);

    internal static HspCryptoTransform? CrackEncryption(
        byte[] plain,
        byte[] encrypted,
        Func<byte[], bool> validator
    )
    {
        byte[] difBuffer = GetDifBuffer(plain, encrypted);
        for (int i = 0; i < 0x100; i++)
        {
            XorAddTransform xoradd;
            byte add = (byte)(i & 0x7F);
            xoradd._xorSum = (i >= 0x80);
            xoradd._addByte = add;
            xoradd._xorByte = XorAddTransform.GetXorByte(
                add,
                difBuffer[0],
                encrypted[0],
                xoradd._xorSum
            );
            if (!EncodesAll(xoradd, difBuffer, encrypted))
            {
                continue;
            }

            var decryptor = new HspCryptoTransform { _xorAdd = xoradd };
            if (validator(decryptor.Decryption((byte[])encrypted.Clone())))
            {
                return decryptor;
            }
        }

        return null;
    }

    // The byte-to-byte differences of the known plain text must hit every bit both ways, or
    // the XOR and ADD keys cannot be told apart.
    // ---
    // 既知の平文のバイト間の差分がすべてのビットで 0 と 1 の両方を含まないと、XOR と ADD の
    // キーを区別できない。
    private static byte[] GetDifBuffer(byte[] plain, byte[] encrypted)
    {
        int count = Math.Min(plain.Length, encrypted.Length);
        if (count < 2)
        {
            throw new InvalidOperationException(Strings.BufferSizeTooSmall);
        }

        byte[] difBuffer = new byte[count];
        byte prevByte = 0;
        byte andByte = 0xFF;
        byte orByte = 0x00;
        for (int i = 0; i < count; i++)
        {
            difBuffer[i] = XorAddTransform.Dif(plain[i], prevByte);
            prevByte = plain[i];
            andByte &= difBuffer[i];
            orByte |= difBuffer[i];
        }

        return (andByte == 0x00) && (orByte == 0xFF)
            ? difBuffer
            : throw new InvalidOperationException(Strings.InsufficientDecryptionInfo);
    }

    private static bool EncodesAll(XorAddTransform xoradd, byte[] difBuffer, byte[] encrypted)
    {
        for (int index = 1; index < difBuffer.Length; index++)
        {
            if (encrypted[index] != xoradd.Encode(difBuffer[index]))
            {
                return false;
            }
        }

        return true;
    }
}

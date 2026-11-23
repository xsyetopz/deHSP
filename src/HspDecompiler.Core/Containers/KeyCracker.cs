using System;
using HspDecompiler.Core.DpmToAx.Crypto;

namespace HspDecompiler.Core.Containers;

// Finds the key of an encrypted archive entry. Many wrong keys turn the first bytes into an AX
// magic, so a key counts only when the decrypted bytes pass looksDecoded; the pipeline passes a
// full decode there, because no lighter check has been shown to accept the same keys.
//
// 暗号化されたアーカイブのエントリの鍵を探す。誤った鍵の多くも先頭を AX のマジックに変える
// ため、復号したバイト列が looksDecoded を通った鍵だけを採用する。パイプラインはそこに完全な
// 復元を渡す。より軽い検査が同じ鍵を選ぶことはまだ示されていないためである。
internal sealed class KeyCracker(Func<ReadOnlyMemory<byte>, bool> looksDecoded)
{
    internal byte[]? Decrypt(byte[] encrypted) =>
        HspCryptoTransform
            .CrackEncryption(encrypted, bytes => looksDecoded(bytes))
            ?.Decryption(encrypted);
}

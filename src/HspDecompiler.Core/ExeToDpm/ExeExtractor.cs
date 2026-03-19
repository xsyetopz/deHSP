using System.IO;
using HspDecompiler.Core.Exceptions;

namespace HspDecompiler.Core.ExeToDpm
{
    // Legacy/unused: no callers exist outside this file in the current codebase.
    internal sealed class ExeExtractor
    {
        // Known offsets where DPMX data begins in HSP runtime executables.
        private const long DpmxOffsetLarge = 0x25000;
        private const long DpmxOffsetSmall = 0x1BE00;

        // DPMX magic bytes: 'D', 'P', 'M', 'X'
        private const byte DpmxMagicD = 0x44;
        private const byte DpmxMagicP = 0x50;
        private const byte DpmxMagicM = 0x4D;
        private const byte DpmxMagicX = 0x58;

        internal void GetDpmFile(string exeFilePath, string dpmFilePath)
        {
            try
            {
                FileStream stream = new FileStream(exeFilePath, FileMode.Open, FileAccess.Read);
                using (stream)
                {
                    FileStream dpmStream = new FileStream(dpmFilePath, FileMode.Create, FileAccess.Write);
                    using (dpmStream)
                    {
                        GetDpmFile(stream, dpmStream);
                    }
                }
            }
            catch (IOException ex)
            {
                throw new HspDecoderException(ex.Message, ex);
            }
        }

        internal void GetDpmFile(Stream exeStream, Stream dpmStream)
        {
            try
            {
                long dpmOffset = seekDpmStart(exeStream);
                if (dpmOffset < 0)
                {
                    return;
                }

                exeStream.Seek(dpmOffset, SeekOrigin.Begin);
                int dpmSize = (int)(exeStream.Length - dpmOffset);
                byte[] data = new byte[dpmSize];
                exeStream.Read(data, 0, dpmSize);
                dpmStream.Write(data, 0, dpmSize);
            }
            catch (IOException ex)
            {
                throw new HspDecoderException(ex.Message, ex);
            }
        }

        private long seekDpmStart(Stream exeStream)
        {
            byte[] header = new byte[4];
            if (exeStream.Length >= DpmxOffsetLarge + 4)
            {
                exeStream.Seek(DpmxOffsetLarge, SeekOrigin.Begin);
                exeStream.Read(header, 0, 4);
                if (header[0] == DpmxMagicD && header[1] == DpmxMagicP && header[2] == DpmxMagicM && header[3] == DpmxMagicX)
                {
                    return DpmxOffsetLarge;
                }
            }

            if (exeStream.Length >= DpmxOffsetSmall + 4)
            {
                exeStream.Seek(DpmxOffsetSmall, SeekOrigin.Begin);
                exeStream.Read(header, 0, 4);
                if (header[0] == DpmxMagicD && header[1] == DpmxMagicP && header[2] == DpmxMagicM && header[3] == DpmxMagicX)
                {
                    return DpmxOffsetSmall;
                }
            }

            exeStream.Seek(0, SeekOrigin.Begin);
            long index = 0;
            long length = exeStream.Length;
            while (index < length)
            {
                exeStream.Seek(index, SeekOrigin.Begin);
                if (exeStream.Read(header, 0, 4) < 4)
                {
                    break;
                }

                if (header[0] == DpmxMagicD && header[1] == DpmxMagicP && header[2] == DpmxMagicM && header[3] == DpmxMagicX)
                {
                    return index;
                }

                index += 0x04;
            }
            return -1;
        }
    }
}

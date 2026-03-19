using System;
using System.IO;
using HspDecompiler.Core.Ax3;
using Xunit;

namespace HspDecompiler.Core.Tests
{
    public class Hsp3DictionaryTests
    {
        [Fact]
        public void FromFileLoadsRealDictionary()
        {
            string dictPath = Path.Combine(AppContext.BaseDirectory, "Dictionary.csv");
            if (!File.Exists(dictPath))
            {
                return;
            }

            var dict = Hsp3Dictionary.FromFile(dictPath);
            Assert.NotNull(dict);
        }

        [Fact]
        public void FromFileMissingFileReturnsNull()
        {
            var dict = Hsp3Dictionary.FromFile("/nonexistent/path/Dictionary.csv");
            Assert.Null(dict);
        }
    }
}

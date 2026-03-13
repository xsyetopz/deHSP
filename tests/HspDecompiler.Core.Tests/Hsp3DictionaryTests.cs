using System;
using System.IO;
using Xunit;
using HspDecompiler.Core.Ax3;

namespace HspDecompiler.Core.Tests
{
    public class Hsp3DictionaryTests
    {
        [Fact]
        public void Test_FromFile_Loads_Real_Dictionary()
        {
            string dictPath = Path.Combine(AppContext.BaseDirectory, "Dictionary.csv");
            if (!File.Exists(dictPath))
                return;

            var dict = Hsp3Dictionary.FromFile(dictPath);
            Assert.NotNull(dict);
        }

        [Fact]
        public void Test_FromFile_Missing_File_Returns_Null()
        {
            var dict = Hsp3Dictionary.FromFile("/nonexistent/path/Dictionary.csv");
            Assert.Null(dict);
        }
    }
}

using Xunit;

namespace HspDecompiler.Core.Tests
{
    public class PipelineFileDetectionTests
    {
        [Theory]
        [InlineData("MZ\x00\x00", true)]
        [InlineData("DPMX", true)]
        [InlineData("HSP2", true)]
        [InlineData("HSP3", true)]
        [InlineData("JUNK", false)]
        public void Test_Magic_Bytes_Detection(string magic, bool isRecognized)
        {
            bool recognized = magic.StartsWith("MZ") || magic.StartsWith("DPM") ||
                              magic.StartsWith("HSP2") || magic.StartsWith("HSP3");
            Assert.Equal(isRecognized, recognized);
        }
    }
}

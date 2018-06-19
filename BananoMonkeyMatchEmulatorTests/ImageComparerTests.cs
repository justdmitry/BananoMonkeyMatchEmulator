namespace BananoMonkeyMatchEmulator
{
    using System;
    using System.Drawing;
    using System.Reflection;
    using Xunit;

    public class ImageComparerTests
    {
        [Theory]
        [InlineData("26250.png", "38685.png", true)]
        [InlineData("54594.png", "77493.png", true)]
        [InlineData("37095.png", "98748.png", false)]
        [InlineData("57207.png", "92230.png", false)]
        public void TestV1(string file1, string file2, bool match)
        {
            const string resPrefix = "BananoMonkeyMatchEmulator.samples.v1_june2018.";

            using (var stream1 = Assembly.GetExecutingAssembly().GetManifestResourceStream(resPrefix + file1))
            {
                using (var stream2 = Assembly.GetExecutingAssembly().GetManifestResourceStream(resPrefix + file2))
                {
                    using (var image1 = Image.FromStream(stream1))
                    {
                        using (var image2 = Image.FromStream(stream2))
                        {
                            var equal = ImageComparer.AreEqual(image1, image2, out var similarity);
                            Assert.Equal(match, equal);
                        }
                    }
                }
            }
        }
    }
}

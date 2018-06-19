namespace BananoMonkeyMatchEmulator
{
    using System.Drawing;

    public static class ImageComparer
    {
        public static bool AreEqual(Image firstImage, Image secondImage, out float similarity)
        {
            float equal = 0;
            float diff = 0;
            using (var firstBitmap = new Bitmap(firstImage))
            {
                using (var secondBitmap = new Bitmap(secondImage))
                {
                    for (var i = 0; i < firstBitmap.Width; i++)
                    {
                        for (var j = 0; j < firstBitmap.Height; j++)
                        {
                            var eq = firstBitmap.GetPixel(i, j) == secondBitmap.GetPixel(i, j);
                            if (eq)
                            {
                                equal++;
                            }
                            else
                            {
                                diff++;
                            }
                        }
                    }
                }
            }

            similarity = equal / (diff + equal);
            return similarity > 0.95;
        }
    }
}
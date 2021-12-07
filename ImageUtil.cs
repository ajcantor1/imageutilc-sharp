/// <summary>
/// Static Image Utility class to handle image merging
/// </summary>
unsafe public static class ImageUtil
{
    /// <summary>
    /// Data structure that represents a single RGB pixel of a 24 byte image. 
    /// </summary>
    protected struct PixelData
    {
        /// <summary>
        /// Blue intensity (0-255)
        /// </summary>
        public byte Blue;

        /// <summary>
        /// Green intensity (0-255)
        /// </summary>
        public byte Green;

        /// <summary>
        /// Red intensity (0-255)
        /// </summary>
        public byte Red;

    }

    /// <summary>
    /// Compose a multispectral image (24 byte).
    /// White light image will be used in the blue layer and
    /// UV light image will be used in the green layer
    /// </summary>
    /// <param name="white">White light image of blister</param>
    /// <param name="uv">UV light image of blister</param>
    /// <param name="translation">
    /// Amount pixels in x direction that the UV light image 
    /// needs to be translated to correct for offset
    /// </param>
    /// <returns></returns>
    public static Bitmap mergeBitmaps(Bitmap white, Bitmap uv, int translation)
    {

        int width = Math.Min(white.Width, uv.Width);
        int height = Math.Min(white.Height, uv.Height);
        
        /// Size of multispectal images
        Size size = new Size(width, height);

        Rectangle bounds = new Rectangle(Point.Empty, size);

        // Bitmap memory will be "locked" in system memory
        BitmapData whiteData = white.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        BitmapData uvData = uv.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

        /// Point to first byte of white light image
        Byte* whiteBase = (Byte*)whiteData.Scan0.ToPointer();      

        /// Point to first byte of UV light image
        Byte* uvBase = (Byte*)uvData.Scan0.ToPointer();

        // Compose a completely black image R=0, G=0, B=0 that will store the multispectral image
        Bitmap multiBitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (Graphics gfx = Graphics.FromImage(multiBitmap))
        {
            using (SolidBrush brush = new SolidBrush(Color.Black))
            {
                gfx.FillRectangle(brush, 0, 0, width, height);
            }
        }

        // Lock newly created biymap in memory
        BitmapData multiData = multiBitmap.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

        Byte* multiBase = (Byte*)multiData.Scan0.ToPointer();


        // Use default task scheduler to run as many "merging" operations as parallel.
        Parallel.For(0, width*height, pixel =>
        {
            PixelData* multiPixel = (PixelData*)(multiBase + pixel * sizeof(PixelData));

            
            if (translation >= 0)
            {
                // Correct with provided offset. To ensure that UV and White perfect overlay upon each other. 
                PixelData* uvPixel = (PixelData*)(uvBase + pixel * sizeof(PixelData));

                multiPixel->Green = uvPixel->Blue;

                // if part of the white image does not exist in the uv image leave the pixels empty (black, #000)
                if (pixel < translation * width)
                {

                    multiPixel->Blue = 0;

                }
                else
                {
                    PixelData* whitePixel = (PixelData*)((whiteBase + (pixel - translation * width) * sizeof(PixelData)));

                    multiPixel->Blue = whitePixel->Blue;

                }

            }
            else
            {

                // Correct for offset in the other direction.
                PixelData* whitePixel = (PixelData*)(whiteBase + pixel * sizeof(PixelData));

                multiPixel->Blue = whitePixel->Blue;

                if (pixel < Math.Abs(translation * width))
                {


                    multiPixel->Green = 0;

                }
                else
                {
                    PixelData* uvPixel = (PixelData*)((uvBase + (pixel - Math.Abs(translation * width)) * sizeof(PixelData)));

                    multiPixel->Green = uvPixel->Blue;

                }
            }

            // Red layer is not used
            multiPixel->Red = 0;
        });

        uv.UnlockBits(uvData);
        white.UnlockBits(whiteData);

        multiBitmap.UnlockBits(multiData);

        multiBase = null;
        uvBase = null;
        whiteBase = null;
        }

        return multiBitmap;
}

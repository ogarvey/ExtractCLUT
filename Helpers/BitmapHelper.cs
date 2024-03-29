using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Gif;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using System.Drawing;
using Image = System.Drawing.Image;
using Color = System.Drawing.Color;
using SizeF = System.Drawing.SizeF;
using ColorMatrix = System.Drawing.Imaging.ColorMatrix;

namespace ExtractCLUT.Helpers
{

  
  public static class BitmapHelper
  {
    public static Image TintImage(Image originalImage, Color tint)
    {
      // Create a new bitmap with the same size as the original image
      Bitmap tintedImage = new Bitmap(originalImage.Width, originalImage.Height);

      // Create a ColorMatrix and set its tint
      float[][] colorMatrixElements = {
        new float[] { tint.R / 255.0f, 0, 0, 0, 0 },
        new float[] { 0, tint.G / 255.0f, 0, 0, 0 },
        new float[] { 0, 0, tint.B / 255.0f, 0, 0 },
        new float[] { 0, 0, 0, 1, 0 },
        new float[] { 0, 0, 0, 0, 1 }
      };

      ColorMatrix colorMatrix = new ColorMatrix(colorMatrixElements);

      using (ImageAttributes attributes = new ImageAttributes())
      {
        attributes.SetColorMatrix(colorMatrix);

        using (Graphics g = Graphics.FromImage(tintedImage))
        {
          g.DrawImage(
              originalImage,
              new Rectangle(0, 0, originalImage.Width, originalImage.Height),
              0, 0, originalImage.Width, originalImage.Height,
              GraphicsUnit.Pixel,
              attributes);
        }
      }

      return tintedImage;
    }

    public static Image AddBitmapText(Image bitmap, string text)
    {
      using (Graphics graphics = Graphics.FromImage(bitmap))
      {
        // Set the smoothing mode for better text quality
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        // Define the font and brush for the text
        using (Font font = new Font("Arial", 4, FontStyle.Bold))
        using (Brush textBrush = new SolidBrush(Color.Cyan))
        {

          // Draw the text (index) on the square
          SizeF textSize = graphics.MeasureString(text, font);

          graphics.DrawString(text, font, textBrush, 0,0);
        }
      }
      return bitmap;
    }
    const int Ymask = 0x00ff0000;
    const int Umask = 0x0000ff00;
    const int Vmask = 0x000000ff;

    const uint MaskAlpha = 0xff000000;
    const uint MaskGreen = 0x0000ff00;
    const uint MaskRedBlue = 0x00ff00ff;

    const int AlphaShift = 24;
    public static uint Mix3To1(uint c1, uint c2)
    {
      return MixColours(3, 1, c1, c2);
    }

    public static uint Mix2To1To1(uint c1, uint c2, uint c3)
    {
      return MixColours(2, 1, 1, c1, c2, c3);
    }

    public static uint Mix7To1(uint c1, uint c2)
    {
      return MixColours(7, 1, c1, c2);
    }

    public static uint Mix2To7To7(uint c1, uint c2, uint c3)
    {
      return MixColours(2, 7, 7, c1, c2, c3);
    }

    public static uint MixEven(uint c1, uint c2)
    {
      return MixColours(1, 1, c1, c2);
    }

    public static uint Mix5To2To1(uint c1, uint c2, uint c3)
    {
      return MixColours(5, 2, 1, c1, c2, c3);
    }

    public static uint Mix6To1To1(uint c1, uint c2, uint c3)
    {
      return MixColours(6, 1, 1, c1, c2, c3);
    }

    public static uint Mix5To3(uint c1, uint c2)
    {
      return MixColours(5, 3, c1, c2);
    }

    public static uint Mix2To3To3(uint c1, uint c2, uint c3)
    {
      return MixColours(2, 3, 3, c1, c2, c3);
    }

    public static uint Mix14To1To1(uint c1, uint c2, uint c3)
    {
      return MixColours(14, 1, 1, c1, c2, c3);
    }

    // This method can overflow between blue and red and from red to nothing when the sum of all weightings is higher than 255.
    // It only works for weightings with a sum that is a power of two, otherwise the blue value is corrupted.
    // Parameters: weighting0, weighting1[, ...], colour0, colour1[, ...]
    public static uint MixColours(params uint[] weightingsAndColours)
    {
      uint totalPartsColour = 0;
      uint totalPartsAlpha = 0;

      uint totalGreen = 0;
      uint totalRedBlue = 0;
      uint totalAlpha = 0;

      for (int i = 0; i < weightingsAndColours.Length / 2; i++)
      {
        var weighting = weightingsAndColours[i];
        var colour = weightingsAndColours[weightingsAndColours.Length / 2 + i];

        if (weighting > 0)
        {

          var alpha = (colour >> AlphaShift) * weighting;

          totalPartsAlpha += weighting;
          if (alpha != 0)
          {
            totalAlpha += alpha;

            totalPartsColour += weighting;
            totalGreen += (colour & MaskGreen) * weighting;
            totalRedBlue += (colour & MaskRedBlue) * weighting;
          }
        }
      }

      totalAlpha /= totalPartsAlpha;
      totalAlpha <<= AlphaShift;

      if (totalPartsColour > 0)
      {
        totalGreen /= totalPartsColour;
        totalGreen &= MaskGreen;

        totalRedBlue /= totalPartsColour;
        totalRedBlue &= MaskRedBlue;
      }

      return totalAlpha | totalGreen | totalRedBlue;
    }
    const uint RgbMask = 0x00ffffff;

#if EASYTHREADING
      private static volatile int[] lookupTable;
#else
    private static int[] lookupTable;
#endif

    private static int[] LookupTable
    {
      get
      {
        if (lookupTable == null) Initialize();
        return lookupTable;
      }
    }

    /// <summary>
    /// Gets whether the lookup table is ready.
    /// </summary>
    public static bool Initialized
    {
      get
      {
        return lookupTable != null;
      }
    }

    /// <summary>
    /// Returns the 24bit YUV equivalent of the provided 24bit RGB color.
    /// <para>Any alpha component is dropped.</para>
    /// </summary>
    /// <param name="rgb">A 24bit rgb color.</param>
    /// <returns>The corresponding 24bit YUV color.</returns>
    public static int GetYuv(uint rgb)
    {
      return LookupTable[rgb & RgbMask];
    }

    /// <summary>
    /// Calculates the lookup table.
    /// </summary>
    public static unsafe void Initialize()
    {
      var lTable = new int[0x1000000]; // 256 * 256 * 256
      fixed (int* lookupP = lTable)
      {
        byte* lP = (byte*)lookupP;
        for (uint i = 0; i < lTable.Length; i++)
        {
#if EASYTHREADING
    if(lookupTable != null) return; // table was set by another thread
#endif

          float r = (i & 0xff0000) >> 16;
          float g = (i & 0x00ff00) >> 8;
          float b = (i & 0x0000ff);

          lP++; //Skip alpha byte
          *(lP++) = (byte)(.299 * r + .587 * g + .114 * b);
          *(lP++) = (byte)((int)(-.169 * r - .331 * g + .5 * b) + 128);
          *(lP++) = (byte)((int)(.5 * r - .419 * g - .081 * b) + 128);
        }
      }
      lookupTable = lTable;
    }

    /// <summary>
    /// Releases the reference to the lookup table.
    /// <para>The table has to be calculated again for the next lookup.</para>
    /// </summary>
    public static void UnloadLookupTable()
    {
      lookupTable = null;
    }
    public static void ReplaceColorWithTransparent(string inputPath, string outputPath, System.Drawing.Color targetColor)
    {
      var R = targetColor.R;
      var G = targetColor.G;
      var B = targetColor.B;


      // Load the PNG image from the input path
      using Bitmap image = new Bitmap(inputPath);

      // Replace the target color with transparent pixels
      for (int x = 0; x < image.Width; x++)
      {
        for (int y = 0; y < image.Height; y++)
        {
          if (image.GetPixel(x, y).R == R && image.GetPixel(x, y).G == G && image.GetPixel(x, y).B == B)
          {
            image.SetPixel(x, y, System.Drawing.Color.Transparent);
          }
        }
      }

      // Save the modified image as a new PNG file
      image.Save(outputPath, ImageFormat.Png);
    }
    /// <summary>
    /// Compares two ARGB colors according to the provided Y, U, V and A thresholds.
    /// </summary>
    /// <param name="c1">An ARGB color.</param>
    /// <param name="c2">A second ARGB color.</param>
    /// <param name="trY">The Y (luminance) threshold.</param>
    /// <param name="trU">The U (chrominance) threshold.</param>
    /// <param name="trV">The V (chrominance) threshold.</param>
    /// <param name="trA">The A (transparency) threshold.</param>
    /// <returns>Returns true if colors differ more than the thresholds permit, otherwise false.</returns>
    private static bool Diff(uint c1, uint c2, uint trY, uint trU, uint trV, uint trA)
    {
      int YUV1 = (int)GetYuv(c1);
      int YUV2 = (int)GetYuv(c2);

      return ((Math.Abs((YUV1 & Ymask) - (YUV2 & Ymask)) > trY) ||
      (Math.Abs((YUV1 & Umask) - (YUV2 & Umask)) > trU) ||
      (Math.Abs((YUV1 & Vmask) - (YUV2 & Vmask)) > trV) ||
      (Math.Abs(((int)((uint)c1 >> 24) - (int)((uint)c2 >> 24))) > trA));
    }

    public static unsafe Bitmap Scale4(this Bitmap bitmap, uint trY = 48, uint trU = 7, uint trV = 6, uint trA = 0, bool wrapX = false, bool wrapY = false)
    {

      int Xres = bitmap.Width;
      int Yres = bitmap.Height;

      var dest = new Bitmap(bitmap.Width * 4, bitmap.Height * 4);

      var bmpData = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
      var destData = dest.LockBits(new Rectangle(Point.Empty, dest.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
      {

        uint* sp = (uint*)bmpData.Scan0.ToPointer();
        uint* dp = (uint*)destData.Scan0.ToPointer();

        Scale4(sp, dp, Xres, Yres, trY, trU, trV, trA, wrapX, wrapY);
      }
      bitmap.UnlockBits(bmpData);
      dest.UnlockBits(destData);

      return dest;
    }

    public static unsafe void Scale4(uint* sp, uint* dp, int Xres, int Yres, uint trY = 48, uint trU = 7, uint trV = 6, uint trA = 0, bool wrapX = false, bool wrapY = false)
    {
      //Don't shift trA, as it uses shift right instead of a mask for comparisons.
      trY <<= 2 * 8;
      trU <<= 1 * 8;
      int dpL = Xres * 4;

      int prevline, nextline;
      var w = new uint[9];

      for (int j = 0; j < Yres; j++)
      {
        if (j > 0)
        {
          prevline = -Xres;
        }
        else
        {
          if (wrapY)
          {
            prevline = Xres * (Yres - 1);
          }
          else
          {
            prevline = 0;
          }
        }
        if (j < Yres - 1)
        {
          nextline = Xres;
        }
        else
        {
          if (wrapY)
          {
            nextline = -(Xres * (Yres - 1));
          }
          else
          {
            nextline = 0;
          }
        }

        for (int i = 0; i < Xres; i++)
        {
          w[1] = *(sp + prevline);
          w[4] = *sp;
          w[7] = *(sp + nextline);

          if (i > 0)
          {
            w[0] = *(sp + prevline - 1);
            w[3] = *(sp - 1);
            w[6] = *(sp + nextline - 1);
          }
          else
          {
            if (wrapX)
            {
              w[0] = *(sp + prevline + Xres - 1);
              w[3] = *(sp + Xres - 1);
              w[6] = *(sp + nextline + Xres - 1);
            }
            else
            {
              w[0] = w[1];
              w[3] = w[4];
              w[6] = w[7];
            }
          }

          if (i < Xres - 1)
          {
            w[2] = *(sp + prevline + 1);
            w[5] = *(sp + 1);
            w[8] = *(sp + nextline + 1);
          }
          else
          {
            if (wrapX)
            {
              w[2] = *(sp + prevline - Xres + 1);
              w[5] = *(sp - Xres + 1);
              w[8] = *(sp + nextline - Xres + 1);
            }
            else
            {
              w[2] = w[1];
              w[5] = w[4];
              w[8] = w[7];
            }
          }

          int pattern = 0;
          int flag = 1;

          for (int k = 0; k < 9; k++)
          {
            if (k == 4) continue;

            if (w[k] != w[4])
            {
              if (Diff(w[4], w[k], trY, trU, trV, trA))
                pattern |= flag;
            }
            flag <<= 1;
          }

          switch (pattern)
          {
            case 0:
            case 1:
            case 4:
            case 32:
            case 128:
            case 5:
            case 132:
            case 160:
            case 33:
            case 129:
            case 36:
            case 133:
            case 164:
            case 161:
            case 37:
            case 165:
              {
                *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[3]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[5]);
                *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[1]);
                *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[1]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                break;
              }
            case 2:
            case 34:
            case 130:
            case 162:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[2]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                break;
              }
            case 16:
            case 17:
            case 48:
            case 49:
              {
                *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[3]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[1]);
                *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 64:
            case 65:
            case 68:
            case 69:
              {
                *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[3]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[5]);
                *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[1]);
                *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[1]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 8:
            case 12:
            case 136:
            case 140:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[0]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[5]);
                *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[1]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                break;
              }
            case 3:
            case 35:
            case 131:
            case 163:
              {
                *(dp) = Mix5To3(w[4], w[3]);
                *(dp + 1) = Mix7To1(w[4], w[3]);
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[2]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                break;
              }
            case 6:
            case 38:
            case 134:
            case 166:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                *(dp + 2) = Mix7To1(w[4], w[5]);
                *(dp + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                break;
              }
            case 20:
            case 21:
            case 52:
            case 53:
              {
                *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[3]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                *(dp + 3) = Mix5To3(w[4], w[1]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[1]);
                *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 144:
            case 145:
            case 176:
            case 177:
              {
                *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[3]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[1]);
                *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                break;
              }
            case 192:
            case 193:
            case 196:
            case 197:
              {
                *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[3]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[5]);
                *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[1]);
                *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[1]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 96:
            case 97:
            case 100:
            case 101:
              {
                *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[3]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[5]);
                *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[1]);
                *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[1]);
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 40:
            case 44:
            case 168:
            case 172:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[0]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[5]);
                *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[1]);
                *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                break;
              }
            case 9:
            case 13:
            case 137:
            case 141:
              {
                *(dp) = Mix5To3(w[4], w[1]);
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[5]);
                *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                *(dp + dpL) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[1]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                break;
              }
            case 18:
            case 50:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = Mix3To1(w[4], w[2]);
                  *(dp + 3) = Mix5To3(w[4], w[2]);
                  *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                  *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                }
                else
                {
                  *(dp + 2) = MixEven(w[1], w[4]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 2) = w[4];
                  *(dp + dpL + 3) = MixEven(w[5], w[4]);
                }
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 80:
            case 81:
              {
                *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[3]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[1]);
                *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                }
                else
                {
                  *(dp + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + 3) = MixEven(w[5], w[4]);
                  *(dp + dpL + dpL + dpL + 2) = MixEven(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                break;
              }
            case 72:
            case 76:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[0]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[5]);
                *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[1]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                  *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                  *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                }
                else
                {
                  *(dp + dpL + dpL) = MixEven(w[3], w[4]);
                  *(dp + dpL + dpL + 1) = w[4];
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = MixEven(w[7], w[4]);
                }
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[8]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 10:
            case 138:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *(dp) = Mix5To3(w[4], w[0]);
                  *(dp + 1) = Mix3To1(w[4], w[0]);
                  *(dp + dpL) = Mix3To1(w[4], w[0]);
                  *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = MixEven(w[1], w[4]);
                  *(dp + dpL) = MixEven(w[3], w[4]);
                  *(dp + dpL + 1) = w[4];
                }
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[2]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                break;
              }
            case 66:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[2]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 24:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[0]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 7:
            case 39:
            case 135:
              {
                *(dp) = Mix5To3(w[4], w[3]);
                *(dp + 1) = Mix7To1(w[4], w[3]);
                *(dp + 2) = Mix7To1(w[4], w[5]);
                *(dp + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                break;
              }
            case 148:
            case 149:
            case 180:
              {
                *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[3]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                *(dp + 3) = Mix5To3(w[4], w[1]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[1]);
                *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                break;
              }
            case 224:
            case 228:
            case 225:
              {
                *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[3]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[5]);
                *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[1]);
                *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[1]);
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 41:
            case 169:
            case 45:
              {
                *(dp) = Mix5To3(w[4], w[1]);
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[5]);
                *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                *(dp + dpL) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[1]);
                *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                break;
              }
            case 22:
            case 54:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = w[4];
                  *(dp + 3) = w[4];
                  *(dp + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + 2) = MixEven(w[1], w[4]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 3) = MixEven(w[5], w[4]);
                }
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = w[4];
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 208:
            case 209:
              {
                *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[3]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[1]);
                *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 3) = w[4];
                  *(dp + dpL + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + 3) = MixEven(w[5], w[4]);
                  *(dp + dpL + dpL + dpL + 2) = MixEven(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                break;
              }
            case 104:
            case 108:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[0]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[5]);
                *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[1]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL + 1) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL) = MixEven(w[3], w[4]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = MixEven(w[7], w[4]);
                }
                *(dp + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[8]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 11:
            case 139:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                  *(dp + 1) = w[4];
                  *(dp + dpL) = w[4];
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = MixEven(w[1], w[4]);
                  *(dp + dpL) = MixEven(w[3], w[4]);
                }
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[2]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                break;
              }
            case 19:
            case 51:
              {
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp) = Mix5To3(w[4], w[3]);
                  *(dp + 1) = Mix7To1(w[4], w[3]);
                  *(dp + 2) = Mix3To1(w[4], w[2]);
                  *(dp + 3) = Mix5To3(w[4], w[2]);
                  *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                  *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                }
                else
                {
                  *(dp) = Mix3To1(w[4], w[1]);
                  *(dp + 1) = Mix3To1(w[1], w[4]);
                  *(dp + 2) = Mix5To3(w[1], w[5]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                  *(dp + dpL + 3) = Mix2To1To1(w[5], w[4], w[1]);
                }
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 146:
            case 178:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = Mix3To1(w[4], w[2]);
                  *(dp + 3) = Mix5To3(w[4], w[2]);
                  *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                  *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                  *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                  *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                }
                else
                {
                  *(dp + 2) = Mix2To1To1(w[1], w[4], w[5]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                  *(dp + dpL + 3) = Mix5To3(w[5], w[1]);
                  *(dp + dpL + dpL + 3) = Mix3To1(w[5], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = Mix3To1(w[4], w[5]);
                }
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                break;
              }
            case 84:
            case 85:
              {
                *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[3]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + 3) = Mix5To3(w[4], w[1]);
                  *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                  *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                }
                else
                {
                  *(dp + 3) = Mix3To1(w[4], w[5]);
                  *(dp + dpL + 3) = Mix3To1(w[5], w[4]);
                  *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                  *(dp + dpL + dpL + 3) = Mix5To3(w[5], w[7]);
                  *(dp + dpL + dpL + dpL + 2) = Mix2To1To1(w[7], w[4], w[5]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[1]);
                *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                break;
              }
            case 112:
            case 113:
              {
                *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[3]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[1]);
                *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                }
                else
                {
                  *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                  *(dp + dpL + dpL + 3) = Mix2To1To1(w[5], w[4], w[7]);
                  *(dp + dpL + dpL + dpL) = Mix3To1(w[4], w[7]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[7], w[5]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                break;
              }
            case 200:
            case 204:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[0]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[5]);
                *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[1]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                  *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                  *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                  *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                  *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                }
                else
                {
                  *(dp + dpL + dpL) = Mix2To1To1(w[3], w[4], w[7]);
                  *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = Mix3To1(w[4], w[7]);
                }
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 73:
            case 77:
              {
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp) = Mix5To3(w[4], w[1]);
                  *(dp + dpL) = Mix7To1(w[4], w[1]);
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                  *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                  *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                }
                else
                {
                  *(dp) = Mix3To1(w[4], w[3]);
                  *(dp + dpL) = Mix3To1(w[3], w[4]);
                  *(dp + dpL + dpL) = Mix5To3(w[3], w[7]);
                  *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = Mix2To1To1(w[7], w[4], w[3]);
                }
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[5]);
                *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[1]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[8]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 42:
            case 170:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *(dp) = Mix5To3(w[4], w[0]);
                  *(dp + 1) = Mix3To1(w[4], w[0]);
                  *(dp + dpL) = Mix3To1(w[4], w[0]);
                  *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                  *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                  *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = Mix2To1To1(w[1], w[4], w[3]);
                  *(dp + dpL) = Mix5To3(w[3], w[1]);
                  *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                  *(dp + dpL + dpL) = Mix3To1(w[3], w[4]);
                  *(dp + dpL + dpL + dpL) = Mix3To1(w[4], w[3]);
                }
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[2]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                break;
              }
            case 14:
            case 142:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *(dp) = Mix5To3(w[4], w[0]);
                  *(dp + 1) = Mix3To1(w[4], w[0]);
                  *(dp + 2) = Mix7To1(w[4], w[5]);
                  *(dp + 3) = Mix5To3(w[4], w[5]);
                  *(dp + dpL) = Mix3To1(w[4], w[0]);
                  *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = Mix5To3(w[1], w[3]);
                  *(dp + 2) = Mix3To1(w[1], w[4]);
                  *(dp + 3) = Mix3To1(w[4], w[1]);
                  *(dp + dpL) = Mix2To1To1(w[3], w[4], w[1]);
                  *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                }
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                break;
              }
            case 67:
              {
                *(dp) = Mix5To3(w[4], w[3]);
                *(dp + 1) = Mix7To1(w[4], w[3]);
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[2]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 70:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                *(dp + 2) = Mix7To1(w[4], w[5]);
                *(dp + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 28:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[0]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                *(dp + 3) = Mix5To3(w[4], w[1]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 152:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[0]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                break;
              }
            case 194:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[2]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 98:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[2]);
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 56:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[0]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 25:
              {
                *(dp) = Mix5To3(w[4], w[1]);
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 26:
            case 31:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                  *(dp + 1) = w[4];
                  *(dp + dpL) = w[4];
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = MixEven(w[1], w[4]);
                  *(dp + dpL) = MixEven(w[3], w[4]);
                }
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = w[4];
                  *(dp + 3) = w[4];
                  *(dp + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + 2) = MixEven(w[1], w[4]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 3) = MixEven(w[5], w[4]);
                }
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + 2) = w[4];
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 82:
            case 214:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = w[4];
                  *(dp + 3) = w[4];
                  *(dp + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + 2) = MixEven(w[1], w[4]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 3) = MixEven(w[5], w[4]);
                }
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = w[4];
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 3) = w[4];
                  *(dp + dpL + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + 3) = MixEven(w[5], w[4]);
                  *(dp + dpL + dpL + dpL + 2) = MixEven(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                break;
              }
            case 88:
            case 248:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[0]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL + 1) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL) = MixEven(w[3], w[4]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = MixEven(w[7], w[4]);
                }
                *(dp + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 3) = w[4];
                  *(dp + dpL + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + 3) = MixEven(w[5], w[4]);
                  *(dp + dpL + dpL + dpL + 2) = MixEven(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                break;
              }
            case 74:
            case 107:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                  *(dp + 1) = w[4];
                  *(dp + dpL) = w[4];
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = MixEven(w[1], w[4]);
                  *(dp + dpL) = MixEven(w[3], w[4]);
                }
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[2]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL + 1) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL) = MixEven(w[3], w[4]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = MixEven(w[7], w[4]);
                }
                *(dp + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[8]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 27:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                  *(dp + 1) = w[4];
                  *(dp + dpL) = w[4];
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = MixEven(w[1], w[4]);
                  *(dp + dpL) = MixEven(w[3], w[4]);
                }
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 86:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = w[4];
                  *(dp + 3) = w[4];
                  *(dp + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + 2) = MixEven(w[1], w[4]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 3) = MixEven(w[5], w[4]);
                }
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = w[4];
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 216:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[0]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 3) = w[4];
                  *(dp + dpL + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + 3) = MixEven(w[5], w[4]);
                  *(dp + dpL + dpL + dpL + 2) = MixEven(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                break;
              }
            case 106:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[2]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL + 1) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL) = MixEven(w[3], w[4]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = MixEven(w[7], w[4]);
                }
                *(dp + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[8]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 30:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = w[4];
                  *(dp + 3) = w[4];
                  *(dp + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + 2) = MixEven(w[1], w[4]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 3) = MixEven(w[5], w[4]);
                }
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = w[4];
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 210:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 3) = w[4];
                  *(dp + dpL + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + 3) = MixEven(w[5], w[4]);
                  *(dp + dpL + dpL + dpL + 2) = MixEven(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                break;
              }
            case 120:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[0]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL + 1) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL) = MixEven(w[3], w[4]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = MixEven(w[7], w[4]);
                }
                *(dp + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 75:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                  *(dp + 1) = w[4];
                  *(dp + dpL) = w[4];
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = MixEven(w[1], w[4]);
                  *(dp + dpL) = MixEven(w[3], w[4]);
                }
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[2]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 29:
              {
                *(dp) = Mix5To3(w[4], w[1]);
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                *(dp + 3) = Mix5To3(w[4], w[1]);
                *(dp + dpL) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 198:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                *(dp + 2) = Mix7To1(w[4], w[5]);
                *(dp + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 184:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[0]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                break;
              }
            case 99:
              {
                *(dp) = Mix5To3(w[4], w[3]);
                *(dp + 1) = Mix7To1(w[4], w[3]);
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[2]);
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 57:
              {
                *(dp) = Mix5To3(w[4], w[1]);
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 71:
              {
                *(dp) = Mix5To3(w[4], w[3]);
                *(dp + 1) = Mix7To1(w[4], w[3]);
                *(dp + 2) = Mix7To1(w[4], w[5]);
                *(dp + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 156:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[0]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                *(dp + 3) = Mix5To3(w[4], w[1]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                break;
              }
            case 226:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[2]);
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 60:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[0]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                *(dp + 3) = Mix5To3(w[4], w[1]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 195:
              {
                *(dp) = Mix5To3(w[4], w[3]);
                *(dp + 1) = Mix7To1(w[4], w[3]);
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[2]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 102:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                *(dp + 2) = Mix7To1(w[4], w[5]);
                *(dp + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 153:
              {
                *(dp) = Mix5To3(w[4], w[1]);
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                break;
              }
            case 58:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *(dp) = Mix5To3(w[4], w[0]);
                  *(dp + 1) = Mix3To1(w[4], w[0]);
                  *(dp + dpL) = Mix3To1(w[4], w[0]);
                  *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                }
                else
                {
                  *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                  *(dp + 1) = Mix3To1(w[4], w[1]);
                  *(dp + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + 1) = w[4];
                }
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = Mix3To1(w[4], w[2]);
                  *(dp + 3) = Mix5To3(w[4], w[2]);
                  *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                  *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                }
                else
                {
                  *(dp + 2) = Mix3To1(w[4], w[1]);
                  *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                  *(dp + dpL + 2) = w[4];
                  *(dp + dpL + 3) = Mix3To1(w[4], w[5]);
                }
                *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 83:
              {
                *(dp) = Mix5To3(w[4], w[3]);
                *(dp + 1) = Mix7To1(w[4], w[3]);
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = Mix3To1(w[4], w[2]);
                  *(dp + 3) = Mix5To3(w[4], w[2]);
                  *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                  *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                }
                else
                {
                  *(dp + 2) = Mix3To1(w[4], w[1]);
                  *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                  *(dp + dpL + 2) = w[4];
                  *(dp + dpL + 3) = Mix3To1(w[4], w[5]);
                }
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                }
                else
                {
                  *(dp + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[5]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[7]);
                  *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                }
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                break;
              }
            case 92:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[0]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                *(dp + 3) = Mix5To3(w[4], w[1]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                  *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                  *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                }
                else
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + dpL + 1) = w[4];
                  *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[7]);
                }
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                }
                else
                {
                  *(dp + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[5]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[7]);
                  *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                }
                break;
              }
            case 202:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *(dp) = Mix5To3(w[4], w[0]);
                  *(dp + 1) = Mix3To1(w[4], w[0]);
                  *(dp + dpL) = Mix3To1(w[4], w[0]);
                  *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                }
                else
                {
                  *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                  *(dp + 1) = Mix3To1(w[4], w[1]);
                  *(dp + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + 1) = w[4];
                }
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[2]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                  *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                  *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                }
                else
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + dpL + 1) = w[4];
                  *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[7]);
                }
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 78:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *(dp) = Mix5To3(w[4], w[0]);
                  *(dp + 1) = Mix3To1(w[4], w[0]);
                  *(dp + dpL) = Mix3To1(w[4], w[0]);
                  *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                }
                else
                {
                  *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                  *(dp + 1) = Mix3To1(w[4], w[1]);
                  *(dp + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + 1) = w[4];
                }
                *(dp + 2) = Mix7To1(w[4], w[5]);
                *(dp + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                  *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                  *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                }
                else
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + dpL + 1) = w[4];
                  *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[7]);
                }
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[8]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 154:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *(dp) = Mix5To3(w[4], w[0]);
                  *(dp + 1) = Mix3To1(w[4], w[0]);
                  *(dp + dpL) = Mix3To1(w[4], w[0]);
                  *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                }
                else
                {
                  *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                  *(dp + 1) = Mix3To1(w[4], w[1]);
                  *(dp + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + 1) = w[4];
                }
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = Mix3To1(w[4], w[2]);
                  *(dp + 3) = Mix5To3(w[4], w[2]);
                  *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                  *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                }
                else
                {
                  *(dp + 2) = Mix3To1(w[4], w[1]);
                  *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                  *(dp + dpL + 2) = w[4];
                  *(dp + dpL + 3) = Mix3To1(w[4], w[5]);
                }
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                break;
              }
            case 114:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = Mix3To1(w[4], w[2]);
                  *(dp + 3) = Mix5To3(w[4], w[2]);
                  *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                  *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                }
                else
                {
                  *(dp + 2) = Mix3To1(w[4], w[1]);
                  *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                  *(dp + dpL + 2) = w[4];
                  *(dp + dpL + 3) = Mix3To1(w[4], w[5]);
                }
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                }
                else
                {
                  *(dp + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[5]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[7]);
                  *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                }
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                break;
              }
            case 89:
              {
                *(dp) = Mix5To3(w[4], w[1]);
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                  *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                  *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                }
                else
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + dpL + 1) = w[4];
                  *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[7]);
                }
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                }
                else
                {
                  *(dp + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[5]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[7]);
                  *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                }
                break;
              }
            case 90:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *(dp) = Mix5To3(w[4], w[0]);
                  *(dp + 1) = Mix3To1(w[4], w[0]);
                  *(dp + dpL) = Mix3To1(w[4], w[0]);
                  *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                }
                else
                {
                  *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                  *(dp + 1) = Mix3To1(w[4], w[1]);
                  *(dp + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + 1) = w[4];
                }
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = Mix3To1(w[4], w[2]);
                  *(dp + 3) = Mix5To3(w[4], w[2]);
                  *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                  *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                }
                else
                {
                  *(dp + 2) = Mix3To1(w[4], w[1]);
                  *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                  *(dp + dpL + 2) = w[4];
                  *(dp + dpL + 3) = Mix3To1(w[4], w[5]);
                }
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                  *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                  *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                }
                else
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + dpL + 1) = w[4];
                  *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[7]);
                }
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                }
                else
                {
                  *(dp + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[5]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[7]);
                  *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                }
                break;
              }
            case 55:
            case 23:
              {
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp) = Mix5To3(w[4], w[3]);
                  *(dp + 1) = Mix7To1(w[4], w[3]);
                  *(dp + 2) = w[4];
                  *(dp + 3) = w[4];
                  *(dp + dpL + 2) = w[4];
                  *(dp + dpL + 3) = w[4];
                }
                else
                {
                  *(dp) = Mix3To1(w[4], w[1]);
                  *(dp + 1) = Mix3To1(w[1], w[4]);
                  *(dp + 2) = Mix5To3(w[1], w[5]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                  *(dp + dpL + 3) = Mix2To1To1(w[5], w[4], w[1]);
                }
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 182:
            case 150:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = w[4];
                  *(dp + 3) = w[4];
                  *(dp + dpL + 2) = w[4];
                  *(dp + dpL + 3) = w[4];
                  *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                  *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                }
                else
                {
                  *(dp + 2) = Mix2To1To1(w[1], w[4], w[5]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                  *(dp + dpL + 3) = Mix5To3(w[5], w[1]);
                  *(dp + dpL + dpL + 3) = Mix3To1(w[5], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = Mix3To1(w[4], w[5]);
                }
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                break;
              }
            case 213:
            case 212:
              {
                *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[3]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + 3) = Mix5To3(w[4], w[1]);
                  *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                  *(dp + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + 3) = w[4];
                  *(dp + dpL + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + 3) = Mix3To1(w[4], w[5]);
                  *(dp + dpL + 3) = Mix3To1(w[5], w[4]);
                  *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                  *(dp + dpL + dpL + 3) = Mix5To3(w[5], w[7]);
                  *(dp + dpL + dpL + dpL + 2) = Mix2To1To1(w[7], w[4], w[5]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[1]);
                *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                break;
              }
            case 241:
            case 240:
              {
                *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[3]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[1]);
                *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + 3) = w[4];
                  *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                  *(dp + dpL + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                  *(dp + dpL + dpL + 3) = Mix2To1To1(w[5], w[4], w[7]);
                  *(dp + dpL + dpL + dpL) = Mix3To1(w[4], w[7]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[7], w[5]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                break;
              }
            case 236:
            case 232:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[0]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[5]);
                *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[1]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + 1) = w[4];
                  *(dp + dpL + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL + 1) = w[4];
                  *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                  *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                }
                else
                {
                  *(dp + dpL + dpL) = Mix2To1To1(w[3], w[4], w[7]);
                  *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = Mix3To1(w[4], w[7]);
                }
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 109:
            case 105:
              {
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp) = Mix5To3(w[4], w[1]);
                  *(dp + dpL) = Mix7To1(w[4], w[1]);
                  *(dp + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + 1) = w[4];
                  *(dp + dpL + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL + 1) = w[4];
                }
                else
                {
                  *(dp) = Mix3To1(w[4], w[3]);
                  *(dp + dpL) = Mix3To1(w[3], w[4]);
                  *(dp + dpL + dpL) = Mix5To3(w[3], w[7]);
                  *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = Mix2To1To1(w[7], w[4], w[3]);
                }
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[5]);
                *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[1]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[8]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 171:
            case 43:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                  *(dp + 1) = w[4];
                  *(dp + dpL) = w[4];
                  *(dp + dpL + 1) = w[4];
                  *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                  *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = Mix2To1To1(w[1], w[4], w[3]);
                  *(dp + dpL) = Mix5To3(w[3], w[1]);
                  *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                  *(dp + dpL + dpL) = Mix3To1(w[3], w[4]);
                  *(dp + dpL + dpL + dpL) = Mix3To1(w[4], w[3]);
                }
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[2]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                break;
              }
            case 143:
            case 15:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                  *(dp + 1) = w[4];
                  *(dp + 2) = Mix7To1(w[4], w[5]);
                  *(dp + 3) = Mix5To3(w[4], w[5]);
                  *(dp + dpL) = w[4];
                  *(dp + dpL + 1) = w[4];
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = Mix5To3(w[1], w[3]);
                  *(dp + 2) = Mix3To1(w[1], w[4]);
                  *(dp + 3) = Mix3To1(w[4], w[1]);
                  *(dp + dpL) = Mix2To1To1(w[3], w[4], w[1]);
                  *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                }
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                break;
              }
            case 124:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[0]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                *(dp + 3) = Mix5To3(w[4], w[1]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL + 1) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL) = MixEven(w[3], w[4]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = MixEven(w[7], w[4]);
                }
                *(dp + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 203:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                  *(dp + 1) = w[4];
                  *(dp + dpL) = w[4];
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = MixEven(w[1], w[4]);
                  *(dp + dpL) = MixEven(w[3], w[4]);
                }
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[2]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 62:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = w[4];
                  *(dp + 3) = w[4];
                  *(dp + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + 2) = MixEven(w[1], w[4]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 3) = MixEven(w[5], w[4]);
                }
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = w[4];
                *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 211:
              {
                *(dp) = Mix5To3(w[4], w[3]);
                *(dp + 1) = Mix7To1(w[4], w[3]);
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 3) = w[4];
                  *(dp + dpL + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + 3) = MixEven(w[5], w[4]);
                  *(dp + dpL + dpL + dpL + 2) = MixEven(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                break;
              }
            case 118:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = w[4];
                  *(dp + 3) = w[4];
                  *(dp + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + 2) = MixEven(w[1], w[4]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 3) = MixEven(w[5], w[4]);
                }
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = w[4];
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 217:
              {
                *(dp) = Mix5To3(w[4], w[1]);
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 3) = w[4];
                  *(dp + dpL + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + 3) = MixEven(w[5], w[4]);
                  *(dp + dpL + dpL + dpL + 2) = MixEven(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                break;
              }
            case 110:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                *(dp + 2) = Mix7To1(w[4], w[5]);
                *(dp + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL + 1) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL) = MixEven(w[3], w[4]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = MixEven(w[7], w[4]);
                }
                *(dp + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[8]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 155:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                  *(dp + 1) = w[4];
                  *(dp + dpL) = w[4];
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = MixEven(w[1], w[4]);
                  *(dp + dpL) = MixEven(w[3], w[4]);
                }
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                break;
              }
            case 188:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[0]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                *(dp + 3) = Mix5To3(w[4], w[1]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                break;
              }
            case 185:
              {
                *(dp) = Mix5To3(w[4], w[1]);
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                break;
              }
            case 61:
              {
                *(dp) = Mix5To3(w[4], w[1]);
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                *(dp + 3) = Mix5To3(w[4], w[1]);
                *(dp + dpL) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 157:
              {
                *(dp) = Mix5To3(w[4], w[1]);
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                *(dp + 3) = Mix5To3(w[4], w[1]);
                *(dp + dpL) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                break;
              }
            case 103:
              {
                *(dp) = Mix5To3(w[4], w[3]);
                *(dp + 1) = Mix7To1(w[4], w[3]);
                *(dp + 2) = Mix7To1(w[4], w[5]);
                *(dp + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 227:
              {
                *(dp) = Mix5To3(w[4], w[3]);
                *(dp + 1) = Mix7To1(w[4], w[3]);
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[2]);
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 230:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                *(dp + 2) = Mix7To1(w[4], w[5]);
                *(dp + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 199:
              {
                *(dp) = Mix5To3(w[4], w[3]);
                *(dp + 1) = Mix7To1(w[4], w[3]);
                *(dp + 2) = Mix7To1(w[4], w[5]);
                *(dp + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 220:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[0]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                *(dp + 3) = Mix5To3(w[4], w[1]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                  *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                  *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                }
                else
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + dpL + 1) = w[4];
                  *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[7]);
                }
                *(dp + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 3) = w[4];
                  *(dp + dpL + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + 3) = MixEven(w[5], w[4]);
                  *(dp + dpL + dpL + dpL + 2) = MixEven(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                break;
              }
            case 158:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *(dp) = Mix5To3(w[4], w[0]);
                  *(dp + 1) = Mix3To1(w[4], w[0]);
                  *(dp + dpL) = Mix3To1(w[4], w[0]);
                  *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                }
                else
                {
                  *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                  *(dp + 1) = Mix3To1(w[4], w[1]);
                  *(dp + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + 1) = w[4];
                }
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = w[4];
                  *(dp + 3) = w[4];
                  *(dp + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + 2) = MixEven(w[1], w[4]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 3) = MixEven(w[5], w[4]);
                }
                *(dp + dpL + 2) = w[4];
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                break;
              }
            case 234:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *(dp) = Mix5To3(w[4], w[0]);
                  *(dp + 1) = Mix3To1(w[4], w[0]);
                  *(dp + dpL) = Mix3To1(w[4], w[0]);
                  *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                }
                else
                {
                  *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                  *(dp + 1) = Mix3To1(w[4], w[1]);
                  *(dp + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + 1) = w[4];
                }
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[2]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL + 1) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL) = MixEven(w[3], w[4]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = MixEven(w[7], w[4]);
                }
                *(dp + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 242:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = Mix3To1(w[4], w[2]);
                  *(dp + 3) = Mix5To3(w[4], w[2]);
                  *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                  *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                }
                else
                {
                  *(dp + 2) = Mix3To1(w[4], w[1]);
                  *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                  *(dp + dpL + 2) = w[4];
                  *(dp + dpL + 3) = Mix3To1(w[4], w[5]);
                }
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 3) = w[4];
                  *(dp + dpL + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + 3) = MixEven(w[5], w[4]);
                  *(dp + dpL + dpL + dpL + 2) = MixEven(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                break;
              }
            case 59:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                  *(dp + 1) = w[4];
                  *(dp + dpL) = w[4];
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = MixEven(w[1], w[4]);
                  *(dp + dpL) = MixEven(w[3], w[4]);
                }
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = Mix3To1(w[4], w[2]);
                  *(dp + 3) = Mix5To3(w[4], w[2]);
                  *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                  *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                }
                else
                {
                  *(dp + 2) = Mix3To1(w[4], w[1]);
                  *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                  *(dp + dpL + 2) = w[4];
                  *(dp + dpL + 3) = Mix3To1(w[4], w[5]);
                }
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 121:
              {
                *(dp) = Mix5To3(w[4], w[1]);
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL + 1) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL) = MixEven(w[3], w[4]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = MixEven(w[7], w[4]);
                }
                *(dp + dpL + dpL + 1) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                }
                else
                {
                  *(dp + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[5]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[7]);
                  *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                }
                break;
              }
            case 87:
              {
                *(dp) = Mix5To3(w[4], w[3]);
                *(dp + 1) = Mix7To1(w[4], w[3]);
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = w[4];
                  *(dp + 3) = w[4];
                  *(dp + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + 2) = MixEven(w[1], w[4]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 3) = MixEven(w[5], w[4]);
                }
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + 2) = w[4];
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                }
                else
                {
                  *(dp + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[5]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[7]);
                  *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                }
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                break;
              }
            case 79:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                  *(dp + 1) = w[4];
                  *(dp + dpL) = w[4];
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = MixEven(w[1], w[4]);
                  *(dp + dpL) = MixEven(w[3], w[4]);
                }
                *(dp + 2) = Mix7To1(w[4], w[5]);
                *(dp + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                  *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                  *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                }
                else
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + dpL + 1) = w[4];
                  *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[7]);
                }
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[8]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 122:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *(dp) = Mix5To3(w[4], w[0]);
                  *(dp + 1) = Mix3To1(w[4], w[0]);
                  *(dp + dpL) = Mix3To1(w[4], w[0]);
                  *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                }
                else
                {
                  *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                  *(dp + 1) = Mix3To1(w[4], w[1]);
                  *(dp + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + 1) = w[4];
                }
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = Mix3To1(w[4], w[2]);
                  *(dp + 3) = Mix5To3(w[4], w[2]);
                  *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                  *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                }
                else
                {
                  *(dp + 2) = Mix3To1(w[4], w[1]);
                  *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                  *(dp + dpL + 2) = w[4];
                  *(dp + dpL + 3) = Mix3To1(w[4], w[5]);
                }
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL + 1) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL) = MixEven(w[3], w[4]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = MixEven(w[7], w[4]);
                }
                *(dp + dpL + dpL + 1) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                }
                else
                {
                  *(dp + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[5]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[7]);
                  *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                }
                break;
              }
            case 94:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *(dp) = Mix5To3(w[4], w[0]);
                  *(dp + 1) = Mix3To1(w[4], w[0]);
                  *(dp + dpL) = Mix3To1(w[4], w[0]);
                  *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                }
                else
                {
                  *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                  *(dp + 1) = Mix3To1(w[4], w[1]);
                  *(dp + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + 1) = w[4];
                }
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = w[4];
                  *(dp + 3) = w[4];
                  *(dp + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + 2) = MixEven(w[1], w[4]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 3) = MixEven(w[5], w[4]);
                }
                *(dp + dpL + 2) = w[4];
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                  *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                  *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                }
                else
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + dpL + 1) = w[4];
                  *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[7]);
                }
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                }
                else
                {
                  *(dp + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[5]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[7]);
                  *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                }
                break;
              }
            case 218:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *(dp) = Mix5To3(w[4], w[0]);
                  *(dp + 1) = Mix3To1(w[4], w[0]);
                  *(dp + dpL) = Mix3To1(w[4], w[0]);
                  *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                }
                else
                {
                  *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                  *(dp + 1) = Mix3To1(w[4], w[1]);
                  *(dp + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + 1) = w[4];
                }
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = Mix3To1(w[4], w[2]);
                  *(dp + 3) = Mix5To3(w[4], w[2]);
                  *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                  *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                }
                else
                {
                  *(dp + 2) = Mix3To1(w[4], w[1]);
                  *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                  *(dp + dpL + 2) = w[4];
                  *(dp + dpL + 3) = Mix3To1(w[4], w[5]);
                }
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                  *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                  *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                }
                else
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + dpL + 1) = w[4];
                  *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[7]);
                }
                *(dp + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 3) = w[4];
                  *(dp + dpL + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + 3) = MixEven(w[5], w[4]);
                  *(dp + dpL + dpL + dpL + 2) = MixEven(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                break;
              }
            case 91:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                  *(dp + 1) = w[4];
                  *(dp + dpL) = w[4];
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = MixEven(w[1], w[4]);
                  *(dp + dpL) = MixEven(w[3], w[4]);
                }
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = Mix3To1(w[4], w[2]);
                  *(dp + 3) = Mix5To3(w[4], w[2]);
                  *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                  *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                }
                else
                {
                  *(dp + 2) = Mix3To1(w[4], w[1]);
                  *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                  *(dp + dpL + 2) = w[4];
                  *(dp + dpL + 3) = Mix3To1(w[4], w[5]);
                }
                *(dp + dpL + 1) = w[4];
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                  *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                  *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                }
                else
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + dpL + 1) = w[4];
                  *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[7]);
                }
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                }
                else
                {
                  *(dp + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[5]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[7]);
                  *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                }
                break;
              }
            case 229:
              {
                *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[3]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[5]);
                *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[1]);
                *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[1]);
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 167:
              {
                *(dp) = Mix5To3(w[4], w[3]);
                *(dp + 1) = Mix7To1(w[4], w[3]);
                *(dp + 2) = Mix7To1(w[4], w[5]);
                *(dp + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                break;
              }
            case 173:
              {
                *(dp) = Mix5To3(w[4], w[1]);
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[5]);
                *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                *(dp + dpL) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[1]);
                *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                break;
              }
            case 181:
              {
                *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[3]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                *(dp + 3) = Mix5To3(w[4], w[1]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[1]);
                *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                break;
              }
            case 186:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *(dp) = Mix5To3(w[4], w[0]);
                  *(dp + 1) = Mix3To1(w[4], w[0]);
                  *(dp + dpL) = Mix3To1(w[4], w[0]);
                  *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                }
                else
                {
                  *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                  *(dp + 1) = Mix3To1(w[4], w[1]);
                  *(dp + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + 1) = w[4];
                }
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = Mix3To1(w[4], w[2]);
                  *(dp + 3) = Mix5To3(w[4], w[2]);
                  *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                  *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                }
                else
                {
                  *(dp + 2) = Mix3To1(w[4], w[1]);
                  *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                  *(dp + dpL + 2) = w[4];
                  *(dp + dpL + 3) = Mix3To1(w[4], w[5]);
                }
                *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                break;
              }
            case 115:
              {
                *(dp) = Mix5To3(w[4], w[3]);
                *(dp + 1) = Mix7To1(w[4], w[3]);
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = Mix3To1(w[4], w[2]);
                  *(dp + 3) = Mix5To3(w[4], w[2]);
                  *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                  *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                }
                else
                {
                  *(dp + 2) = Mix3To1(w[4], w[1]);
                  *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                  *(dp + dpL + 2) = w[4];
                  *(dp + dpL + 3) = Mix3To1(w[4], w[5]);
                }
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                }
                else
                {
                  *(dp + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[5]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[7]);
                  *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                }
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                break;
              }
            case 93:
              {
                *(dp) = Mix5To3(w[4], w[1]);
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                *(dp + 3) = Mix5To3(w[4], w[1]);
                *(dp + dpL) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                  *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                  *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                }
                else
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + dpL + 1) = w[4];
                  *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[7]);
                }
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                }
                else
                {
                  *(dp + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[5]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[7]);
                  *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                }
                break;
              }
            case 206:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *(dp) = Mix5To3(w[4], w[0]);
                  *(dp + 1) = Mix3To1(w[4], w[0]);
                  *(dp + dpL) = Mix3To1(w[4], w[0]);
                  *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                }
                else
                {
                  *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                  *(dp + 1) = Mix3To1(w[4], w[1]);
                  *(dp + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + 1) = w[4];
                }
                *(dp + 2) = Mix7To1(w[4], w[5]);
                *(dp + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                  *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                  *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                }
                else
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + dpL + 1) = w[4];
                  *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[7]);
                }
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 205:
            case 201:
              {
                *(dp) = Mix5To3(w[4], w[1]);
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[5]);
                *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                *(dp + dpL) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[1]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                  *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                  *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                }
                else
                {
                  *(dp + dpL + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + dpL + 1) = w[4];
                  *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[7]);
                }
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 174:
            case 46:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *(dp) = Mix5To3(w[4], w[0]);
                  *(dp + 1) = Mix3To1(w[4], w[0]);
                  *(dp + dpL) = Mix3To1(w[4], w[0]);
                  *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                }
                else
                {
                  *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                  *(dp + 1) = Mix3To1(w[4], w[1]);
                  *(dp + dpL) = Mix3To1(w[4], w[3]);
                  *(dp + dpL + 1) = w[4];
                }
                *(dp + 2) = Mix7To1(w[4], w[5]);
                *(dp + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                break;
              }
            case 179:
            case 147:
              {
                *(dp) = Mix5To3(w[4], w[3]);
                *(dp + 1) = Mix7To1(w[4], w[3]);
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = Mix3To1(w[4], w[2]);
                  *(dp + 3) = Mix5To3(w[4], w[2]);
                  *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                  *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                }
                else
                {
                  *(dp + 2) = Mix3To1(w[4], w[1]);
                  *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                  *(dp + dpL + 2) = w[4];
                  *(dp + dpL + 3) = Mix3To1(w[4], w[5]);
                }
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                break;
              }
            case 117:
            case 116:
              {
                *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[3]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                *(dp + 3) = Mix5To3(w[4], w[1]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[1]);
                *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                  *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                }
                else
                {
                  *(dp + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[5]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[7]);
                  *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                }
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                break;
              }
            case 189:
              {
                *(dp) = Mix5To3(w[4], w[1]);
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                *(dp + 3) = Mix5To3(w[4], w[1]);
                *(dp + dpL) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                break;
              }
            case 231:
              {
                *(dp) = Mix5To3(w[4], w[3]);
                *(dp + 1) = Mix7To1(w[4], w[3]);
                *(dp + 2) = Mix7To1(w[4], w[5]);
                *(dp + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 126:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = w[4];
                  *(dp + 3) = w[4];
                  *(dp + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + 2) = MixEven(w[1], w[4]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 3) = MixEven(w[5], w[4]);
                }
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = w[4];
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL + 1) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL) = MixEven(w[3], w[4]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = MixEven(w[7], w[4]);
                }
                *(dp + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 219:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                  *(dp + 1) = w[4];
                  *(dp + dpL) = w[4];
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = MixEven(w[1], w[4]);
                  *(dp + dpL) = MixEven(w[3], w[4]);
                }
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 3) = w[4];
                  *(dp + dpL + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + 3) = MixEven(w[5], w[4]);
                  *(dp + dpL + dpL + dpL + 2) = MixEven(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                break;
              }
            case 125:
              {
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp) = Mix5To3(w[4], w[1]);
                  *(dp + dpL) = Mix7To1(w[4], w[1]);
                  *(dp + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + 1) = w[4];
                  *(dp + dpL + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL + 1) = w[4];
                }
                else
                {
                  *(dp) = Mix3To1(w[4], w[3]);
                  *(dp + dpL) = Mix3To1(w[3], w[4]);
                  *(dp + dpL + dpL) = Mix5To3(w[3], w[7]);
                  *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = Mix2To1To1(w[7], w[4], w[3]);
                }
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                *(dp + 3) = Mix5To3(w[4], w[1]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 221:
              {
                *(dp) = Mix5To3(w[4], w[1]);
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + 3) = Mix5To3(w[4], w[1]);
                  *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                  *(dp + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + 3) = w[4];
                  *(dp + dpL + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + 3) = Mix3To1(w[4], w[5]);
                  *(dp + dpL + 3) = Mix3To1(w[5], w[4]);
                  *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                  *(dp + dpL + dpL + 3) = Mix5To3(w[5], w[7]);
                  *(dp + dpL + dpL + dpL + 2) = Mix2To1To1(w[7], w[4], w[5]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                *(dp + dpL) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                break;
              }
            case 207:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                  *(dp + 1) = w[4];
                  *(dp + 2) = Mix7To1(w[4], w[5]);
                  *(dp + 3) = Mix5To3(w[4], w[5]);
                  *(dp + dpL) = w[4];
                  *(dp + dpL + 1) = w[4];
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = Mix5To3(w[1], w[3]);
                  *(dp + 2) = Mix3To1(w[1], w[4]);
                  *(dp + 3) = Mix3To1(w[4], w[1]);
                  *(dp + dpL) = Mix2To1To1(w[3], w[4], w[1]);
                  *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                }
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 238:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                *(dp + 2) = Mix7To1(w[4], w[5]);
                *(dp + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + 1) = w[4];
                  *(dp + dpL + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL + 1) = w[4];
                  *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                  *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                }
                else
                {
                  *(dp + dpL + dpL) = Mix2To1To1(w[3], w[4], w[7]);
                  *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = Mix3To1(w[4], w[7]);
                }
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 190:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = w[4];
                  *(dp + 3) = w[4];
                  *(dp + dpL + 2) = w[4];
                  *(dp + dpL + 3) = w[4];
                  *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                  *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                }
                else
                {
                  *(dp + 2) = Mix2To1To1(w[1], w[4], w[5]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                  *(dp + dpL + 3) = Mix5To3(w[5], w[1]);
                  *(dp + dpL + dpL + 3) = Mix3To1(w[5], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = Mix3To1(w[4], w[5]);
                }
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                break;
              }
            case 187:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                  *(dp + 1) = w[4];
                  *(dp + dpL) = w[4];
                  *(dp + dpL + 1) = w[4];
                  *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                  *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = Mix2To1To1(w[1], w[4], w[3]);
                  *(dp + dpL) = Mix5To3(w[3], w[1]);
                  *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                  *(dp + dpL + dpL) = Mix3To1(w[3], w[4]);
                  *(dp + dpL + dpL + dpL) = Mix3To1(w[4], w[3]);
                }
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                break;
              }
            case 243:
              {
                *(dp) = Mix5To3(w[4], w[3]);
                *(dp + 1) = Mix7To1(w[4], w[3]);
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + 3) = w[4];
                  *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                  *(dp + dpL + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                  *(dp + dpL + dpL + 3) = Mix2To1To1(w[5], w[4], w[7]);
                  *(dp + dpL + dpL + dpL) = Mix3To1(w[4], w[7]);
                  *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[7], w[5]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                break;
              }
            case 119:
              {
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp) = Mix5To3(w[4], w[3]);
                  *(dp + 1) = Mix7To1(w[4], w[3]);
                  *(dp + 2) = w[4];
                  *(dp + 3) = w[4];
                  *(dp + dpL + 2) = w[4];
                  *(dp + dpL + 3) = w[4];
                }
                else
                {
                  *(dp) = Mix3To1(w[4], w[1]);
                  *(dp + 1) = Mix3To1(w[1], w[4]);
                  *(dp + 2) = Mix5To3(w[1], w[5]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                  *(dp + dpL + 3) = Mix2To1To1(w[5], w[4], w[1]);
                }
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 237:
            case 233:
              {
                *(dp) = Mix5To3(w[4], w[1]);
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[5]);
                *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                *(dp + dpL) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix6To1To1(w[4], w[5], w[1]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[1]);
                *(dp + dpL + dpL) = w[4];
                *(dp + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + dpL) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                }
                *(dp + dpL + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 175:
            case 47:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                }
                else
                {
                  *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                }
                *(dp + 1) = w[4];
                *(dp + 2) = Mix7To1(w[4], w[5]);
                *(dp + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL) = w[4];
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix6To1To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                break;
              }
            case 183:
            case 151:
              {
                *(dp) = Mix5To3(w[4], w[3]);
                *(dp + 1) = Mix7To1(w[4], w[3]);
                *(dp + 2) = w[4];
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 3) = w[4];
                }
                else
                {
                  *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                }
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + 2) = w[4];
                *(dp + dpL + 3) = w[4];
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 1) = Mix6To1To1(w[4], w[3], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[3]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                break;
              }
            case 245:
            case 244:
              {
                *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[3]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                *(dp + 3) = Mix5To3(w[4], w[1]);
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[1]);
                *(dp + dpL + 1) = Mix6To1To1(w[4], w[3], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + 2) = w[4];
                *(dp + dpL + dpL + 3) = w[4];
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                }
                break;
              }
            case 250:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL + 1) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL) = MixEven(w[3], w[4]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = MixEven(w[7], w[4]);
                }
                *(dp + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 3) = w[4];
                  *(dp + dpL + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + 3) = MixEven(w[5], w[4]);
                  *(dp + dpL + dpL + dpL + 2) = MixEven(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                break;
              }
            case 123:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                  *(dp + 1) = w[4];
                  *(dp + dpL) = w[4];
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = MixEven(w[1], w[4]);
                  *(dp + dpL) = MixEven(w[3], w[4]);
                }
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL + 1) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL) = MixEven(w[3], w[4]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = MixEven(w[7], w[4]);
                }
                *(dp + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 95:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                  *(dp + 1) = w[4];
                  *(dp + dpL) = w[4];
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = MixEven(w[1], w[4]);
                  *(dp + dpL) = MixEven(w[3], w[4]);
                }
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = w[4];
                  *(dp + 3) = w[4];
                  *(dp + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + 2) = MixEven(w[1], w[4]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 3) = MixEven(w[5], w[4]);
                }
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + 2) = w[4];
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 222:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = w[4];
                  *(dp + 3) = w[4];
                  *(dp + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + 2) = MixEven(w[1], w[4]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 3) = MixEven(w[5], w[4]);
                }
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = w[4];
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 3) = w[4];
                  *(dp + dpL + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + 3) = MixEven(w[5], w[4]);
                  *(dp + dpL + dpL + dpL + 2) = MixEven(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                break;
              }
            case 252:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix5To2To1(w[4], w[1], w[0]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                *(dp + 3) = Mix5To3(w[4], w[1]);
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL + 1) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL) = MixEven(w[3], w[4]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = MixEven(w[7], w[4]);
                }
                *(dp + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + 2) = w[4];
                *(dp + dpL + dpL + 3) = w[4];
                *(dp + dpL + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                }
                break;
              }
            case 249:
              {
                *(dp) = Mix5To3(w[4], w[1]);
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To2To1(w[4], w[1], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = w[4];
                *(dp + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 3) = w[4];
                  *(dp + dpL + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + 3) = MixEven(w[5], w[4]);
                  *(dp + dpL + dpL + dpL + 2) = MixEven(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + dpL) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                }
                *(dp + dpL + dpL + dpL + 1) = w[4];
                break;
              }
            case 235:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                  *(dp + 1) = w[4];
                  *(dp + dpL) = w[4];
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = MixEven(w[1], w[4]);
                  *(dp + dpL) = MixEven(w[3], w[4]);
                }
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix5To2To1(w[4], w[5], w[2]);
                *(dp + dpL + dpL) = w[4];
                *(dp + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + dpL) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                }
                *(dp + dpL + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 111:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                }
                else
                {
                  *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                }
                *(dp + 1) = w[4];
                *(dp + 2) = Mix7To1(w[4], w[5]);
                *(dp + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL) = w[4];
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL + 1) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL) = MixEven(w[3], w[4]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = MixEven(w[7], w[4]);
                }
                *(dp + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix5To2To1(w[4], w[5], w[8]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 63:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                }
                else
                {
                  *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                }
                *(dp + 1) = w[4];
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = w[4];
                  *(dp + 3) = w[4];
                  *(dp + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + 2) = MixEven(w[1], w[4]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 3) = MixEven(w[5], w[4]);
                }
                *(dp + dpL) = w[4];
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + 2) = w[4];
                *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To2To1(w[4], w[7], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 159:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                  *(dp + 1) = w[4];
                  *(dp + dpL) = w[4];
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = MixEven(w[1], w[4]);
                  *(dp + dpL) = MixEven(w[3], w[4]);
                }
                *(dp + 2) = w[4];
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 3) = w[4];
                }
                else
                {
                  *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                }
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + 2) = w[4];
                *(dp + dpL + 3) = w[4];
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To2To1(w[4], w[7], w[6]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                break;
              }
            case 215:
              {
                *(dp) = Mix5To3(w[4], w[3]);
                *(dp + 1) = Mix7To1(w[4], w[3]);
                *(dp + 2) = w[4];
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 3) = w[4];
                }
                else
                {
                  *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                }
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + 2) = w[4];
                *(dp + dpL + 3) = w[4];
                *(dp + dpL + dpL) = Mix5To2To1(w[4], w[3], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 3) = w[4];
                  *(dp + dpL + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + 3) = MixEven(w[5], w[4]);
                  *(dp + dpL + dpL + dpL + 2) = MixEven(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                break;
              }
            case 246:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = w[4];
                  *(dp + 3) = w[4];
                  *(dp + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + 2) = MixEven(w[1], w[4]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 3) = MixEven(w[5], w[4]);
                }
                *(dp + dpL) = Mix5To2To1(w[4], w[3], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = w[4];
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + 2) = w[4];
                *(dp + dpL + dpL + 3) = w[4];
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                }
                break;
              }
            case 254:
              {
                *(dp) = Mix5To3(w[4], w[0]);
                *(dp + 1) = Mix3To1(w[4], w[0]);
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = w[4];
                  *(dp + 3) = w[4];
                  *(dp + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + 2) = MixEven(w[1], w[4]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 3) = MixEven(w[5], w[4]);
                }
                *(dp + dpL) = Mix3To1(w[4], w[0]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[0]);
                *(dp + dpL + 2) = w[4];
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL + 1) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL) = MixEven(w[3], w[4]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = MixEven(w[7], w[4]);
                }
                *(dp + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + 2) = w[4];
                *(dp + dpL + dpL + 3) = w[4];
                *(dp + dpL + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                }
                break;
              }
            case 253:
              {
                *(dp) = Mix5To3(w[4], w[1]);
                *(dp + 1) = Mix5To3(w[4], w[1]);
                *(dp + 2) = Mix5To3(w[4], w[1]);
                *(dp + 3) = Mix5To3(w[4], w[1]);
                *(dp + dpL) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 2) = Mix7To1(w[4], w[1]);
                *(dp + dpL + 3) = Mix7To1(w[4], w[1]);
                *(dp + dpL + dpL) = w[4];
                *(dp + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + 2) = w[4];
                *(dp + dpL + dpL + 3) = w[4];
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + dpL) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                }
                *(dp + dpL + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                }
                break;
              }
            case 251:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                  *(dp + 1) = w[4];
                  *(dp + dpL) = w[4];
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = MixEven(w[1], w[4]);
                  *(dp + dpL) = MixEven(w[3], w[4]);
                }
                *(dp + 2) = Mix3To1(w[4], w[2]);
                *(dp + 3) = Mix5To3(w[4], w[2]);
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + 2) = Mix7To1(w[4], w[2]);
                *(dp + dpL + 3) = Mix3To1(w[4], w[2]);
                *(dp + dpL + dpL) = w[4];
                *(dp + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 3) = w[4];
                  *(dp + dpL + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + 3) = MixEven(w[5], w[4]);
                  *(dp + dpL + dpL + dpL + 2) = MixEven(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + dpL) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                }
                *(dp + dpL + dpL + dpL + 1) = w[4];
                break;
              }
            case 239:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                }
                else
                {
                  *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                }
                *(dp + 1) = w[4];
                *(dp + 2) = Mix7To1(w[4], w[5]);
                *(dp + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL) = w[4];
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + 3) = Mix5To3(w[4], w[5]);
                *(dp + dpL + dpL) = w[4];
                *(dp + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + dpL) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                }
                *(dp + dpL + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + dpL + 2) = Mix7To1(w[4], w[5]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[5]);
                break;
              }
            case 127:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                }
                else
                {
                  *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                }
                *(dp + 1) = w[4];
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 2) = w[4];
                  *(dp + 3) = w[4];
                  *(dp + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + 2) = MixEven(w[1], w[4]);
                  *(dp + 3) = MixEven(w[1], w[5]);
                  *(dp + dpL + 3) = MixEven(w[5], w[4]);
                }
                *(dp + dpL) = w[4];
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + 2) = w[4];
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL) = w[4];
                  *(dp + dpL + dpL + dpL + 1) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL) = MixEven(w[3], w[4]);
                  *(dp + dpL + dpL + dpL) = MixEven(w[7], w[3]);
                  *(dp + dpL + dpL + dpL + 1) = MixEven(w[7], w[4]);
                }
                *(dp + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[8]);
                *(dp + dpL + dpL + 3) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 2) = Mix3To1(w[4], w[8]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[8]);
                break;
              }
            case 191:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                }
                else
                {
                  *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                }
                *(dp + 1) = w[4];
                *(dp + 2) = w[4];
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 3) = w[4];
                }
                else
                {
                  *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                }
                *(dp + dpL) = w[4];
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + 2) = w[4];
                *(dp + dpL + 3) = w[4];
                *(dp + dpL + dpL) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 2) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + 3) = Mix7To1(w[4], w[7]);
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 1) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 2) = Mix5To3(w[4], w[7]);
                *(dp + dpL + dpL + dpL + 3) = Mix5To3(w[4], w[7]);
                break;
              }
            case 223:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                  *(dp + 1) = w[4];
                  *(dp + dpL) = w[4];
                }
                else
                {
                  *(dp) = MixEven(w[1], w[3]);
                  *(dp + 1) = MixEven(w[1], w[4]);
                  *(dp + dpL) = MixEven(w[3], w[4]);
                }
                *(dp + 2) = w[4];
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 3) = w[4];
                }
                else
                {
                  *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                }
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + 2) = w[4];
                *(dp + dpL + 3) = w[4];
                *(dp + dpL + dpL) = Mix3To1(w[4], w[6]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[6]);
                *(dp + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + 3) = w[4];
                  *(dp + dpL + dpL + dpL + 2) = w[4];
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + 3) = MixEven(w[5], w[4]);
                  *(dp + dpL + dpL + dpL + 2) = MixEven(w[7], w[4]);
                  *(dp + dpL + dpL + dpL + 3) = MixEven(w[7], w[5]);
                }
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[6]);
                *(dp + dpL + dpL + dpL + 1) = Mix3To1(w[4], w[6]);
                break;
              }
            case 247:
              {
                *(dp) = Mix5To3(w[4], w[3]);
                *(dp + 1) = Mix7To1(w[4], w[3]);
                *(dp + 2) = w[4];
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 3) = w[4];
                }
                else
                {
                  *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                }
                *(dp + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + 2) = w[4];
                *(dp + dpL + 3) = w[4];
                *(dp + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + 2) = w[4];
                *(dp + dpL + dpL + 3) = w[4];
                *(dp + dpL + dpL + dpL) = Mix5To3(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 1) = Mix7To1(w[4], w[3]);
                *(dp + dpL + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                }
                break;
              }
            case 255:
              {
                if (Diff(w[3], w[1], trY, trU, trV, trA))
                {
                  *dp = w[4];
                }
                else
                {
                  *(dp) = Mix2To1To1(w[4], w[1], w[3]);
                }
                *(dp + 1) = w[4];
                *(dp + 2) = w[4];
                if (Diff(w[1], w[5], trY, trU, trV, trA))
                {
                  *(dp + 3) = w[4];
                }
                else
                {
                  *(dp + 3) = Mix2To1To1(w[4], w[1], w[5]);
                }
                *(dp + dpL) = w[4];
                *(dp + dpL + 1) = w[4];
                *(dp + dpL + 2) = w[4];
                *(dp + dpL + 3) = w[4];
                *(dp + dpL + dpL) = w[4];
                *(dp + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + 2) = w[4];
                *(dp + dpL + dpL + 3) = w[4];
                if (Diff(w[7], w[3], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + dpL) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + dpL) = Mix2To1To1(w[4], w[7], w[3]);
                }
                *(dp + dpL + dpL + dpL + 1) = w[4];
                *(dp + dpL + dpL + dpL + 2) = w[4];
                if (Diff(w[5], w[7], trY, trU, trV, trA))
                {
                  *(dp + dpL + dpL + dpL + 3) = w[4];
                }
                else
                {
                  *(dp + dpL + dpL + dpL + 3) = Mix2To1To1(w[4], w[7], w[5]);
                }
                break;
              }
          }
          sp++;
          dp += 4;
        }
        dp += (dpL * 3);
      }
    }
  }
}

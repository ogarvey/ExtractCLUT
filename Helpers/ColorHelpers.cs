using System.Drawing;
using System.Drawing.Imaging;
using SixLabors.ImageSharp.PixelFormats;
using Color = System.Drawing.Color;
using SizeF = System.Drawing.SizeF;

namespace ExtractCLUT.Helpers
{
  public static class ColorHelper
  {
    public static List<Color> GenerateColors(int count)
    {
      List<Color> colors = new List<Color>();

      // Generate spectrum colors
      for (int i = 0; i < count - count / 2; i++)
      {
        float hue = (i / (float)(count - count / 2)) * 360;  // Hue varies from 0 to 360
        colors.Add(ColorFromHSV(hue, (float)1.0, (float)1.0)); // Saturation and lightness are constant
      }

      // Generate grayscale colors
      for (int i = 0; i < count / 2; i++)
      {
        int grayValue = (int)((i / (float)(count / 2)) * 255);
        colors.Add(Color.FromArgb(grayValue, grayValue, grayValue));
      }

      return colors;
    }

    public static List<Color> ReadBgr15Palette(byte[] bytes)
    {
      List<Color> colors = new List<Color>();

      for (int i = 0; i < bytes.Length - 1; i += 2)
      {
        ushort color = BitConverter.ToUInt16(bytes.Skip(i).Take(2).Reverse().ToArray(), 0);
        byte red = (byte)((color >> 10) & 0x1F);
        byte green = (byte)((color >> 5) & 0x1F);
        byte blue = (byte)(color & 0x1F);

        red = (byte)((red << 3) | (red >> 2));
        green = (byte)((green << 3) | (green >> 2));
        blue = (byte)((blue << 3) | (blue >> 2));

        Color rgbColor = Color.FromArgb(blue, green, red);
        colors.Add(rgbColor);
      }

      return colors;
    }
    public static ushort RgbToA1B5G5R5(int r, int g, int b)
    {
      // Validate RGB values
      if (r < 0 || r > 255 || g < 0 || g > 255 || b < 0 || b > 255)
      {
        throw new ArgumentException("RGB color values must be integers between 0 and 255.");
      }

      // Convert RGB to 5-bit values
      ushort r5 = (ushort)(r >> 3);
      ushort g5 = (ushort)(g >> 3);
      ushort b5 = (ushort)(b >> 3);

      // Construct the A1B5G5R5 color value
      ushort a1b5g5r5 = (ushort)(0x8000 | (b5 << 10) | (g5 << 5) | r5); // 0x8000 sets the alpha bit to 1 (opaque)

      return a1b5g5r5;
    }
    public static List<Color> ReadABgr15Palette(byte[] bytes, bool translucent = false)
    {
      List<Color> colors = new List<Color>();

      for (int i = 0; i < bytes.Length - 1; i += 2)
      {
        ushort color = BitConverter.ToUInt16(bytes.Skip(i).Take(2).ToArray(), 0);
        bool alpha = ((color >> 15) & 0x1) > 0;  // 1 bit for Alpha
        byte red = (byte)((color >> 10) & 0x1F);
        byte green = (byte)((color >> 5) & 0x1F);
        byte blue = (byte)(color & 0x1F);

        red = (byte)((red << 3) | (red >> 2));
        green = (byte)((green << 3) | (green >> 2));
        blue = (byte)((blue << 3) | (blue >> 2));
        Color rgbColor;
        if (alpha)
        {
          if (red == 0 && green == 0 && blue == 0)
          {
            rgbColor = Color.Black;
          }
          else
          {
            rgbColor = Color.FromArgb(translucent ? 128 : 255, blue, green, red);
          }
        }
        else
        {
          if (red == 0 && green == 0 && blue == 0)
          {
            rgbColor = Color.Transparent;
          }
          else
          {
            rgbColor = Color.FromArgb(255, blue, green, red);
          }
        }

        colors.Add(rgbColor);
      }

      return colors;
    }
    // Convert HSV color to RGB color
    static Color ColorFromHSV(float hue, float saturation, float value)
    {
      int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
      float f = (float)(hue / 60 - Math.Floor(hue / 60));

      value = value * 255;
      int v = Convert.ToInt32(value);
      int p = Convert.ToInt32(value * (1 - saturation));
      int q = Convert.ToInt32(value * (1 - f * saturation));
      int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

      if (hi == 0)
        return Color.FromArgb(255, v, t, p);
      else if (hi == 1)
        return Color.FromArgb(255, q, v, p);
      else if (hi == 2)
        return Color.FromArgb(255, p, v, t);
      else if (hi == 3)
        return Color.FromArgb(255, p, q, v);
      else if (hi == 4)
        return Color.FromArgb(255, t, p, v);
      else
        return Color.FromArgb(255, v, p, q);
    }
    public static List<Color> ConvertBytesToRGB(byte[] bytes, bool translate = false)
    {
      List<Color> colors = new List<Color>();

      for (int i = 0; i < bytes.Length - 2; i += 3)
      {
        byte red = translate ? VgaTranslate(bytes[i]) : (bytes[i]);
        byte green = translate ? VgaTranslate(bytes[i + 1]) : (bytes[i + 1]);
        byte blue = translate ? VgaTranslate(bytes[i + 2]) : (bytes[i + 2]);

        Color color = Color.FromArgb(red, green, blue);
        colors.Add(color);
      }
      return colors;
    }

    public static List<SixLabors.ImageSharp.Color> ConvertBytesToRgbIS(byte[] bytes, bool translate = false)
    {
      List<SixLabors.ImageSharp.Color> colors = new List<SixLabors.ImageSharp.Color>();

      for (int i = 0; i < bytes.Length - 2; i += 3)
      {
        byte red = translate ? VgaTranslate(bytes[i]) : (bytes[i]);
        byte green = translate ? VgaTranslate(bytes[i + 1]) : (bytes[i + 1]);
        byte blue = translate ? VgaTranslate(bytes[i + 2]) : (bytes[i + 2]);

        Rgba32 color = new Rgba32(red, green, blue);
        colors.Add(color);
      }
      return colors;
    }

    public static List<Color> ConvertBytesToRGBA(byte[] bytes)
    {
      List<Color> colors = new List<Color>();

      for (int i = 0; i < bytes.Length - 3; i += 4)
      {
        byte red = (byte)(bytes[i]);
        byte green = (byte)(bytes[i + 1]);
        byte blue = (byte)(bytes[i + 2]);
        byte alpha = 255;

        Color color = Color.FromArgb(alpha, red, green, blue);
        colors.Add(color);
      }
      return colors;
    }

    public static List<Color> ConvertBytesToBGRA(byte[] bytes)
    {
      List<Color> colors = new List<Color>();

      for (int i = 0; i < bytes.Length - 3; i += 4)
      {
        byte blue = (byte)(bytes[i]);
        byte green = (byte)(bytes[i + 1]);
        byte red = (byte)(bytes[i + 2]);
        byte alpha = 255;

        Color color = Color.FromArgb(alpha, red, green, blue);
        colors.Add(color);
      }
      return colors;
    }

    public static List<Color> ConvertBytesToRGB(byte[] bytes, byte transR, byte transG, byte transB, bool translate = false)
    {
      List<Color> colors = new List<Color>();

      for (int i = 0; i < bytes.Length - 2; i += 3)
      {
        byte red = translate ? VgaTranslate(bytes[i]) : (bytes[i]);
        byte green = translate ? VgaTranslate(bytes[i + 1]) : (bytes[i + 1]);
        byte blue = translate ? VgaTranslate(bytes[i + 2]) : (bytes[i + 2]);
        if (red == transR && green == transG && blue == transB)
        {
          Color color = Color.Transparent;
          colors.Add(color);
        }
        else
        {
          Color color = Color.FromArgb(red, green, blue);
          colors.Add(color);
        }
      }
      return colors;
    }


    public static List<Color> ReadRgb15Palette(byte[] bytes)
    {
      List<Color> colors = new List<Color>();

      for (int i = 0; i < bytes.Length - 1; i += 2)
      {
        ushort color = BitConverter.ToUInt16(bytes.Skip(i).Take(2).Reverse().ToArray(), 0);
        byte red = (byte)((color >> 10) & 0x1F);
        byte green = (byte)((color >> 5) & 0x1F);
        byte blue = (byte)(color & 0x1F);

        red = (byte)((red << 3) | (red >> 2));
        green = (byte)((green << 3) | (green >> 2));
        blue = (byte)((blue << 3) | (blue >> 2));

        Color rgbColor = Color.FromArgb(red, green, blue);
        colors.Add(rgbColor);
      }

      return colors;
    }

    public static List<Color> ConvertBytesToARGB(byte[] bytes, int intensity = 1)
    {
      List<Color> colors = new List<Color>();

      for (int i = 0; i < bytes.Length - 3; i += 4)
      {
        byte red = (byte)(bytes[i]);
        byte green = (byte)(bytes[i + 1]);
        byte blue = (byte)(bytes[i + 2]);

        Color color = Color.FromArgb(red, green, blue);
        colors.Add(color);
      }
      return colors;
    }

    public static List<SixLabors.ImageSharp.Color> ConvertBytesToArgbIS(byte[] bytes, int intensity = 1)
    {
      List<SixLabors.ImageSharp.Color> colors = new List<SixLabors.ImageSharp.Color>();

      for (int i = 0; i < bytes.Length - 3; i += 4)
      {
        byte red = (byte)(bytes[i]);
        byte green = (byte)(bytes[i + 1]);
        byte blue = (byte)(bytes[i + 2]);

        Rgba32 color = new Rgba32(red, green, blue);
        colors.Add(color);
      }
      return colors;
    }

    public static List<SixLabors.ImageSharp.Color> ReadABgr15PaletteIS(byte[] bytes)
    {
      List<SixLabors.ImageSharp.Color> colors = new List<SixLabors.ImageSharp.Color>();

      for (int i = 0; i < bytes.Length - 1; i += 2)
      {
        ushort color = BitConverter.ToUInt16(bytes.Skip(i).Take(2).ToArray(), 0);
        byte alpha = (byte)((color >> 15) & 0x1);  // 1 bit for Alpha
        byte red = (byte)((color >> 10) & 0x1F);
        byte green = (byte)((color >> 5) & 0x1F);
        byte blue = (byte)(color & 0x1F);

        alpha = (byte)(alpha == 0 ? 0 : 255);
        red = (byte)((red << 3) | (red >> 2));
        green = (byte)((green << 3) | (green >> 2));
        blue = (byte)((blue << 3) | (blue >> 2));

        SixLabors.ImageSharp.Color rgbColor = new Rgba32(blue, green, red, 255);
        colors.Add(rgbColor);
      }

      return colors;
    }
    public static int FindClutColorTableOffset(byte[] data, int? startBank = null)
    {
      const int ClutHeaderSize = 4;
      byte[] clut = new byte[256];
      List<byte[]> clutHeaders = new List<byte[]>() {
        new byte[] { 0xC3, 0x00, 0x00, 0x00 },
        new byte[] { 0xC3, 0x00, 0x00, 0x01 },
        new byte[] { 0xC3, 0x00, 0x00, 0x02 },
        new byte[] { 0xC3, 0x00, 0x00, 0x03 }
      };
      List<byte[]> clutBanks = new List<byte[]>();

      for (int bs = startBank ?? 0; bs <= 3; bs++)
      {
        for (int i = 0; i <= data.Length - ClutHeaderSize; i++)
        {
          if (data[i] == clutHeaders[bs][0] && data[i + 1] == clutHeaders[bs][1] &&
              data[i + 2] == clutHeaders[bs][2] && data[i + 3] == clutHeaders[bs][3])
          {
            return i;
          }
        }
      }

      return -1;
    }
    private static byte VgaTranslate(byte value)
    {
      return (byte)(value * 255 / 63);
    }

    public static void RotateSubset(List<Color> colors, int startIndex, int endIndex, int permutations)
    {
      int subsetSize = endIndex - startIndex + 1;
      permutations = permutations % subsetSize; // Remove unnecessary full rotations

      if (permutations == 0 || subsetSize <= 1)
      {
        return;
      }

      // Create a temporary list to hold the rotated values
      List<Color> rotatedSubset = new List<Color>(subsetSize);
      for (int i = 0; i < subsetSize; i++)
      {
        rotatedSubset.Add(colors[startIndex + ((i - permutations + subsetSize) % subsetSize)]);
      }

      // Copy the rotated subset back into the original list
      for (int i = 0; i < subsetSize; i++)
      {
        colors[startIndex + i] = rotatedSubset[i];
      }
    }

    public static void ReverseRotateSubset(List<Color> colors, int startIndex, int endIndex, int permutations)
    {
      int subsetSize = endIndex - startIndex + 1;
      permutations = permutations % subsetSize; // Remove unnecessary full rotations

      if (permutations == 0 || subsetSize <= 1)
      {
        return;
      }

      // Create a temporary list to hold the rotated values
      List<Color> rotatedSubset = new List<Color>(subsetSize);

      for (int i = 0; i < subsetSize; i++)
      {
        rotatedSubset.Add(colors[startIndex + ((i + permutations) % subsetSize)]);
      }

      // Copy the rotated subset back into the original list
      for (int i = 0; i < subsetSize; i++)
      {
        colors[startIndex + i] = rotatedSubset[i];
      }
    }

    public static Bitmap CreateLabelledPalette(List<Color> colors)
    {
      int squareSize = colors.Count / 2;
      int cols = Math.Min(colors.Count / 2, 32);
      int rows = (int)Math.Ceiling(colors.Count / (double)cols);
      int fontSize = squareSize / 4;

      // Ensure at least one row even if colors list is empty
      rows = Math.Max(rows, 1);

      // Calculate the width and height of the bitmap
      int width = squareSize * cols;
      int height = squareSize * rows;

      // Create a new bitmap with the calculated dimensions
      Bitmap bitmap = new Bitmap(width, height);

      using (Graphics graphics = Graphics.FromImage(bitmap))
      {
        // Set the smoothing mode for better text quality
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        // Define the font and brush for the text
        using (Font font = new Font("Arial", fontSize, FontStyle.Bold))
        using (Brush textBrush = new SolidBrush(Color.White))
        {
          for (int i = 0; i < colors.Count; i++)
          {
            int xIndex = i % cols;
            int yIndex = i / cols;

            // Draw the 8x8 square
            using (Brush brush = new SolidBrush(colors[i]))
            {
              graphics.FillRectangle(brush, xIndex * squareSize, yIndex * squareSize, squareSize, squareSize);
            }

            //Draw the text (index) on the square
            string text = i.ToString();
            SizeF textSize = graphics.MeasureString(text, font);
            float x = (xIndex * squareSize) + (squareSize - textSize.Width) / 2;
            float y = (yIndex * squareSize) + (squareSize - textSize.Height) / 2;

            graphics.DrawString(text, font, textBrush, x, y);
          }
        }
      }

      return bitmap;
    }

    public static void WritePalette(string path, List<Color> colors)
    {
      // output a bitmap where each color in the colors array is an 8*8 pixel square
      var bitmap = CreateLabelledPalette(colors);
      bitmap.Save(path, ImageFormat.Png);
    }

    public static List<Color> ReadPalette(byte[] data, bool translate = false)
    {
      var length = (int)data.Length;
      List<Color> colors = new List<Color>();
      for (int i = 0; i < length; i += 4)
      {
        var r = translate ? VgaTranslate(data[i + 1]) : data[i + 1];
        var g = translate ? VgaTranslate(data[i + 2]) : data[i + 2];
        var b = translate ? VgaTranslate(data[i + 3]) : data[i + 3];
        var color = Color.FromArgb(255, r, g, b);
        colors.Add(color);
      }
      return colors;
    }

    public static Color[] GenerateGrayscalePalette(int numColors)
    {
      Color[] palette = new Color[numColors];
      float step = 1f / (numColors - 1);

      for (int i = 0; i < numColors; i++)
      {
        float intensity = step * i;
        byte value = (byte)(intensity * 255);
        palette[i] = Color.FromArgb(value, value, value);
      }
      return palette;
    }



    public static List<Color> ReadClutBankPalettes(byte[] data, byte count)
    {
      var length = 0x100;
      List<Color> colors = new List<Color>();
      for (int i = 4; i < 4 + length; i += 4)
      {
        var color = Color.FromArgb(255, data[i + 1], data[i + 2], data[i + 3]);
        colors.Add(color);
      }
      if (count >= 2)
      {
        for (int i = 264; i < 264 + length; i += 4)
        {
          var color = Color.FromArgb(255, data[i + 1], data[i + 2], data[i + 3]);
          colors.Add(color);
        }
      }
      if (count >= 3 && data[520] != 0)
      {
        for (int i = 524; i < 524 + length; i += 4)
        {
          var color = Color.FromArgb(255, data[i + 1], data[i + 2], data[i + 3]);
          colors.Add(color);
        }
      }
      if (count == 4 && data[780] != 0)
      {
        for (int i = 784; i < 784 + length; i += 4)
        {
          var color = Color.FromArgb(255, data[i + 1], data[i + 2], data[i + 3]);
          colors.Add(color);
        }
      }
      return colors;
    }

    public static List<byte[]> FindClutColorTable(byte[] data)
    {
      const int ClutHeaderSize = 4;
      byte[] clut = new byte[256];
      List<byte[]> clutHeaders = new List<byte[]>() {
        new byte[] { 0xC3, 0x00, 0x00, 0x00 },
        new byte[] { 0xC3, 0x00, 0x00, 0x01 },
        new byte[] { 0xC3, 0x00, 0x00, 0x02 },
        new byte[] { 0xC3, 0x00, 0x00, 0x03 }
      };
      List<byte[]> clutBanks = new List<byte[]>();

      for (int bs = 0; bs <= 3; bs++)
      {
        for (int i = 0; i <= data.Length - ClutHeaderSize; i++)
        {
          if (data[i] == clutHeaders[bs][0] && data[i + 1] == clutHeaders[bs][1] &&
              data[i + 2] == clutHeaders[bs][2] && data[i + 3] == clutHeaders[bs][3])
          {
            Array.Copy(data, i + ClutHeaderSize, clut, 0, clut.Length);
            clutBanks.Add(clut);
            clut = new byte[256];
            break;
          }
        }
      }

      return clutBanks;
    }

    public static List<Color> Read16BitRgbPalette(ushort[] palette)
    {
      List<Color> colors = new List<Color>();
      for (int i = 0; i < palette.Length; i++)
      {
        ushort color = palette[i];
        byte b = (byte)((color & 0xF800) >> 11);
        byte g = (byte)((color & 0x07E0) >> 5);
        byte r = (byte)(color & 0x001F);

        r = (byte)(r * 255 / 31);
        g = (byte)(g * 255 / 63);
        b = (byte)(b * 255 / 31);

        Color rgbColor = Color.FromArgb(b, g, r);
        colors.Add(rgbColor);
      }
      return colors;
    }
  }
}

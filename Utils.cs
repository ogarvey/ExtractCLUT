using System.Drawing;
using System.Drawing.Imaging;
using Color = System.Drawing.Color;
using SixLabors.ImageSharp.Formats.Gif;
using Image = SixLabors.ImageSharp.Image;
using SizeF = System.Drawing.SizeF;
using System.Text;
using Encoder = System.Drawing.Imaging.Encoder;

namespace ExtractCLUT
{
  public static class Utils
  {
    // utils.multiply = function(component, multiplier)
    // {
    //   if (!isFinite(multiplier) || multiplier === 0)
    //   {
    //     return 0;
    //   }

    //   return Math.round(component * multiplier);
    // };

    public static int Multiply(int component, int multiplier)
    {
      if (double.IsInfinity(multiplier) || multiplier == 0)
      {
        return 0;
      }

      return (int)Math.Round((double)(component * multiplier));
    }

    public static string ReadNullTerminatedString(this BinaryReader reader)
    {
      var byteList = new List<byte>();
      byte currentByte;

      while ((currentByte = reader.ReadByte()) != 0x00)
      {
        byteList.Add(currentByte);
      }

      return Encoding.ASCII.GetString(byteList.ToArray());
    }
    public static int bytesToInt(this byte[] bytes)
    {
      return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
    }
    public static ushort ReadBigEndianUInt16(BinaryReader reader)
    {
      byte[] bytes = reader.ReadBytes(2);
      Array.Reverse(bytes);
      return BitConverter.ToUInt16(bytes, 0);
    }

    public static int ReadBigEndianInt16(BinaryReader reader)
    {
      byte[] bytes = reader.ReadBytes(2);
      Array.Reverse(bytes);
      return BitConverter.ToInt16(bytes, 0);
    }

    public static uint ReadBigEndianUInt32(BinaryReader reader)
    {
      byte[] bytes = reader.ReadBytes(4);
      Array.Reverse(bytes);
      return BitConverter.ToUInt32(bytes, 0);
    }
    
    public static bool MatchesSequence(BinaryReader reader, byte[] sequence)
    {
      for (int i = 0; i < sequence.Length; i++)
      {
        byte nextByte = reader.ReadByte();
        if (nextByte != sequence[i])
        {
          // rewind to the start of the sequence (including the byte we just read)
          reader.BaseStream.Position -= i + 1;
          return false;
        }
      }

      return true;
    }
    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> source)
    {
      return source.Select((item, index) => (item, index));
    }

    public static byte[] GetPaletteBytes(string path, int offset)
    {
      var paletteBytes = new byte[1024];
      byte[] fileBytes;

      // Read file bytes using FileStream and close the file when done
      using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
      {
        fileBytes = new byte[fileStream.Length];
        fileStream.Read(fileBytes, 0, fileBytes.Length);
      }
      if (fileBytes.Length < offset + 1040)
      {
        throw new Exception("File is too small to contain a palette");
      }
      var palette1 = fileBytes.Skip(offset + 4).Take(256).ToArray();
      var palette2 = fileBytes.Skip(offset + 264).Take(256).ToArray();
      var palette3 = fileBytes.Skip(offset + 524).Take(256).ToArray();
      var palette4 = fileBytes.Skip(offset + 784).Take(256).ToArray();

      palette1.CopyTo(paletteBytes, 0);
      palette2.CopyTo(paletteBytes, 256);
      palette3.CopyTo(paletteBytes, 512);
      palette4.CopyTo(paletteBytes, 768);

      return paletteBytes;
    }
    public static void SplitCsvFiles(string directoryPath, string outputFile)
    {
      // Get all files in the directory
      string[] files = Directory.GetFiles(directoryPath, "*.txt");

      // Create a new output file
      using (StreamWriter writer = new StreamWriter(outputFile))
      {
        // Loop through each file in the directory
        foreach (string file in files)
        {
          // Read the contents of the file
          string content = File.ReadAllText(file);

          // Split the comma-separated values into an array
          string[] values = content.Split(',');

          // Loop through the values and write them to the output file in rows of 40
          for (int i = 0; i < values.Length; i += 40)
          {
            // Join the next 40 values into a comma-separated string
            string row = string.Join(",", values.Skip(i).Take(40));

            // Write the row to the output file
            writer.WriteLine(row);
          }
          writer.WriteLine("-----END FILE: " + file + " -----");
        }
      }
    }

    public static Bitmap CreateImage(byte[] imageBin, List<Color> colors, int Width, int Height, bool useTransparency = false)
    {
      // convert each byte of imageBin to an int and use that as an index into the colors array to create a 384 pixel wide image
      var image = new Bitmap(Width, Height);
      var graphics = Graphics.FromImage(image);
      var brush = new SolidBrush(Color.Black);
      var x = 0;
      var y = 0;
      var width = 1;
      var height = 1;
      if (useTransparency)
      {
        colors[0] = Color.Transparent;
      }
      foreach (var b in imageBin)
      {
        if (b >= colors.Count)
        {
          brush.Color = colors[b % colors.Count];
        }
        else
        {
          brush.Color = colors[b];
        }
        graphics.FillRectangle(brush, x, y, width, height);
        x += width;
        if (x >= Width)
        {
          x = 0;
          y += height;
        }
      }

      return image;

    }

    public static List<byte[]> SplitAsterixBinaryFile(string filePath)
    {
      List<byte[]> byteArrays = new List<byte[]>();
      byte[] delimiter = new byte[] { 0x00, 0x00, 0x00, 0x80 };

      using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
      {
        List<byte> currentChunk = new List<byte>();
        int delimiterIndex = 0;

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
          byte currentByte = reader.ReadByte();

          if (currentByte == delimiter[delimiterIndex])
          {
            delimiterIndex++;
          }
          else
          {
            delimiterIndex = 0;
          }

          currentChunk.Add(currentByte);

          // Check if we have a complete delimiter sequence
          if (delimiterIndex == 4)
          {
            // Save the current chunk, excluding the delimiter at the end
            byteArrays.Add(currentChunk.GetRange(0, currentChunk.Count - 4).ToArray());

            // Start a new chunk, including the delimiter at the beginning
            currentChunk = new List<byte>(delimiter);
            delimiterIndex = 0;
          }
        }

        // Save the last chunk if it's not empty
        if (currentChunk.Count > 0)
        {
          byteArrays.Add(currentChunk.ToArray());
        }
      }

      return byteArrays;
    }
    static ImageCodecInfo GetEncoderInfo(string mimeType)
    {
      // Get the codec information for the specified MIME type
      ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();

      foreach (ImageCodecInfo codec in codecs)
      {
        if (codec.MimeType == mimeType)
        {
          return codec;
        }
      }

      return null;
    }
    public static void ConvertBitmapsToGif(List<Bitmap> bitmaps, string filePath)
    {
      // Set the encoder parameters for the GIF animation
      EncoderParameters encoderParams = new EncoderParameters(1);
      encoderParams.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.MultiFrame);

      // Get the codec for the GIF file format
      ImageCodecInfo gifCodec = GetEncoderInfo("image/gif");

      // Save the first frame of the GIF animation
      bitmaps[0].Save(filePath, gifCodec, encoderParams);

      // Set the encoder parameters for the subsequent frames
      encoderParams.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.FrameDimensionTime);

      // Save the remaining frames of the GIF animation
      for (int i = 1; i < bitmaps.Count; i++)
      {
        bitmaps[0].SaveAdd(bitmaps[i], encoderParams);
      }

      // Set the encoder parameters for the final frame of the GIF animation
      encoderParams.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.Flush);

      // Save the final frame of the GIF animation
      bitmaps[0].SaveAdd(encoderParams);
    }
    public static int ExtractNumber(string fileName)
    {
      // Split the filename on the underscore and parse the first part as an integer
      var parts = fileName.Split('_');
      if (parts.Length > 0 && int.TryParse(parts[^1], out int number))
      {
        return number;
      }
      return 0; // Default to 0 if no number is found or the parsing fails
    }
    public static byte? PeekByte(this BinaryReader reader)
    {
      if (reader.BaseStream.Position >= reader.BaseStream.Length)
      {
        return null;
      }

      byte nextByte = reader.ReadByte();
      reader.BaseStream.Seek(-1, SeekOrigin.Current);
      return nextByte;
    }
  }
}

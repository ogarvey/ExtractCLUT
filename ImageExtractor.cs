using System.Collections.Generic;
using System.IO;
using ExtractCLUT;

class ImageExtractor
{
  public static List<byte[]> LoTRSExtractImagesFromBinary(string filePath)
  {
    List<byte[]> images = new List<byte[]>();
    const byte startByte = 0x80;
    const byte separatorByte = 0x00;

    using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
    {
      List<byte> imageBytes = new List<byte>();
      bool inImage = false;
      bool previousByteWasSeparator = false;

      while (reader.BaseStream.Position < reader.BaseStream.Length)
      {
        byte currentByte = reader.ReadByte();
        byte? nextByte = reader.PeekByte();

        if (!inImage && currentByte == startByte && nextByte == separatorByte)
        {
          inImage = true;
          imageBytes.Clear();
        }

        if (inImage)
        {
          imageBytes.Add(currentByte);

          if (currentByte == separatorByte)
          {
            if (previousByteWasSeparator)
            {
              // We've found two or more consecutive separator bytes (00 00, 00 00 00, etc.)
              imageBytes.RemoveAt(imageBytes.Count - 1); // Remove the last separator byte
              images.Add(imageBytes.ToArray());
              inImage = false;

              // Skip the rest of the separator bytes
              while (reader.PeekByte() == separatorByte)
              {
                reader.ReadByte();
              }
            }
            else
            {
              previousByteWasSeparator = true;
            }
          }
          else
          {
            previousByteWasSeparator = false;
          }
        }
      }
    }

    return images;
  }

  public static List<byte[]> AsterixExtractImagesFromBinary(string filePath)
  {
    List<byte[]> images = new List<byte[]>();
    const byte startByte = 0x80;
    const byte separatorByte = 0xFF;

    using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
    {
      List<byte> imageBytes = new List<byte>();
      bool inImage = false;
      bool previousByteWasSeparator = false;

      while (reader.BaseStream.Position < reader.BaseStream.Length)
      {
        byte currentByte = reader.ReadByte();
        byte? nextByte = reader.PeekByte();

        if (!inImage && currentByte == startByte && nextByte == separatorByte)
        {
          inImage = true;
          imageBytes.Clear();
        }

        if (inImage)
        {
          imageBytes.Add(currentByte);

          if (currentByte == separatorByte)
          {
            if (previousByteWasSeparator)
            {
              // We've found two or more consecutive separator bytes (00 00, 00 00 00, etc.)
              imageBytes.RemoveAt(imageBytes.Count - 1); // Remove the last separator byte
              images.Add(imageBytes.ToArray());
              inImage = false;

              // Skip the rest of the separator bytes
              while (reader.PeekByte() == separatorByte)
              {
                reader.ReadByte();
              }
            }
            else
            {
              previousByteWasSeparator = true;
            }
          }
          else
          {
            previousByteWasSeparator = false;
          }
        }
      }
    }

    return images;
  }

  public static List<byte[]> ExtractImagesFromBinary2(string filePath)
  {
    List<byte[]> images = new List<byte[]>();
    const byte startByte = 0x80;
    const byte separatorByte = 0x00;

    using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
    {
      List<byte> imageBytes = new List<byte>();
      bool inImage = false;
      bool previousByteWasSeparator = false;

      while (reader.BaseStream.Position < reader.BaseStream.Length)
      {
        byte currentByte = reader.ReadByte();

        if (!inImage && currentByte == startByte && reader.PeekChar() == separatorByte)
        {
          inImage = true;
          imageBytes.Clear();
        }

        if (inImage)
        {
          imageBytes.Add(currentByte);

          if (currentByte == separatorByte)
          {
            if (previousByteWasSeparator)
            {
              // We've found two or more consecutive separator bytes (00 00, 00 00 00, etc.)
              imageBytes.RemoveAt(imageBytes.Count - 1); // Remove the last separator byte
              images.Add(imageBytes.ToArray());
              inImage = false;

              // Skip the rest of the separator bytes
              while (reader.PeekChar() == separatorByte)
              {
                reader.ReadByte();
              }
            }
            else
            {
              previousByteWasSeparator = true;
            }
          }
          else
          {
            previousByteWasSeparator = false;
          }
        }
      }
    }

    return images;
  }
}

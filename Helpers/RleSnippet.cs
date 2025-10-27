
// public static byte[] Rle7(byte[] rleData, int lineWidth)
// {
//   List<byte[]> lines = new List<byte[]>();
//   List<byte> currentLine = new List<byte>();

//   try
//   {

//     int i = 0;
//     while (i < rleData.Length)
//     {
//       byte firstByte = rleData[i];
//       bool isRun = (firstByte & 0x80) != 0; // Check if the MSB is set
//       byte colorIndex = (byte)(firstByte & 0x7F); // Extract color index (7 bits)

//       if (isRun)
//       {
//         if (i + 1 >= rleData.Length)
//         {
//           break;
//         }

//         byte runLength = rleData[i + 1];
//         i += 2;

//         if (runLength == 1)
//         {
//           //throw new Exception("Invalid RLE data: Run length of 1 is forbidden.");
//           continue;
//         }

//         int addLength = (runLength == 0) ? (lineWidth - currentLine.Count) : runLength;

//         if (currentLine.Count + addLength > lineWidth)
//         {
//           addLength = lineWidth - currentLine.Count;
//         }

//         currentLine.AddRange(Enumerable.Repeat(colorIndex, addLength));
//       }
//       else // Single pixel
//       {
//         currentLine.Add(colorIndex);
//         i++;
//       }

//       if (currentLine.Count == lineWidth)
//       {
//         lines.Add(currentLine.ToArray());
//         currentLine.Clear();
//       }
//     }

//     // Add the last line if not empty
//     if (currentLine.Count > 0)
//     {
//       lines.Add(currentLine.ToArray());
//     }
//   }
//   catch (Exception ex)
//   {
//     //MessageBox.Show($"Error at line {lines.Count}, returning image so far: {ex}");
//     return lines.SelectMany(l => l).ToArray();
//   }

//   return lines.SelectMany(l => l).ToArray();
// }
// /// <summary>
// /// Encodes raw pixel data into Rle7 format.
// /// </summary>
// /// <param name="pixelData">A flat byte array of color indices for the image.</param>
// /// <param name="width">The width of the image.</param>
// /// <param name="height">The height of the image.</param>
// /// <returns>A byte array containing the Rle7 encoded data.</returns>
// public static byte[] EncodeRle7(byte[] pixelData, int width, int height)
// {
//   var encodedData = new List<byte>();

//   for (int y = 0; y < height; y++)
//   {
//     int lineStart = y * width;
//     int x = 0;
//     while (x < width)
//     {
//       byte colorIndex = pixelData[lineStart + x];
//       if (colorIndex > 127)
//       {
//         throw new ArgumentException($"Color index {colorIndex} at position ({x},{y}) is too large for Rle7 encoding. Must be < 128.");
//       }

//       // Find the length of the run for the current color
//       int runLength = 1;
//       while (x + runLength < width && runLength < 255 && pixelData[lineStart + x + runLength] == colorIndex)
//       {
//         runLength++;
//       }

//       if (runLength > 1)
//       {
//         // This is a run of pixels
//         byte firstByte = (byte)(0x80 | colorIndex);
//         encodedData.Add(firstByte);

//         // If the run extends to the end of the line, the length is encoded as 0
//         if (x + runLength == width)
//         {
//           encodedData.Add(0);
//         }
//         else
//         {
//           encodedData.Add((byte)runLength);
//         }
//         x += runLength;
//       }
//       else
//       {
//         // This is a single pixel
//         encodedData.Add(colorIndex);
//         x++;
//       }
//     }
//   }

//   return encodedData.ToArray();
// }

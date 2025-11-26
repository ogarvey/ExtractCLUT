using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC
{
    public static class VoD
    {
        public struct DecompressedImage
        {
            public byte[] Data;
            public int Width;
            public int Height;
            public int StartX; // Added StartX
        }

        public static DecompressedImage Decompress(byte[] fileData)
        {
            if (fileData == null || fileData.Length < 0xA)
                throw new ArgumentException("Invalid data");

            ushort columnCount = BitConverter.ToUInt16(fileData, 0x06);
            ushort startX = BitConverter.ToUInt16(fileData, 0x08);

            if (columnCount == 0)
                return new DecompressedImage { Data = new byte[0], Width = 0, Height = 0, StartX = startX };

            // First pass: Determine height
            int maxY = 0;
            int offsetsStart = 0x0A;

            for (int i = 0; i < columnCount; i++)
            {
                int offsetPtr = offsetsStart + (i * 2);
                if (offsetPtr + 2 > fileData.Length) break;

                ushort lineInfoOffset = BitConverter.ToUInt16(fileData, offsetPtr);

                int currentPtr = lineInfoOffset;
                while (currentPtr + 6 <= fileData.Length)
                {
                    ushort height = BitConverter.ToUInt16(fileData, currentPtr);
                    if (height == 0) break;

                    ushort y = BitConverter.ToUInt16(fileData, currentPtr + 2);
                    if (y + height > maxY)
                        maxY = y + height;

                    currentPtr += 6;
                }
            }

            // The width of the data buffer is just the number of columns in this chunk.
            // The actual screen position is determined by StartX.
            int width = columnCount;
            int heightVal = maxY;
            byte[] pixelData = new byte[width * heightVal];

            // Second pass: Fill data
            for (int i = 0; i < columnCount; i++)
            {
                int offsetPtr = offsetsStart + (i * 2);
                if (offsetPtr + 2 > fileData.Length) break;

                ushort lineInfoOffset = BitConverter.ToUInt16(fileData, offsetPtr);

                int currentPtr = lineInfoOffset;
                while (currentPtr + 6 <= fileData.Length)
                {
                    ushort segmentHeight = BitConverter.ToUInt16(fileData, currentPtr);
                    if (segmentHeight == 0) break;

                    ushort y = BitConverter.ToUInt16(fileData, currentPtr + 2);
                    ushort dataOffset = BitConverter.ToUInt16(fileData, currentPtr + 4);

                    for (int j = 0; j < segmentHeight; j++)
                    {
                        if (dataOffset + j < fileData.Length)
                        {
                            byte pixel = fileData[dataOffset + j];
                            int targetY = y + j;

                            if (targetY < heightVal)
                            {
                                // Treat 0 as transparent (don't overwrite existing data)
                                // This handles overlapping segments where a later segment
                                // has a 'hole' (0) that shouldn't erase the underlying pixel
                                // drawn by an earlier segment.
                                if (pixel != 0)
                                {
                                    pixelData[targetY * width + i] = pixel;
                                }
                            }
                        }
                    }

                    currentPtr += 6;
                }
            }

            return new DecompressedImage
            {
                Data = pixelData,
                Width = width,
                Height = heightVal,
                StartX = startX
            };
        }


        public static void Merge(DecompressedImage baseImage, DecompressedImage overlay)
        {
            // Merge overlay into baseImage based on StartX

            for (int x = 0; x < overlay.Width; x++)
            {
                int targetX = overlay.StartX + x - baseImage.StartX;

                // Check if the overlay column falls within the base image width
                if (targetX >= 0 && targetX < baseImage.Width)
                {
                    for (int y = 0; y < overlay.Height; y++)
                    {
                        if (y < baseImage.Height)
                        {
                            byte pixel = overlay.Data[y * overlay.Width + x];
                            // Treat 0 as transparent
                            if (pixel != 0)
                            {
                                baseImage.Data[y * baseImage.Width + targetX] = pixel;
                            }
                        }
                    }
                }
            }
        }
    }
}

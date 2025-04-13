using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC
{
    public static class SAGAHelper
    {
        public static List<long> FillFrameOffsets(byte[] resourceData, int maxFrame, bool reallyFill = true)
        {
            List<long> frameOffsets = new List<long>();
            ushort currentFrame = 0;
            byte markByte;
            ushort control;
            ushort runcount;

            bool longData = true;

            using (MemoryStream memoryStream = new MemoryStream(resourceData))
            using (BinaryReader reader = new BinaryReader(memoryStream))
            {
                while (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    if (reallyFill)
                    {
                        frameOffsets.Add(reader.BaseStream.Position);

                        if (currentFrame == maxFrame)
                            break;
                    }
                    currentFrame++;

                    do
                    {
                        markByte = reader.ReadByte();

                        switch (markByte)
                        {
                            case (byte)SAGAFrameControl.SAGA_FRAME_START:
                                // Skip header
                                reader.BaseStream.Seek(longData ? 13 : 12, SeekOrigin.Current);
                                continue;

                            case (byte)SAGAFrameControl.SAGA_FRAME_END:
                                continue;

                            case (byte)SAGAFrameControl.SAGA_FRAME_REPOSITION:
                                reader.ReadInt16(); // Big-endian 16-bit value
                                continue;

                            case (byte)SAGAFrameControl.SAGA_FRAME_ROW_END:
                                reader.ReadInt16(); // Big-endian 16-bit value
                                if (longData)
                                    reader.ReadBigEndianInt16(); // Big-endian 16-bit value
                                else
                                    reader.ReadByte(); // Single byte
                                continue;

                            case (byte)SAGAFrameControl.SAGA_FRAME_LONG_COMPRESSED_RUN:
                                reader.ReadBigEndianInt16(); // Big-endian 16-bit value
                                reader.ReadByte();  // Skip 1 byte
                                continue;

                            case (byte)SAGAFrameControl.SAGA_FRAME_LONG_UNCOMPRESSED_RUN:
                                runcount = reader.ReadBigEndianUInt16(); // Big-endian 16-bit value
                                reader.BaseStream.Seek(runcount, SeekOrigin.Current); // Skip runcount bytes
                                continue;

                            case (byte)SAGAFrameControl.SAGA_FRAME_NOOP:
                                reader.ReadByte(); // Skip 3 bytes
                                reader.ReadByte();
                                reader.ReadByte();
                                continue;

                            default:
                                break;
                        }

                        // Mask all but two high-order (control) bits
                        control = (ushort)(markByte & 0xC0);

                        switch (control)
                        {
                            case (byte)SAGAFrameControl.SAGA_FRAME_EMPTY_RUN:
                                // Run of empty pixels
                                continue;

                            case (byte)SAGAFrameControl.SAGA_FRAME_COMPRESSED_RUN:
                                // Run of compressed data
                                reader.ReadByte(); // Skip 1 byte
                                continue;

                            case (byte)SAGAFrameControl.SAGA_FRAME_UNCOMPRESSED_RUN:
                                // Uncompressed run
                                runcount = (ushort)((markByte & 0x3F) + 1);
                                for (int i = 0; i < runcount; i++)
                                    reader.ReadByte(); // Skip runcount bytes
                                continue;

                            default:
                                throw new InvalidOperationException($"Encountered unknown RLE marker {markByte}");
                        }

                    } while (markByte != (byte)SAGAFrameControl.SAGA_FRAME_END);
                }
            }

            return frameOffsets;
        }

        public static byte[] DecodeFrame(byte[] animData, long frameOffset, int screenWidth, int screenHeight)
        {
            // Create a buffer to hold the decoded frame
            byte[] buffer = new byte[screenWidth * screenHeight];

            using (MemoryStream memoryStream = new MemoryStream(animData, (int)frameOffset, animData.Length - (int)frameOffset))
            using (BinaryReader reader = new BinaryReader(memoryStream))
            {
                int xStart = 0;
                int yStart = 0;
                int xVector;
                int newRow;
                int runcount;
                int dataByte;
                int writeIndex = 0;

                // Begin RLE decompression to output buffer
                while (true)
                {
                    int markByte = reader.ReadByte();

                    switch (markByte)
                    {
                        case (byte)SAGAFrameControl.SAGA_FRAME_START:
                            xStart = reader.ReadBigEndianUInt16();  // Big-endian
                            yStart = reader.ReadBigEndianUInt16();  // Big-endian
                            reader.ReadByte();  // Skip pad byte
                            reader.ReadBigEndianUInt16();  // Skip xPos
                            reader.ReadBigEndianUInt16();  // Skip yPos
                            reader.ReadBigEndianUInt16();  // Skip width
                            reader.ReadBigEndianUInt16();  // Skip height

                            // Set write pointer to the draw origin
                            writeIndex = (yStart * screenWidth) + xStart;
                            ValidateWritePointer(writeIndex, buffer, screenWidth, screenHeight);
                            continue;

                        case (byte)SAGAFrameControl.SAGA_FRAME_NOOP:
                            reader.ReadBytes(3);  // Skip 3 bytes
                            continue;

                        case (byte)SAGAFrameControl.SAGA_FRAME_LONG_UNCOMPRESSED_RUN:
                            runcount = reader.ReadBigEndianUInt16();  // Big-endian
                            for (int i = 0; i < runcount; i++)
                            {
                                dataByte = reader.ReadByte();
                                if (dataByte != 0)
                                {
                                    buffer[writeIndex] = (byte)dataByte;
                                }
                                writeIndex++;
                                ValidateWritePointer(writeIndex, buffer, screenWidth, screenHeight);
                            }
                            continue;

                        case (byte)SAGAFrameControl.SAGA_FRAME_LONG_COMPRESSED_RUN:
                            runcount = reader.ReadBigEndianUInt16();  // Big-endian
                            dataByte = reader.ReadByte();
                            for (int i = 0; i < runcount; i++)
                            {
                                buffer[writeIndex++] = (byte)dataByte;
                                ValidateWritePointer(writeIndex, buffer, screenWidth, screenHeight);
                            }
                            continue;

                        case (byte)SAGAFrameControl.SAGA_FRAME_ROW_END:
                            xVector = reader.ReadBigEndianInt16();  // Big-endian
                            newRow = reader.ReadBigEndianInt16();   // Big-endian

                            // Set write pointer to the new draw origin
                            writeIndex = ((yStart + newRow) * screenWidth) + xStart + xVector;
                            ValidateWritePointer(writeIndex, buffer, screenWidth, screenHeight);
                            continue;

                        case (byte)SAGAFrameControl.SAGA_FRAME_REPOSITION:
                            xVector = reader.ReadBigEndianInt16();  // Big-endian
                            writeIndex += xVector;
                            ValidateWritePointer(writeIndex, buffer, screenWidth, screenHeight);
                            continue;

                        case (byte)SAGAFrameControl.SAGA_FRAME_END:
                            // End of frame marker, exit the loop
                            return buffer;

                        default:
                            break;
                    }

                    // Mask all but two high-order control bits
                    int controlChar = markByte & 0xC0;
                    int paramChar = markByte & 0x3F;
                    switch (controlChar)
                    {
                        case (byte)SAGAFrameControl.SAGA_FRAME_EMPTY_RUN:
                            // Run of empty pixels
                            runcount = paramChar + 1;
                            writeIndex += runcount;
                            ValidateWritePointer(writeIndex, buffer, screenWidth, screenHeight);
                            continue;

                        case (byte)SAGAFrameControl.SAGA_FRAME_COMPRESSED_RUN:
                            // Run of compressed data
                            runcount = paramChar + 1;
                            dataByte = reader.ReadByte();
                            for (int i = 0; i < runcount; i++)
                            {
                                buffer[writeIndex++] = (byte)dataByte;
                                ValidateWritePointer(writeIndex, buffer, screenWidth, screenHeight);
                            }
                            continue;

                        case (byte)SAGAFrameControl.SAGA_FRAME_UNCOMPRESSED_RUN:
                            // Uncompressed run
                            runcount = paramChar + 1;
                            for (int i = 0; i < runcount; i++)
                            {
                                dataByte = reader.ReadByte();
                                if (dataByte != 0)
                                {
                                    buffer[writeIndex] = (byte)dataByte;
                                }
                                writeIndex++;
                                ValidateWritePointer(writeIndex, buffer, screenWidth, screenHeight);
                            }
                            continue;

                        default:
                            throw new InvalidOperationException("decodeFrame() Invalid RLE marker encountered");
                    }
                }
            }
        }

        public static byte[] DecodeRLEBuffer(byte[] inputBuffer)
        {
            // Use a List<byte> to dynamically resize the output buffer
            List<byte> outputBuffer = new List<byte>();

            // Use MemoryStream and BinaryReader to read the input buffer
            using (MemoryStream memoryStream = new MemoryStream(inputBuffer))
            using (BinaryReader reader = new BinaryReader(memoryStream))
            {
                int bgRuncount;
                int fgRuncount;

                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    // Read the background run count
                    bgRuncount = reader.ReadByte();
                    if (reader.BaseStream.Position >= reader.BaseStream.Length)
                        break;

                    // Read the foreground run count
                    fgRuncount = reader.ReadByte();

                    // Add background (zeroes) to the output buffer
                    for (int c = 0; c < bgRuncount; c++)
                    {
                        outputBuffer.Add(0);
                    }

                    // Add foreground (data from inputBuffer) to the output buffer
                    for (int c = 0; c < fgRuncount; c++)
                    {
                        if (reader.BaseStream.Position >= reader.BaseStream.Length)
                            break;

                        outputBuffer.Add(reader.ReadByte());
                    }
                }
            }
            return outputBuffer.ToArray();
        }

        public static byte[] DecodeBGImageRLE(byte[] inputData, int width, int height)
        {
            if (inputData == null || inputData.Length == 0)
                throw new ArgumentException("Input data cannot be null or empty.");

            List<byte> outputData = new List<byte>();

            int inbufIndex = 0;
            int inbufLength = inputData.Length;

            while (inbufIndex < inbufLength)
            {
                byte markByte = inputData[inbufIndex++];
                int testByte = markByte & 0xC0; // Mask all but two high order bits

                switch (testByte)
                {
                    case 0xC0: // Uncompressed run follows, max runlength 63
                        {
                            int runCount = markByte & 0x3F;
                            if (inbufIndex + runCount > inbufLength)
                                throw new InvalidOperationException("Input buffer too short for uncompressed run.");

                            for (int i = 0; i < runCount; i++)
                            {
                                outputData.Add(inputData[inbufIndex++]);
                            }
                        }
                        break;

                    case 0x80: // Compressed run follows, max runlength 63
                        {
                            int runCount = (markByte & 0x3F) + 3;
                            if (inbufIndex >= inbufLength)
                                throw new InvalidOperationException("Input buffer too short for compressed run.");

                            byte value = inputData[inbufIndex++];
                            for (int i = 0; i < runCount; i++)
                            {
                                outputData.Add(value);
                            }
                        }
                        break;

                    case 0x40: // Repeat decoded sequence from output stream, max runlength 10
                        {
                            int runCount = ((markByte >> 3) & 0x07) + 3;
                            int backtrackAmount = inputData[inbufIndex++];

                            if (backtrackAmount > outputData.Count)
                                throw new InvalidOperationException("Backtrack amount exceeds output buffer size.");

                            for (int i = 0; i < runCount; i++)
                            {
                                outputData.Add(outputData[outputData.Count - backtrackAmount]);
                            }
                        }
                        break;

                    default: // Process based on the third and fourth highest order bits
                        testByte = markByte & 0x30;
                        switch (testByte)
                        {
                            case 0x30: // Bitfield compression
                                {
                                    int runCount = (markByte & 0x0F) + 1;
                                    if (inbufIndex + runCount + 2 > inbufLength)
                                        throw new InvalidOperationException("Input buffer too short for bitfield compression.");

                                    byte bitfieldByte1 = inputData[inbufIndex++];
                                    byte bitfieldByte2 = inputData[inbufIndex++];

                                    for (int i = 0; i < runCount; i++)
                                    {
                                        byte bitfield = inputData[inbufIndex++];
                                        for (int b = 0; b < 8; b++)
                                        {
                                            if ((bitfield & 0x80) != 0)
                                            {
                                                outputData.Add(bitfieldByte2);
                                            }
                                            else
                                            {
                                                outputData.Add(bitfieldByte1);
                                            }
                                            bitfield <<= 1;
                                        }
                                    }
                                }
                                break;

                            case 0x20: // Uncompressed run follows
                                {
                                    int runCount = ((markByte & 0x0F) << 8) + inputData[inbufIndex++];
                                    if (inbufIndex + runCount > inbufLength)
                                        throw new InvalidOperationException("Input buffer too short for uncompressed run.");

                                    for (int i = 0; i < runCount; i++)
                                    {
                                        outputData.Add(inputData[inbufIndex++]);
                                    }
                                }
                                break;

                            case 0x10: // Repeat decoded sequence from output stream
                                {
                                    int backtrackAmount = ((markByte & 0x0F) << 8) + inputData[inbufIndex++];
                                    int runCount = inputData[inbufIndex++];

                                    if (backtrackAmount > outputData.Count)
                                        throw new InvalidOperationException("Backtrack amount exceeds output buffer size.");

                                    for (int i = 0; i < runCount; i++)
                                    {
                                        outputData.Add(outputData[outputData.Count - backtrackAmount]);
                                    }
                                }
                                break;

                            default:
                                return UnbankBGImage(outputData.ToArray(), width, height);
                        }
                        break;
                }
            }

            return UnbankBGImage(outputData.ToArray(), width, height);
        }

        public static byte[] UnbankBGImage(byte[] srcBuf, int columns, int scanlines)
        {
            if (scanlines <= 0)
                throw new ArgumentException("Scanlines must be greater than 0.");

            // Create a destination buffer of appropriate size
            byte[] dstBuf = new byte[columns * scanlines];

            int quadrupleRows = scanlines - (scanlines % 4);
            int remainRows = scanlines - quadrupleRows;

            int rowJumpSrc = columns * 4;
            int rowJumpDest = columns * 4;

            int srcIndex = 0;
            int dstIndex = 0;

            // Process blocks of 4 rows at a time
            for (int y = 0; y < quadrupleRows; y += 4)
            {
                for (int x = 0; x < columns; x++)
                {
                    int temp = x * 4;

                    // Assign each destination pointer its respective source byte
                    dstBuf[dstIndex + x] = srcBuf[srcIndex + temp];
                    dstBuf[dstIndex + columns + x] = srcBuf[srcIndex + 1 + temp];
                    dstBuf[dstIndex + columns * 2 + x] = srcBuf[srcIndex + 2 + temp];
                    dstBuf[dstIndex + columns * 3 + x] = srcBuf[srcIndex + 3 + temp];
                }

                // Move to the next block of 4 rows
                if (y < quadrupleRows - 4)
                {
                    dstIndex += rowJumpDest;
                    srcIndex += rowJumpSrc;
                }
            }

            // Handle remaining rows (less than 4)
            switch (remainRows)
            {
                case 1:
                    dstIndex += rowJumpDest;
                    srcIndex += rowJumpSrc;
                    for (int x = 0; x < columns; x++)
                    {
                        int temp = x * 4;
                        dstBuf[dstIndex + x] = srcBuf[srcIndex + temp];
                    }
                    break;

                case 2:
                    dstIndex += rowJumpDest;
                    srcIndex += rowJumpSrc;
                    for (int x = 0; x < columns; x++)
                    {
                        int temp = x * 4;
                        dstBuf[dstIndex + x] = srcBuf[srcIndex + temp];
                        dstBuf[dstIndex + columns + x] = srcBuf[srcIndex + 1 + temp];
                    }
                    break;

                case 3:
                    dstIndex += rowJumpDest;
                    srcIndex += rowJumpSrc;
                    for (int x = 0; x < columns; x++)
                    {
                        int temp = x * 4;
                        dstBuf[dstIndex + x] = srcBuf[srcIndex + temp];
                        dstBuf[dstIndex + columns + x] = srcBuf[srcIndex + 1 + temp];
                        dstBuf[dstIndex + columns * 2 + x] = srcBuf[srcIndex + 2 + temp];
                    }
                    break;

                default:
                    break;
            }

            return dstBuf;
        }


        private static void ValidateWritePointer(int writeIndex, byte[] buffer, int screenWidth, int screenHeight)
        {
            if (writeIndex < 0 || writeIndex >= (screenWidth * screenHeight))
            {
                throw new InvalidOperationException("decodeFrame() Write pointer is out of bounds.");
            }
        }
    }

    public enum SAGAFrameControl
    {
        SAGA_FRAME_START = 0x0F,
        SAGA_FRAME_END = 0x3F,
        SAGA_FRAME_NOOP = 0x1F,
        SAGA_FRAME_REPOSITION = 0x30,
        SAGA_FRAME_ROW_END = 0x2F,
        SAGA_FRAME_LONG_COMPRESSED_RUN = 0x20,
        SAGA_FRAME_LONG_UNCOMPRESSED_RUN = 0x10,
        SAGA_FRAME_COMPRESSED_RUN = 0x80,
        SAGA_FRAME_UNCOMPRESSED_RUN = 0x40,
        SAGA_FRAME_EMPTY_RUN = 0xC0

    }
}

// var testFileDir = @"C:\Program Files (x86)\Steam\steamapps\common\IHNMAIMS\output";

// var testFiles = Directory.GetFiles(testFileDir, "*.bin");

//  foreach (var testFil in testFiles)
//  {
//    var testBytes2 = File.ReadAllBytes(testFil);

//    if (testBytes2.Length <= 1024 || (testBytes2[16] == 0x0 && testBytes2[17] == 0x0)) continue;
//    var outputName = Path.GetFileNameWithoutExtension(testFil);
//    File.WriteAllBytes(Path.Combine(testFileDir, "palettes", $"{outputName}_palette.bin"), testBytes2.Skip(0x8).Take(768).ToArray());
// //   var testData = testBytes.Skip(0x308).ToArray();

// //   var outputFolder = Path.Combine(Path.GetDirectoryName(testFile), "output");
// //   Directory.CreateDirectory(outputFolder);

// //   try {
// //     // first 4 bytes are width and height as uint16
// //     var width = BitConverter.ToUInt16(testBytes.Take(2).ToArray(), 0);
// //     var height = BitConverter.ToUInt16(testBytes.Skip(2).Take(2).ToArray(), 0);
// //     var decoded = DecodeBGImageRLE(testData, width, height);
// //     //File.WriteAllBytes(Path.Combine(outputFolder, $"{outputName}_decoded.bin"), decoded);
// //     var image = ImageFormatHelper.GenerateClutImage(testPal, decoded, width, height, true, 247, false);
// //     image.RotateFlip(RotateFlipType.RotateNoneFlipY);
// //     image.Save(Path.Combine(outputFolder, $"{outputName}.png"), ImageFormat.Png);
// //   } catch (Exception ex) {
// //     Console.WriteLine($"Error processing {testFile}: {ex.Message}");
// //   }
//  }


// var spriteTestFile = @"C:\Program Files (x86)\Steam\steamapps\common\IHNMAIMS\output\1168.bin";
// var spriteTestBytes = File.ReadAllBytes(spriteTestFile);
//var testFile = @"C:\Program Files (x86)\Steam\steamapps\common\IHNMAIMS\output\1205.bin";
//var testBytes = File.ReadAllBytes(testFile);
// var offsetCount = BitConverter.ToUInt16(spriteTestBytes.Take(2).ToArray());
//var spritePal = ColorHelper.ConvertBytesToRGB(testBytes.Skip(0x8).Take(768).ToArray(), 1);
// var offsets = new List<uint>();

// for (int i = 0; i < offsetCount; i++)
// {
//   var offset = BitConverter.ToUInt32(spriteTestBytes.Skip(2 + (i * 4)).Take(4).ToArray(), 0);
//   offsets.Add(offset);
// }

// var outputFolder = Path.Combine(Path.GetDirectoryName(spriteTestFile), "output_sprites", Path.GetFileNameWithoutExtension(spriteTestFile));
// Directory.CreateDirectory(outputFolder);

// for (int i = 0; i < offsets.Count; i++)
// {
//   var offset = offsets[i];
//   var nextOffset = (i == offsets.Count - 1) ? spriteTestBytes.Length : (int)offsets[i + 1];
//   var spriteData = spriteTestBytes.Skip((int)offset).Take(nextOffset - (int)offset).ToArray();
//   var offsetX = BitConverter.ToInt16(spriteData.Take(2).ToArray(), 0);
//   var offsetY = BitConverter.ToInt16(spriteData.Skip(2).Take(2).ToArray(), 0);
//   var width = BitConverter.ToUInt16(spriteData.Skip(4).Take(2).ToArray(), 0);
//   var height = BitConverter.ToUInt16(spriteData.Skip(6).Take(2).ToArray(), 0);
//   var imageData = DecodeRLEBuffer(spriteData.Skip(8).ToArray());
//   //File.WriteAllBytes(Path.Combine(outputFolder, $"{i}_decoded.bin"), imageData);
//   var spriteImage = ImageFormatHelper.GenerateClutImage(spritePal, imageData, width, height, true, 247, false);
//   spriteImage.RotateFlip(RotateFlipType.RotateNoneFlipY);
//   spriteImage.Save(Path.Combine(outputFolder, $"{i}_{offsetX}_{offsetY}.png"), ImageFormat.Png);
// }

// var animTestFile = @"C:\Program Files (x86)\Steam\steamapps\common\IHNMAIMS\output\1206.bin";
// var animTestBytes = File.ReadAllBytes(animTestFile).Skip(0xC).ToArray();

// var offsets = SAGAHelper.FillFrameOffsets(animTestBytes,10,true);

// var animOutputFolder = Path.Combine(Path.GetDirectoryName(animTestFile), "output_anim", Path.GetFileNameWithoutExtension(animTestFile));
// Directory.CreateDirectory(animOutputFolder);

// var frameImages = new List<Image>();

// foreach (var (offset, oIndex) in offsets.WithIndex())
// {
//   var decodedFrame = SAGAHelper.DecodeFrame(animTestBytes, offset,0x280,0x1e0);
//   //File.WriteAllBytes(Path.Combine(animOutputFolder, $"{oIndex}_decoded.bin"), decodedFrame);
//   var frameImage = ImageFormatHelper.GenerateClutImage(spritePal, decodedFrame, 0x280, 0x1e0, true, 247, false);
//   frameImage.Save(Path.Combine(animOutputFolder, $"{oIndex}.png"), ImageFormat.Png);
//   frameImages.Add(frameImage);
// }

// var bgImage = @"C:\Program Files (x86)\Steam\steamapps\common\IHNMAIMS\output\output\1205.png";

// ImageFormatHelper.CreateGifFromImageList(frameImages, Path.Combine(animOutputFolder, "1206.gif"), 10, 0, Image.FromFile(bgImage));


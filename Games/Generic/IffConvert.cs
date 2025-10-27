using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games.Generic
{
    public static class IffConvert
    {
        public static void ConvertPBM(string lbmInput, string pngOutputBasePath, bool rotatePalette = false, bool outputPalette = false)
        {
            // Use nullable reference types correctly
            BitmapHeader? bmhd = null;
            List<Color>? basePalette = null;
            byte[]? rawImageData = null;
            byte[]? decodedData = null;
            List<CrngInfo> colorRanges = new List<CrngInfo>();
            int imageWidth = 0;
            int imageHeight = 0;

            byte[] Decompress(byte[] compressedData, int finalLength)
            {
                if (compressedData == null)
                {
                    throw new ArgumentNullException(nameof(compressedData));
                }
                if (finalLength < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(finalLength), "Final length cannot be negative.");
                }
                if (finalLength == 0)
                {
                    return new byte[0]; // Handle zero length case
                }

                byte[] decompressedData = new byte[finalLength];
                int compressedIndex = 0;   // Current position in compressedData
                int decompressedIndex = 0; // Current position in decompressedData

                while (decompressedIndex < finalLength)
                {
                    // Ensure we can read the control byte
                    if (compressedIndex >= compressedData.Length)
                    {
                        throw new InvalidDataException($"Compressed data ended prematurely. Expected {finalLength} bytes, but decompression stopped at {decompressedIndex} bytes.");
                    }

                    byte value = compressedData[compressedIndex];

                    if (value > 128) // Run: Output the next byte (257 - value) times
                    {
                        // Check if we can read the data byte for the run
                        if (compressedIndex + 1 >= compressedData.Length)
                        {
                            throw new InvalidDataException("Compressed data ended unexpectedly after a run marker (> 128).");
                        }

                        byte dataByte = compressedData[compressedIndex + 1];
                        int runLength = 257 - value;

                        // Ensure the run doesn't exceed the final length
                        if (decompressedIndex + runLength > finalLength)
                        {
                            // Optional: Depending on strictness, you could truncate instead of throwing.
                            // runLength = finalLength - decompressedIndex;
                            // If truncating, ensure you don't over-read or loop infinitely.
                            // For correctness based on the description (loop until finalLength), exceeding it implies corrupt data.
                            throw new InvalidDataException($"Run length of {runLength} starting at index {decompressedIndex} would exceed final length {finalLength}.");
                        }

                        // Write the repeated byte
                        for (int i = 0; i < runLength; i++)
                        {
                            decompressedData[decompressedIndex + i] = dataByte;
                        }

                        decompressedIndex += runLength;
                        compressedIndex += 2; // Move past the control byte and the data byte
                    }
                    else if (value < 128) // Literal: Read and output the next (value + 1) bytes
                    {
                        int literalLength = value + 1;

                        // Check if there are enough bytes left in compressed data for the literal run
                        if (compressedIndex + 1 + literalLength > compressedData.Length)
                        {
                            throw new InvalidDataException($"Compressed data ended unexpectedly during literal read. Needed {literalLength} bytes after control byte {value} at index {compressedIndex}.");
                        }

                        // Ensure the literal data doesn't exceed the final length
                        if (decompressedIndex + literalLength > finalLength)
                        {
                            // Optional: Truncate as above, with similar caveats.
                            // literalLength = finalLength - decompressedIndex;
                            throw new InvalidDataException($"Literal length of {literalLength} starting at index {decompressedIndex} would exceed final length {finalLength}.");
                        }

                        // Copy the literal bytes
                        Array.Copy(compressedData, compressedIndex + 1, decompressedData, decompressedIndex, literalLength);

                        decompressedIndex += literalLength;
                        compressedIndex += (literalLength + 1); // Move past control byte and all literal bytes
                    }
                    else // Value == 128: Stop marker
                    {
                        compressedIndex++; // Consume the stop byte
                        break; // Exit the loop as requested
                    }
                }

                // Optional Check: Did we finish exactly at finalLength, or did we stop early due to marker 128?
                // The description implies stopping at 128 is valid, even if finalLength isn't reached.
                // If the requirement is *exactly* finalLength unless stopped by 128, this check is useful.
                // If the requirement is *always* exactly finalLength, you might throw here if (decompressedIndex < finalLength && value != 128).
                // For now, we return the array as is, which might be partially filled if 128 was hit early.
                // If the loop finished because decompressedIndex reached finalLength, but there's still compressed data,
                // that's usually considered valid (extra data ignored).

                // If the stop marker (128) caused an early exit and the requirement is to always return
                // an array of exactly finalLength, you might need to decide whether to pad the rest
                // (e.g., with zeros) or throw an error. The current code returns the partially filled array.

                return decompressedData;
            }

            using (var lbmReader = new BinaryReader(File.OpenRead(lbmInput)))
            {
                // --- File Reading Logic (mostly same as before, ensure BinaryReaderExtensions are used) ---
                if (lbmReader.BaseStream.Length < 12) { Console.Error.WriteLine("Error: File too small."); return; }

                var fourCC = Encoding.UTF8.GetString(lbmReader.ReadBytes(4));
                if (fourCC != "FORM") { Console.Error.WriteLine("Error: Not an IFF FORM file."); return; }

                /*var formSize =*/
                lbmReader.ReadBigEndianUInt32();
                var formType = Encoding.UTF8.GetString(lbmReader.ReadBytes(4));

                if (formType != "PBM ") { Console.Error.WriteLine("Error: FORM type is not PBM."); return; }

                while (lbmReader.BaseStream.Position < lbmReader.BaseStream.Length)
                {
                    string chunkType;
                    uint chunkSize;

                    // Handle potential padding before chunk ID
                    if (lbmReader.BaseStream.Position % 2 != 0)
                    {
                        if (lbmReader.BaseStream.Position < lbmReader.BaseStream.Length)
                            lbmReader.ReadByte(); // Read padding byte
                        else break; // End of stream after padding
                    }
                    if (lbmReader.BaseStream.Position + 8 > lbmReader.BaseStream.Length) // Check if enough space for ID+Size
                        break;

                    try
                    {
                        chunkType = Encoding.UTF8.GetString(lbmReader.ReadBytes(4));
                        if (string.IsNullOrWhiteSpace(chunkType) || chunkType == "\0\0\0\0") break;
                        chunkSize = lbmReader.ReadBigEndianUInt32();
                    }
                    catch (EndOfStreamException) { break; }

                    long currentChunkDataPos = lbmReader.BaseStream.Position;
                    long nextChunkPos = currentChunkDataPos + chunkSize;
                    // Basic check for invalid chunk size / stream position corruption
                    if (nextChunkPos < currentChunkDataPos || nextChunkPos > lbmReader.BaseStream.Length)
                    {
                        Console.Error.WriteLine($"Warning: Invalid chunk size ({chunkSize}) or position for chunk {chunkType}. Stopping parse.");
                        break;
                    }


                    // --- Process Chunks (BMHD, CMAP, CRNG, BODY) ---
                    switch (chunkType)
                    {
                        case "BMHD":
                            if (chunkSize >= 20) // Read only if size is valid
                            {
                                bmhd = new BitmapHeader(lbmReader.ReadBytes((int)chunkSize));
                                imageWidth = bmhd.Width;
                                imageHeight = bmhd.Height;
                            }
                            else
                            {
                                Console.Error.WriteLine($"Warning: Skipping BMHD chunk with unexpected size {chunkSize}.");
                                lbmReader.BaseStream.Seek(nextChunkPos, SeekOrigin.Begin);
                            }
                            break;

                        case "CMAP":
                            if (chunkSize > 0 && chunkSize <= lbmReader.BaseStream.Length - lbmReader.BaseStream.Position)
                            {
                                var paletteData = lbmReader.ReadBytes((int)chunkSize);
                                // Assuming ColorHelper exists and works
                                basePalette = ColorHelper.ConvertBytesToRGB(paletteData);
                            }
                            else
                            {
                                Console.Error.WriteLine($"Warning: Skipping CMAP chunk with invalid size {chunkSize}.");
                                lbmReader.BaseStream.Seek(nextChunkPos, SeekOrigin.Begin);
                            }
                            break;

                        case "CRNG":
                            // CRNG minimum size is 6 bytes (rate, flags, low, high) + 2 padding = 8 bytes total chunk structure?
                            // The CrngInfo constructor expects the reader positioned *after* ID/Size/Padding.
                            // Let's adjust CrngInfo constructor or reading logic here.
                            // Assuming CrngInfo constructor reads: padding(2), rate(2), flags(2), low(1), high(1) = 8 bytes
                            if (chunkSize >= 8) // Check against expected size for CRNG data fields
                            {
                                try
                                {
                                    // Pass the reader, CrngInfo constructor will read its fields
                                    colorRanges.Add(new CrngInfo(lbmReader));
                                }
                                catch (Exception ex)
                                {
                                    Console.Error.WriteLine($"Warning: Could not parse CRNG chunk data. Skipping. Error: {ex.Message}");
                                    lbmReader.BaseStream.Seek(nextChunkPos, SeekOrigin.Begin);
                                }
                            }
                            else
                            {
                                Console.Error.WriteLine($"Warning: Skipping CRNG chunk with unexpected size {chunkSize}.");
                                lbmReader.BaseStream.Seek(nextChunkPos, SeekOrigin.Begin);
                            }
                            break;

                        case "BODY":
                            if (chunkSize > 0 && chunkSize <= lbmReader.BaseStream.Length - lbmReader.BaseStream.Position)
                            {
                                rawImageData = lbmReader.ReadBytes((int)chunkSize); // Store raw data
                            }
                            else
                            {
                                Console.Error.WriteLine($"Warning: Skipping BODY chunk with invalid size {chunkSize}.");
                                lbmReader.BaseStream.Seek(nextChunkPos, SeekOrigin.Begin);
                            }
                            break;

                        default:
                            // Skip other unknown or unhandled chunks
                            lbmReader.BaseStream.Seek(nextChunkPos, SeekOrigin.Begin);
                            break;
                    }

                    // Ensure we are positioned correctly for the next chunk ID read,
                    // even if a chunk processor didn't read exactly chunkSize bytes (e.g. partial read on error).
                    // This Seek handles moving past unread data or skipping unknown chunks correctly.
                    if (lbmReader.BaseStream.Position != nextChunkPos)
                    {
                        // Check position before seeking to avoid issues if already past the end
                        if (nextChunkPos <= lbmReader.BaseStream.Length)
                        {
                            lbmReader.BaseStream.Seek(nextChunkPos, SeekOrigin.Begin);
                        }
                        else
                        {
                            // If calculated position is invalid, seek to end and break.
                            Console.Error.WriteLine($"Warning: Calculated next chunk position ({nextChunkPos}) is invalid. Stopping parse.");
                            lbmReader.BaseStream.Seek(0, SeekOrigin.End);
                            break;
                        }
                    }

                } // End while loop reading chunks
            } // End using BinaryReader

            // --- Post-processing and Image Generation ---

            // ** Crucial Null Checks **
            if (bmhd == null || basePalette == null || rawImageData == null)
            {
                Console.Error.WriteLine("Error: Missing required chunks (BMHD, CMAP, or BODY) in LBM file. Cannot generate image.");
                return;
            }

            // Decompress the image data (only needs to be done once)
            try
            {
                if (bmhd.CompressionType == 1) // ByteRun1 RLE
                {
                    int expectedSize = imageWidth * imageHeight;
                    decodedData = Decompress(rawImageData, expectedSize);
                }
                else if (bmhd.CompressionType == 0) // Uncompressed
                {
                    decodedData = rawImageData;
                    if (decodedData.Length != imageWidth * imageHeight)
                    {
                        Console.Error.WriteLine($"Warning: Uncompressed BODY size ({decodedData.Length}) doesn't match expected ({imageWidth * imageHeight}). Image may be corrupt or truncated.");
                        // Attempt to resize/pad - might cause issues but better than failing?
                        Array.Resize(ref decodedData, imageWidth * imageHeight);
                    }
                }
                else
                {
                    Console.Error.WriteLine($"Error: Unsupported compression type: {bmhd.CompressionType}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error during decompression: {ex.Message}");
                return;
            }

            if (decodedData == null) // Check after decompression attempt
            {
                Console.Error.WriteLine("Error: Image data could not be decoded or was missing.");
                return;
            }


            // --- Determine Frame Count using LCM ---
            var activeCycleLengths = colorRanges
                .Where(crng => crng.Low > 0 && crng.CycleLength > 1)
                .Select(crng => crng.CycleLength)
                .ToList();

            int numFrames = 1;
            if (rotatePalette && activeCycleLengths.Any())
            {
                numFrames = MathHelpers.CalculateLcmOfList(activeCycleLengths);
                // Optional: Cap the number of frames if LCM becomes excessively large
                const int MAX_FRAMES = 120; // Example limit
                if (numFrames > MAX_FRAMES)
                {
                    Console.WriteLine($"Warning: Calculated frame count ({numFrames}) exceeds limit ({MAX_FRAMES}). Clamping.");
                    numFrames = MAX_FRAMES;
                }
            }


            Console.WriteLine($"Generating {numFrames} frame(s)..."); // Info message

            string outputDirectory = Path.Combine(Path.GetDirectoryName(pngOutputBasePath)!, "output", Path.GetFileNameWithoutExtension(lbmInput)) ?? "."; // Handle null dir
            Directory.CreateDirectory(outputDirectory); // Ensure output directory exists
            string outputFileName = Path.GetFileNameWithoutExtension(pngOutputBasePath);
            string outputExtension = Path.GetExtension(pngOutputBasePath);
            if (string.IsNullOrEmpty(outputExtension)) outputExtension = ".png";


            // Generate each frame
            for (int frame = 0; frame < numFrames; frame++)
            {
                // **Create a working copy of the palette for THIS frame**
                // Important: Start from the original basePalette each time!
                List<Color> currentPalette = new List<Color>(basePalette);

                // **Apply ALL active CRNG rotations for the current frame step**
                foreach (var crng in colorRanges)
                {
                    if (rotatePalette && crng.CycleLength > 0) // CycleLength > 0 check is technically redundant if > 1 used for LCM
                    {
                        // Rotate the currentPalette IN PLACE for this specific range
                        // The step calculation (frame % crng.CycleLength) ensures each range cycles correctly relative to its own length
                        ImageFormatHelper.RotatePaletteRange(currentPalette, crng.Low, crng.High, crng.ReverseDirection, frame % crng.CycleLength);
                    }
                }

                // Generate the bitmap using the current frame's calculated palette
                Bitmap? image = null; // Use nullable Bitmap
                try
                {
                    // GenerateClutImage MUST handle potential data length mismatches if they weren't fatal earlier
                    image = (Bitmap)ImageFormatHelper.GenerateClutImage(currentPalette, decodedData, imageWidth, imageHeight, outputFileName.Contains("parch"));

                    // Construct the output filename for this frame
                    string frameOutputPath;
                    if (numFrames > 1 && rotatePalette)
                    {
                        int numDigits = (int)Math.Log10(numFrames - 1) + 1; // Calculate padding digits correctly
                        if (numFrames == 1) numDigits = 1; // Handle log10(0) edge case
                        string frameNumber = frame.ToString().PadLeft(numDigits, '0');
                        frameOutputPath = Path.Combine(outputDirectory, $"{outputFileName}_{frameNumber}{outputExtension}");
                    }
                    else
                    {
                        frameOutputPath = Path.Combine(outputDirectory, $"{outputFileName}{outputExtension}");
                    }
                    if (outputPalette)
                    {
                        var paletteImage = ColorHelper.CreateLabelledPalette(currentPalette);
                        // Save the palette image as a separate file (optional)
                        string paletteOutputPath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(frameOutputPath)}_palette.png");
                        paletteImage.Save(paletteOutputPath, ImageFormat.Png);
                    }
                    // Save the image
                    image.Save(frameOutputPath, ImageFormat.Png);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error generating or saving frame {frame}: {ex.Message}");
                    // Optionally break or continue
                }
                finally
                {
                    image?.Dispose(); // Dispose if image was created
                }
            }
            Console.WriteLine("Finished generating frames.");
        }

        public static void ConvertILBM(string ilbmFile, string pngOutputBasePath)
        {
            
        }

    }

    class CrngInfo
    {
        public short Rate { get; private set; }
        public ushort Flags { get; private set; }
        public byte Low { get; private set; }
        public byte High { get; private set; }

        public bool IsActive => (Flags & 0b1) != 0;
        public bool ReverseDirection => (Flags & 0b10) != 0;
        public int CycleLength => (High >= Low) ? (High - Low + 1) : 0; // Only return length if active

        public CrngInfo(BinaryReader reader)
        {
            // Read the 8 bytes of data for CRNG chunk (as per Deluxe Paint spec image)
            // Padding (2 bytes) - Read and ignore
            reader.ReadUInt16(); // Use ReadUInt16, assumes it handles endianness or we don't care about padding value

            // Rate (INT16BE)
            Rate = reader.ReadBigEndianInt16();

            // Flags (INT16BE) - Note: Spec image says INT16BE, not UINT
            Flags = reader.ReadBigEndianUInt16(); // Assuming unsigned interpretation is okay based on bit flags

            // Low (UINT8)
            Low = reader.ReadByte();

            // High (UINT8)
            High = reader.ReadByte();
        }
    }

    class BitmapHeader
    {
        public ushort Width { get; set; }
        public ushort Height { get; set; }
        public ushort OriginX { get; set; }
        public ushort OriginY { get; set; }
        public byte NumBitPlanes { get; set; }
        public byte MaskValue { get; set; }
        public byte CompressionType { get; set; }
        public byte Padding { get; set; }
        public ushort TransparentColor { get; set; }
        public byte AspectX { get; set; }
        public byte AspectY { get; set; }
        public ushort PageWidth { get; set; }
        public ushort PageHeight { get; set; }

        public BitmapHeader() { }

        public BitmapHeader(byte[] header)
        {
            if (header.Length != 0x14)
            {
                throw new ArgumentException("Header length must be 14 bytes.");
            }
            Width = BitConverter.ToUInt16(header.Skip(0).Take(2).Reverse().ToArray());
            Height = BitConverter.ToUInt16(header.Skip(2).Take(2).Reverse().ToArray());
            OriginX = BitConverter.ToUInt16(header.Skip(4).Take(2).Reverse().ToArray());
            OriginY = BitConverter.ToUInt16(header.Skip(6).Take(2).Reverse().ToArray());
            NumBitPlanes = header[8];
            MaskValue = header[9];
            CompressionType = header[10];
            Padding = header[11];
            TransparentColor = BitConverter.ToUInt16(header.Skip(12).Take(2).Reverse().ToArray());
            AspectX = header[14];
            AspectY = header[15];
            PageWidth = BitConverter.ToUInt16(header.Skip(16).Take(2).Reverse().ToArray());
            PageHeight = BitConverter.ToUInt16(header.Skip(18).Take(2).Reverse().ToArray());
        }
    }


}

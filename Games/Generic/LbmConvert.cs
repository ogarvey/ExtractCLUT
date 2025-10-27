#nullable enable

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
namespace ExtractCLUT.Games.Generic
{

    /// <summary>
    /// A converter to read Commodore Amiga LBM (ILBM/PBM) image files and convert them to PNG format.
    /// This class uses the SixLabors.ImageSharp library for PNG creation.
    /// </summary>
    public class LbmConverter
    {
        // Inner records to represent LBM chunk data in a structured way.
        private record BitmapHeader(
            ushort Width, ushort Height, short XOrigin, short YOrigin,
            byte NumPlanes, byte Masking, byte Compression,
            ushort TransparentColor, byte XAspect, byte YAspect,
            short PageWidth, short PageHeight);

        private record ColorMapEntry(byte R, byte G, byte B);
        private record LbmParseResult(BitmapHeader? Header, IReadOnlyList<ColorMapEntry>? Palette, uint? AmigaMode, byte[]? Body, string? FormatId);
        private record ValidLbmData(BitmapHeader Header, IReadOnlyList<ColorMapEntry>? Palette, uint? AmigaMode, byte[] Body, string FormatId);
        /// <summary>
        /// Converts an LBM image from a source stream to a PNG image in a destination stream.
        /// </summary>
        /// <param name="lbmStream">The input stream containing the LBM file data.</param>
        /// <param name="pngStream">The output stream where the PNG file will be written.</param>
        public async Task ConvertAsync(Stream lbmStream, Stream pngStream)
        {
            // 1. Parse the LBM file structure and its chunks (now a synchronous operation).
            var lbm = ParseLbm(lbmStream);
            if (lbm.Header == null || lbm.Body == null || lbm.FormatId == null)
            {
                throw new InvalidDataException("LBM file is missing a required BMHD, BODY, or FORM chunk.");
            }

            // Create a new tuple with non-nullable types that the compiler can verify.
            var validLbmData = new ValidLbmData(lbm.Header, lbm.Palette, lbm.AmigaMode, lbm.Body, lbm.FormatId);

            // 2. Decode the pixel data based on the format's properties.
            var image = DecodePixels(validLbmData);

            // 3. Save the decoded image as a PNG (this remains async).
            await image.SaveAsPngAsync(pngStream);
        }

        public Image<Rgba32> ConvertToImage(Stream lbmStream)
        {
            // 1. Parse the LBM file structure and its chunks (now a synchronous operation).
            var lbm = ParseLbm(lbmStream);
            if (lbm.Header == null || lbm.Body == null || lbm.FormatId == null)
            {
                throw new InvalidDataException("LBM file is missing a required BMHD, BODY, or FORM chunk.");
            }

            // Create a new tuple with non-nullable types that the compiler can verify.
            var validLbmData = new ValidLbmData(lbm.Header, lbm.Palette, lbm.AmigaMode, lbm.Body, lbm.FormatId);

            // 2. Decode the pixel data based on the format's properties.
            var image = DecodePixels(validLbmData);
            return image;
        }

        private static LbmParseResult ParseLbm(Stream stream)
        {
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

            if (ReadString(reader, 4) != "FORM") throw new InvalidDataException("Not a valid IFF file.");

            var fileLength = ReadUInt32BigEndian(reader);
            var formatId = ReadString(reader, 4);
            if (formatId != "ILBM" && formatId != "PBM ")
            {
                throw new InvalidDataException($"Unsupported IFF format: {formatId}. Only ILBM and PBM are supported.");
            }

            BitmapHeader? header = null;
            IReadOnlyList<ColorMapEntry>? palette = null;
            uint? amigaMode = null;
            byte[]? body = null;

            long readBytes = 4; // Already read formatId
            while (readBytes < fileLength && stream.Position < stream.Length)
            {
                var chunkId = ReadString(reader, 4);
                var chunkLength = ReadUInt32BigEndian(reader);
                readBytes += 8;

                if (readBytes + chunkLength > fileLength + 8)
                {
                    // Avoid reading past the end of the file based on FORM length
                    break;
                }

                var chunkData = reader.ReadBytes((int)chunkLength);
                readBytes += chunkLength;

                // Chunks must be word-aligned. If length is odd, skip a padding byte.
                if (chunkLength % 2 != 0)
                {
                    if (stream.Position < stream.Length)
                    {
                        reader.ReadByte();
                        readBytes++;
                    }
                }

                var chunkSpan = new ReadOnlySpan<byte>(chunkData);

                switch (chunkId)
                {
                    case "BMHD":
                        header = ParseBitmapHeader(chunkSpan);
                        break;
                    case "CMAP":
                        palette = ParseColorMap(chunkSpan);
                        break;
                    case "CAMG":
                        amigaMode = BinaryPrimitives.ReadUInt32BigEndian(chunkSpan);
                        break;
                    case "BODY":
                        body = chunkData;
                        break;
                }
            }

            return new LbmParseResult(header, palette, amigaMode, body, formatId);
        }



        private static Image<Rgba32> DecodePixels(ValidLbmData lbm)
        {
            var header = lbm.Header;
            var image = new Image<Rgba32>(header.Width, header.Height);
            var palette = lbm.Palette ?? CreateDefaultPalette(1 << (header.NumPlanes == 0 ? 8 : header.NumPlanes));

            if (lbm.FormatId == "PBM ")
            {
                // ** FIX START: New, robust logic for PBM format **
                byte[] pixelData;
                // 1. Handle potential compression
                if (header.Compression == 1)
                {
                    pixelData = DecompressPbmBody(lbm.Body);
                }
                else
                {
                    pixelData = lbm.Body;
                }

                // 2. Calculate the stride (row width in bytes), padded to an even number
                int stride = (header.Width % 2 == 0) ? header.Width : header.Width + 1;

                // 3. Loop using the correct stride to find pixel offsets
                for (int y = 0; y < header.Height; y++)
                {
                    for (int x = 0; x < header.Width; x++)
                    {
                        int offset = y * stride + x;
                        if (offset >= pixelData.Length) break;

                        int colorIndex = pixelData[offset];
                        if (colorIndex >= palette.Count) continue;

                        var paletteColor = palette[colorIndex];
                        image[x, y] = colorIndex == 0 ? new Rgba32(0, 0, 0, 0) : new Rgba32(paletteColor.R, paletteColor.G, paletteColor.B, 255);
                    }
                }
                // ** FIX END **
            }
            else // ILBM format
            {
                bool isEhb = (lbm.AmigaMode & 0x80) != 0;
                if (isEhb) palette = CreateEhbPalette(palette);

                int bytesPerRowPerPlane = (header.Width + 15) / 16 * 2;
                int totalPlanesInData = header.NumPlanes + (header.Masking == 1 ? 1 : 0);
                int uncompressedRowSize = totalPlanesInData * bytesPerRowPerPlane;

                var compressedSpan = new ReadOnlySpan<byte>(lbm.Body);
                int compressedOffset = 0;

                for (int y = 0; y < header.Height; y++)
                {
                    ReadOnlySpan<byte> rowData;
                    if (header.Compression == 1)
                    {
                        var (decompressedRow, bytesConsumed) = DecompressIlbmRow(compressedSpan.Slice(compressedOffset), uncompressedRowSize);
                        compressedOffset += bytesConsumed;
                        rowData = decompressedRow;
                    }
                    else
                    {
                        rowData = compressedSpan.Slice(y * uncompressedRowSize, uncompressedRowSize);
                    }

                    for (int x = 0; x < header.Width; x++)
                    {
                        int colorIndex = GetPixelBitplaneValue(rowData, x, 0, header.NumPlanes, bytesPerRowPerPlane);
                        if (colorIndex >= palette.Count) continue;

                        var paletteColor = palette[colorIndex];
                        byte alpha = 255;

                        if (header.Masking == 1)
                        {
                            int maskBit = GetPixelBitplaneValue(rowData, x, 0, 1, bytesPerRowPerPlane, planeStartIndex: header.NumPlanes);
                            alpha = (byte)(maskBit == 1 ? 255 : 0);
                        }
                        else if (header.Masking == 2 && colorIndex == header.TransparentColor)
                        {
                            alpha = 0;
                        }

                        image[x, y] = new Rgba32(paletteColor.R, paletteColor.G, paletteColor.B, alpha);
                    }
                }
            }
            return image;
        }

        private static byte[] DecompressPbmBody(ReadOnlySpan<byte> compressedData)
        {
            var decompressed = new List<byte>();
            int compressedPos = 0;
            while (compressedPos < compressedData.Length)
            {
                sbyte code = (sbyte)compressedData[compressedPos++];
                if (code >= 0)
                {
                    int count = code + 1;
                    for (int i = 0; i < count; i++) { if (compressedPos < compressedData.Length) decompressed.Add(compressedData[compressedPos++]); }
                }
                else if (code > -128)
                {
                    int count = 1 - code;
                    if (compressedPos < compressedData.Length)
                    {
                        byte value = compressedData[compressedPos++];
                        for (int i = 0; i < count; i++) { decompressed.Add(value); }
                    }
                }
            }
            return decompressed.ToArray();
        }

        private static (byte[] decompressedRow, int bytesRead) DecompressIlbmRow(ReadOnlySpan<byte> compressedData, int uncompressedSize)
        {
            var decompressed = new byte[uncompressedSize];
            int decompressedPos = 0;
            int compressedPos = 0;
            while (decompressedPos < uncompressedSize && compressedPos < compressedData.Length)
            {
                sbyte code = (sbyte)compressedData[compressedPos++];
                if (code >= 0)
                {
                    int count = code + 1;
                    for (int i = 0; i < count; i++) { if (decompressedPos < uncompressedSize && compressedPos < compressedData.Length) decompressed[decompressedPos++] = compressedData[compressedPos++]; }
                }
                else if (code > -128)
                {
                    int count = 1 - code;
                    if (compressedPos < compressedData.Length)
                    {
                        byte value = compressedData[compressedPos++];
                        for (int i = 0; i < count; i++) { if (decompressedPos < uncompressedSize) decompressed[decompressedPos++] = value; }
                    }
                }
            }
            return (decompressed, compressedPos);
        }
        
        private static (byte[] decompressedRow, int bytesRead) DecompressRow(ReadOnlySpan<byte> compressedData, int uncompressedSize)
        {
            var decompressed = new byte[uncompressedSize];
            int decompressedPos = 0;
            int compressedPos = 0;

            while (decompressedPos < uncompressedSize && compressedPos < compressedData.Length)
            {
                sbyte code = (sbyte)compressedData[compressedPos++];
                if (code >= 0) // Literal run
                {
                    int count = code + 1;
                    for (int i = 0; i < count; i++)
                    {
                        decompressed[decompressedPos++] = compressedData[compressedPos++];
                    }
                }
                else if (code > -128) // Repeat run
                {
                    int count = 1 - code;
                    byte value = compressedData[compressedPos++];
                    for (int i = 0; i < count; i++)
                    {
                        decompressed[decompressedPos++] = value;
                    }
                }
                // else code == -128 is a no-op
            }
            return (decompressed, compressedPos);
        }
        // Renamed from GetColorIndex to be more generic, as it can now get mask data too.
        private static int GetPixelBitplaneValue(ReadOnlySpan<byte> body, int x, int rowOffset, int numPlanesToRead, int bytesPerRowPerPlane, int planeStartIndex = 0)
        {
            int finalValue = 0;
            int byteIndex = x / 8;
            int bitIndex = 7 - (x % 8);

            for (int p = 0; p < numPlanesToRead; p++)
            {
                int currentPlane = planeStartIndex + p;
                int planeOffset = rowOffset + (currentPlane * bytesPerRowPerPlane);
                if (planeOffset + byteIndex >= body.Length) return 0; // bounds check

                byte pixelByte = body[planeOffset + byteIndex];
                int bit = (pixelByte >> bitIndex) & 1;
                finalValue |= (bit << p);
            }
            return finalValue;
        }

        private static Rgba32 DecodeHamPixel(ReadOnlySpan<byte> body, int x, int rowOffset, BitmapHeader header, int bytesPerRowPerPlane, IReadOnlyList<ColorMapEntry> palette, Rgba32 previousPixel)
        {
            int value = GetPixelBitplaneValue(body, x, rowOffset, header.NumPlanes, bytesPerRowPerPlane);

            int numDataPlanes = header.NumPlanes - 2;
            int control = value >> numDataPlanes;
            int data = value & ((1 << numDataPlanes) - 1);

            var currentPixel = (x == 0) ? new Rgba32(0, 0, 0) : previousPixel;

            switch (control)
            {
                case 0b00: // Use palette
                    var paletteColor = palette[data];
                    return new Rgba32(paletteColor.R, paletteColor.G, paletteColor.B);
                case 0b01: // Modify Blue
                    currentPixel.B = (byte)((data << 4) | data);
                    return currentPixel;
                case 0b10: // Modify Red
                    currentPixel.R = (byte)((data << 4) | data);
                    return currentPixel;
                case 0b11: // Modify Green
                    currentPixel.G = (byte)((data << 4) | data);
                    return currentPixel;
                default:
                    return currentPixel;
            }
        }

        private static byte[] DecompressBody(ReadOnlySpan<byte> compressed, BitmapHeader header)
        {
            int totalPlanes = header.NumPlanes + (header.Masking == 1 ? 1 : 0);
            var bytesPerRow = (header.Width + 15) / 16 * 2 * totalPlanes;
            var decompressed = new List<byte>(bytesPerRow * header.Height);
            int compressedPos = 0;

            while (compressedPos < compressed.Length)
            {
                sbyte code = (sbyte)compressed[compressedPos++];
                if (code >= 0) // Literal run
                {
                    int count = code + 1;
                    for (int j = 0; j < count; j++)
                    {
                        if (compressedPos >= compressed.Length) break;
                        decompressed.Add(compressed[compressedPos++]);
                    }
                }
                else if (code > -128) // Repeat run
                {
                    if (compressedPos >= compressed.Length) break;
                    int count = 1 - code;
                    byte value = compressed[compressedPos++];
                    for (int j = 0; j < count; j++)
                    {
                        decompressed.Add(value);
                    }
                }
                // else code == -128 is a no-op, just continue
            }
            return decompressed.ToArray();
        }

        #region Chunk Parsers & Helpers

        private static BitmapHeader ParseBitmapHeader(ReadOnlySpan<byte> data)
        {
            return new BitmapHeader(
                Width: BinaryPrimitives.ReadUInt16BigEndian(data[0..]),
                Height: BinaryPrimitives.ReadUInt16BigEndian(data[2..]),
                XOrigin: BinaryPrimitives.ReadInt16BigEndian(data[4..]),
                YOrigin: BinaryPrimitives.ReadInt16BigEndian(data[6..]),
                NumPlanes: data[8],
                Masking: data[9],
                Compression: data[10],
                TransparentColor: BinaryPrimitives.ReadUInt16BigEndian(data[12..]),
                XAspect: data[14],
                YAspect: data[15],
                PageWidth: BinaryPrimitives.ReadInt16BigEndian(data[16..]),
                PageHeight: BinaryPrimitives.ReadInt16BigEndian(data[18..])
            );
        }

        private static IReadOnlyList<ColorMapEntry> ParseColorMap(ReadOnlySpan<byte> data)
        {
            int colorCount = data.Length / 3;
            var palette = new List<ColorMapEntry>(colorCount);
            for (int i = 0; i < colorCount; i++)
            {
                int offset = i * 3;
                palette.Add(new ColorMapEntry(data[offset], data[offset + 1], data[offset + 2]));
            }
            return palette;
        }

        private static IReadOnlyList<ColorMapEntry> CreateEhbPalette(IReadOnlyList<ColorMapEntry> basePalette)
        {
            var fullPalette = new List<ColorMapEntry>(64);
            foreach (var color in basePalette) fullPalette.Add(color);
            foreach (var color in basePalette)
            {
                fullPalette.Add(new ColorMapEntry((byte)(color.R >> 1), (byte)(color.G >> 1), (byte)(color.B >> 1)));
            }
            while (fullPalette.Count < 64) fullPalette.Add(new ColorMapEntry(0, 0, 0));

            return fullPalette;
        }

        private static IReadOnlyList<ColorMapEntry> CreateDefaultPalette(int size)
        {
            var palette = new List<ColorMapEntry>(size);
            for (int i = 0; i < size; i++)
            {
                byte intensity = (size <= 1) ? (byte)255 : (byte)((i * 255) / (size - 1));
                palette.Add(new ColorMapEntry(intensity, intensity, intensity));
            }
            return palette;
        }

        private static string ReadString(BinaryReader reader, int length)
        {
            return Encoding.ASCII.GetString(reader.ReadBytes(length));
        }

        private static uint ReadUInt32BigEndian(BinaryReader reader)
        {
            Span<byte> buffer = stackalloc byte[4];
            reader.Read(buffer);
            return BinaryPrimitives.ReadUInt32BigEndian(buffer);
        }

        #endregion
    }
}

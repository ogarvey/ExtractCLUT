using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;

namespace ExtractCLUT.Games.Generic
{
    public class AnimConvert
    {
        private record BitmapHeader(
            ushort Width,
            ushort Height,
            short XOrigin,
            short YOrigin,
            byte NumPlanes,
            byte Masking,
            byte Compression,
            ushort TransparentColor,
            byte XAspect,
            byte YAspect,
            short PageWidth,
            short PageHeight);

        private record AnimHeader(byte Operation, byte Interleave, uint Bits);

        private sealed class AnimFrame
        {
            public string FrameFormat { get; set; } = "PBM ";
            public bool HasDpan { get; set; }
            public BitmapHeader? BitmapHeader { get; set; }
            public AnimHeader? AnimHeader { get; set; }
            public byte[]? Body { get; set; }
            public byte[]? Delta { get; set; }
            public byte[]? ColorMap { get; set; }
            public uint? AmigaMode { get; set; }
        }

        public async Task ExtractFramesAsync(string animFilePath, string outputFolder)
        {
            if (!File.Exists(animFilePath))
            {
                throw new FileNotFoundException("ANIM file was not found.", animFilePath);
            }

            Directory.CreateDirectory(outputFolder);

            byte[] fileData = await File.ReadAllBytesAsync(animFilePath);
            if (fileData.Length < 12 || ReadString(fileData, 0, 4) != "FORM")
            {
                throw new InvalidDataException("Not a valid IFF/ANIM file.");
            }

            uint formLength = ReadUInt32BigEndian(fileData, 4);
            string formatId = ReadString(fileData, 8, 4);
            if (formatId != "ANIM")
            {
                throw new InvalidDataException($"Unsupported IFF format: {formatId}. Expected ANIM.");
            }

            int formEnd = Math.Min(fileData.Length, 8 + (int)formLength);
            var frames = ParseAnimFrames(fileData, 12, formEnd);
            if (frames.Count == 0)
            {
                throw new InvalidDataException("No PBM/ILBM frames were found in ANIM file.");
            }

            string baseFrameFormat = frames[0].FrameFormat;
            BitmapHeader baseHeader = frames[0].BitmapHeader
                ?? throw new InvalidDataException("First frame is missing BMHD chunk.");

            if (baseHeader.NumPlanes == 0)
            {
                throw new InvalidDataException("Unsupported BMHD: NumPlanes is zero.");
            }

            int frameSize = GetUncompressedFrameSize(baseHeader, baseFrameFormat);

            byte[]? currentColorMap = frames[0].ColorMap;
            uint? currentAmigaMode = frames[0].AmigaMode;
            bool isBrushAnim = frames.Exists(f => f.HasDpan);
            var decodedFrames = new List<byte[]>(frames.Count);

            for (int frameIndex = 0; frameIndex < frames.Count; frameIndex++)
            {
                AnimFrame frame = frames[frameIndex];

                if (frame.ColorMap != null)
                {
                    currentColorMap = frame.ColorMap;
                }

                if (frame.AmigaMode.HasValue)
                {
                    currentAmigaMode = frame.AmigaMode;
                }

                byte[] decoded;
                byte operation = frame.AnimHeader?.Operation ?? 0;
                string frameFormat = frame.FrameFormat;

                if (frame.Body != null)
                {
                    BitmapHeader sourceHeader = frame.BitmapHeader ?? baseHeader;
                    decoded = DecodeBodyToFrameBuffer(sourceHeader, frame.Body, baseFrameFormat, baseHeader.NumPlanes);
                }
                else if (frame.Delta != null)
                {
                    int interleave = frame.AnimHeader?.Interleave ?? 0;
                    if (isBrushAnim && interleave == 0)
                    {
                        interleave = 1;
                    }

                    int relative = interleave == 0 ? 2 : interleave;
                    int sourceIndex = frameIndex - relative;
                    if (sourceIndex < 0)
                    {
                        sourceIndex = Math.Max(0, frameIndex - 1);
                    }

                    if (sourceIndex < 0 || sourceIndex >= decodedFrames.Count)
                    {
                        throw new InvalidDataException($"Unable to resolve source frame for delta frame {frameIndex}.");
                    }

                    decoded = new byte[frameSize];
                    Buffer.BlockCopy(decodedFrames[sourceIndex], 0, decoded, 0, frameSize);

                    uint bits = frame.AnimHeader?.Bits ?? 0;
                    byte effectiveOperation = operation;
                    if (isBrushAnim && operation == 75)
                    {
                        effectiveOperation = 5;
                    }

                    bool xor = (bits & 0x2) != 0;
                    if (isBrushAnim && (effectiveOperation == 5 || effectiveOperation == 6))
                    {
                        xor = true;
                    }

                    ApplyDelta(decoded, frame.Delta, baseHeader, effectiveOperation, bits, xor, baseFrameFormat, isBrushAnim);
                }
                else
                {
                    throw new InvalidDataException($"Frame {frameIndex} contains neither BODY nor DLTA data.");
                }

                decodedFrames.Add(decoded);

                byte[] pbmBytes = BuildLbmFromBuffer(baseHeader, decoded, currentColorMap, currentAmigaMode, baseFrameFormat);
                using var pbmStream = new MemoryStream(pbmBytes, writable: false);
                var lbmConverter = new LbmConverter();
                using var image = lbmConverter.ConvertToImage(pbmStream);
                await image.SaveAsPngAsync(Path.Combine(outputFolder, $"frame_{frameIndex:D4}.png"));
            }
        }

        private static List<AnimFrame> ParseAnimFrames(byte[] fileData, int startOffset, int endOffset)
        {
            var frames = new List<AnimFrame>();
            int offset = startOffset;

            while (offset + 8 <= endOffset)
            {
                string chunkId = ReadString(fileData, offset, 4);
                int chunkLength = (int)ReadUInt32BigEndian(fileData, offset + 4);
                int dataStart = offset + 8;
                int dataEnd = dataStart + chunkLength;

                if (dataEnd > fileData.Length || dataEnd > endOffset)
                {
                    break;
                }

                if (chunkId == "FORM" && chunkLength >= 4)
                {
                    string frameFormat = ReadString(fileData, dataStart, 4);
                    if (frameFormat == "PBM " || frameFormat == "ILBM")
                    {
                        frames.Add(ParseAnimFrame(fileData, dataStart + 4, dataEnd, frameFormat));
                    }
                }

                offset = dataEnd + (chunkLength & 1);
            }

            return frames;
        }

        private static AnimFrame ParseAnimFrame(byte[] fileData, int startOffset, int endOffset, string frameFormat)
        {
            var frame = new AnimFrame
            {
                FrameFormat = frameFormat
            };
            int offset = startOffset;

            while (offset + 8 <= endOffset)
            {
                string chunkId = ReadString(fileData, offset, 4);
                int chunkLength = (int)ReadUInt32BigEndian(fileData, offset + 4);
                int dataStart = offset + 8;
                int dataEnd = dataStart + chunkLength;

                if (dataEnd > fileData.Length || dataEnd > endOffset)
                {
                    break;
                }

                switch (chunkId)
                {
                    case "BMHD":
                        if (chunkLength >= 20)
                        {
                            frame.BitmapHeader = ParseBitmapHeader(fileData, dataStart);
                        }
                        break;

                    case "ANHD":
                        if (chunkLength >= 26)
                        {
                            frame.AnimHeader = ParseAnimHeader(fileData, dataStart);
                        }
                        break;

                    case "CMAP":
                        frame.ColorMap = CopyBytes(fileData, dataStart, chunkLength);
                        break;

                    case "CAMG":
                        if (chunkLength >= 4)
                        {
                            frame.AmigaMode = ReadUInt32BigEndian(fileData, dataStart);
                        }
                        break;

                    case "DPAN":
                        frame.HasDpan = true;
                        break;

                    case "BODY":
                        frame.Body = CopyBytes(fileData, dataStart, chunkLength);
                        break;

                    case "DLTA":
                        frame.Delta = CopyBytes(fileData, dataStart, chunkLength);
                        break;
                }

                offset = dataEnd + (chunkLength & 1);
            }

            return frame;
        }

        private static int GetUncompressedFrameSize(BitmapHeader header, string frameFormat)
        {
            if (frameFormat == "PBM ")
            {
                int stride = GetPbmStride(header.Width);
                return stride * header.Height;
            }

            int planePitch = GetPlanePitch(header.Width);
            return planePitch * header.Height * header.NumPlanes;
        }

        private static byte[] DecodeBodyToFrameBuffer(BitmapHeader header, byte[] bodyData, string frameFormat, byte targetNumPlanes)
        {
            if (frameFormat == "PBM ")
            {
                int expectedSize = GetPbmStride(header.Width) * header.Height;
                return DecodePbmBody(bodyData, header.Compression, expectedSize);
            }

            return DecodeBodyToPlanar(header, bodyData, targetNumPlanes);
        }

        private static byte[] DecodePbmBody(byte[] bodyData, byte compression, int expectedSize)
        {
            if (compression == 0)
            {
                if (bodyData.Length < expectedSize)
                {
                    throw new InvalidDataException("PBM BODY chunk is smaller than expected uncompressed frame size.");
                }

                if (bodyData.Length == expectedSize)
                {
                    return bodyData;
                }

                byte[] trimmed = new byte[expectedSize];
                Buffer.BlockCopy(bodyData, 0, trimmed, 0, expectedSize);
                return trimmed;
            }

            if (compression != 1)
            {
                throw new InvalidDataException($"Unsupported PBM BODY compression: {compression}");
            }

            return DecodePbmByteRun(bodyData, expectedSize);
        }

        private static byte[] DecodePbmByteRun(byte[] compressed, int expectedSize)
        {
            byte[] output = new byte[expectedSize];
            int srcPos = 0;
            int dstPos = 0;

            while (dstPos < expectedSize)
            {
                if (srcPos >= compressed.Length)
                {
                    throw new InvalidDataException("Unexpected end of PBM BODY while decoding ByteRun1.");
                }

                sbyte code = unchecked((sbyte)compressed[srcPos++]);
                if (code >= 0)
                {
                    int count = code + 1;
                    if (srcPos + count > compressed.Length)
                    {
                        throw new InvalidDataException("Malformed PBM ByteRun1 literal run.");
                    }

                    int write = Math.Min(count, expectedSize - dstPos);
                    Buffer.BlockCopy(compressed, srcPos, output, dstPos, write);
                    srcPos += count;
                    dstPos += write;
                }
                else if (code != -128)
                {
                    int count = 1 - code;
                    if (srcPos >= compressed.Length)
                    {
                        throw new InvalidDataException("Malformed PBM ByteRun1 repeat run.");
                    }

                    byte value = compressed[srcPos++];
                    int write = Math.Min(count, expectedSize - dstPos);
                    for (int i = 0; i < write; i++)
                    {
                        output[dstPos++] = value;
                    }
                }
            }

            return output;
        }

        private static byte[] DecodeBodyToPlanar(BitmapHeader header, byte[] bodyData, byte targetNumPlanes)
        {
            int planePitch = GetPlanePitch(header.Width);
            int expectedSize = planePitch * header.Height * targetNumPlanes;

            if (header.Compression == 0)
            {
                if (bodyData.Length < expectedSize)
                {
                    throw new InvalidDataException("BODY chunk is smaller than expected uncompressed frame size.");
                }

                if (bodyData.Length == expectedSize)
                {
                    return bodyData;
                }

                byte[] trimmed = new byte[expectedSize];
                Buffer.BlockCopy(bodyData, 0, trimmed, 0, expectedSize);
                return trimmed;
            }

            if (header.Compression != 1)
            {
                throw new InvalidDataException($"Unsupported ILBM BODY compression: {header.Compression}");
            }

            return DecodeIlbmByteRun(bodyData, header.Width, header.Height, targetNumPlanes);
        }

        private static byte[] DecodeIlbmByteRun(byte[] compressed, int width, int height, int bitplanes)
        {
            int planePitch = GetPlanePitch((ushort)width);
            int lineBytes = planePitch * bitplanes;
            int expected = lineBytes * height;

            byte[] dst = new byte[expected];
            int srcPos = 0;
            int dstPos = 0;

            for (int y = 0; y < height; y++)
            {
                int lineRemaining = lineBytes;

                while (lineRemaining > 0)
                {
                    if (srcPos >= compressed.Length)
                    {
                        throw new InvalidDataException("Unexpected end of BODY while decoding ByteRun1.");
                    }

                    sbyte code = unchecked((sbyte)compressed[srcPos++]);

                    if (code >= 0)
                    {
                        int count = code + 1;
                        if (srcPos + count > compressed.Length)
                        {
                            throw new InvalidDataException("Malformed ByteRun1 literal run.");
                        }

                        int write = Math.Min(count, lineRemaining);
                        Buffer.BlockCopy(compressed, srcPos, dst, dstPos, write);
                        srcPos += count;
                        dstPos += write;
                        lineRemaining -= write;
                    }
                    else if (code != -128)
                    {
                        int count = 1 - code;
                        if (srcPos >= compressed.Length)
                        {
                            throw new InvalidDataException("Malformed ByteRun1 repeat run.");
                        }

                        byte value = compressed[srcPos++];
                        int write = Math.Min(count, lineRemaining);
                        for (int i = 0; i < write; i++)
                        {
                            dst[dstPos++] = value;
                        }

                        lineRemaining -= write;
                    }
                }
            }

            return dst;
        }

        private static void ApplyDelta(byte[] destination, byte[] deltaData, BitmapHeader header, byte operation, uint bits, bool xor, string frameFormat, bool isBrushAnim)
        {
            switch (operation)
            {
                case 5:
                case 6:
                    if (frameFormat == "PBM " && isBrushAnim && header.NumPlanes == 8)
                    {
                        DecodeByteVerticalDeltaPbmAsPlanar(destination, deltaData, header.Width, header.Height, xor);
                    }
                    else
                    {
                        DecodeByteVerticalDelta(destination, deltaData, header.Width, header.NumPlanes, xor);
                    }
                    break;

                case 7:
                    DecodeShortLongVerticalDelta7(destination, deltaData, header.Width, header.NumPlanes, (bits & 0x1) != 0);
                    break;

                case 8:
                    DecodeShortLongVerticalDelta8(destination, deltaData, header.Width, header.NumPlanes, (bits & 0x1) != 0);
                    break;

                case 74:
                    DecodeDeltaJ(destination, deltaData, header.Width, header.Height, header.NumPlanes);
                    break;

                case 75:
                    if (frameFormat == "PBM ")
                    {
                        DecodeDeltaKChunkyPbm(destination, deltaData, header.Width, header.Height);
                    }
                    else
                    {
                        DecodeDeltaK(destination, deltaData, header.Width, header.Height, header.NumPlanes);
                    }
                    break;

                default:
                    throw new InvalidDataException($"Unsupported ANIM delta operation: {operation}");
            }
        }

        private static void DecodeByteVerticalDeltaPbmAsPlanar(byte[] destination, byte[] data, int width, int height, bool xor)
        {
            byte[] planar = ConvertChunky8ToPlanar(destination, (ushort)width, (ushort)height);
            DecodeByteVerticalDelta(planar, data, width, 8, xor);
            ConvertPlanar8ToChunky(planar, destination, (ushort)width, (ushort)height);
        }

        private static byte[] ConvertChunky8ToPlanar(byte[] chunky, ushort width, ushort height)
        {
            int stride = GetPbmStride(width);
            int planePitch = GetPlanePitch(width);
            int planeSize = planePitch * height;
            byte[] planar = new byte[planeSize * 8];

            for (int y = 0; y < height; y++)
            {
                int srcRow = y * stride;
                int dstRowBase = y * planePitch;

                for (int x = 0; x < width; x++)
                {
                    int srcIndex = srcRow + x;
                    if ((uint)srcIndex >= (uint)chunky.Length)
                    {
                        continue;
                    }

                    byte value = chunky[srcIndex];
                    int byteInPlane = x >> 3;
                    int bitMask = 0x80 >> (x & 7);

                    for (int plane = 0; plane < 8; plane++)
                    {
                        if (((value >> plane) & 1) != 0)
                        {
                            int dstIndex = plane * planeSize + dstRowBase + byteInPlane;
                            planar[dstIndex] = (byte)(planar[dstIndex] | bitMask);
                        }
                    }
                }
            }

            return planar;
        }

        private static void ConvertPlanar8ToChunky(byte[] planar, byte[] chunky, ushort width, ushort height)
        {
            int stride = GetPbmStride(width);
            int planePitch = GetPlanePitch(width);
            int planeSize = planePitch * height;

            Array.Clear(chunky, 0, chunky.Length);

            for (int y = 0; y < height; y++)
            {
                int dstRow = y * stride;
                int srcRowBase = y * planePitch;

                for (int x = 0; x < width; x++)
                {
                    int byteInPlane = x >> 3;
                    int bitMask = 0x80 >> (x & 7);
                    byte value = 0;

                    for (int plane = 0; plane < 8; plane++)
                    {
                        int srcIndex = plane * planeSize + srcRowBase + byteInPlane;
                        if ((uint)srcIndex < (uint)planar.Length && (planar[srcIndex] & bitMask) != 0)
                        {
                            value = (byte)(value | (1 << plane));
                        }
                    }

                    int dstIndex = dstRow + x;
                    if ((uint)dstIndex < (uint)chunky.Length)
                    {
                        chunky[dstIndex] = value;
                    }
                }
            }
        }

        private static void DecodeDeltaKChunkyPbm(byte[] destination, byte[] deltaData, int width, int height)
        {
            if (deltaData.Length == 0 || width <= 0 || height <= 0)
            {
                return;
            }

            int stride = GetPbmStride((ushort)width);
            int columnCount = stride;

            int streamOffset = 0;
            if (deltaData.Length >= 4)
            {
                int candidate = ReadInt32BigEndianSafe(deltaData, 0);
                if (candidate > 0 && candidate < deltaData.Length)
                {
                    streamOffset = candidate;
                }
            }

            int pos = streamOffset;

            for (int column = 0; column < columnCount && pos < deltaData.Length; column++)
            {
                int opCount = deltaData[pos++];
                int row = 0;

                for (int opIndex = 0; opIndex < opCount && pos < deltaData.Length; opIndex++)
                {
                    int op = deltaData[pos++];

                    if (op == 0)
                    {
                        if (pos + 2 > deltaData.Length)
                        {
                            return;
                        }

                        int run = deltaData[pos++];
                        byte value = deltaData[pos++];

                        for (int i = 0; i < run; i++)
                        {
                            if ((uint)row < (uint)height)
                            {
                                int dstIndex = row * stride + column;
                                if ((uint)dstIndex < (uint)destination.Length)
                                {
                                    destination[dstIndex] = value;
                                }
                            }

                            row++;
                        }
                    }
                    else if (op < 0x80)
                    {
                        row += op;
                    }
                    else
                    {
                        int count = op & 0x7F;
                        for (int i = 0; i < count; i++)
                        {
                            if (pos >= deltaData.Length)
                            {
                                return;
                            }

                            byte value = deltaData[pos++];
                            if ((uint)row < (uint)height)
                            {
                                int dstIndex = row * stride + column;
                                if ((uint)dstIndex < (uint)destination.Length)
                                {
                                    destination[dstIndex] = value;
                                }
                            }

                            row++;
                        }
                    }
                }
            }
        }

        private static void DecodeByteVerticalDelta(byte[] destination, byte[] data, int width, int bitplanes, bool xor)
        {
            int ncolumns = ((width + 15) / 16) * 2;
            int dstPitch = ncolumns * bitplanes;

            for (int plane = 0; plane < bitplanes; plane++)
            {
                int srcOffset = ReadInt32BigEndianSafe(data, plane * 4);
                if (srcOffset <= 0 || srcOffset >= data.Length)
                {
                    continue;
                }

                for (int column = 0; column < ncolumns; column++)
                {
                    int dstOffset = column + plane * ncolumns;
                    if (srcOffset >= data.Length)
                    {
                        return;
                    }

                    int opCount = data[srcOffset++];
                    for (int opIndex = 0; opIndex < opCount; opIndex++)
                    {
                        if (srcOffset >= data.Length)
                        {
                            return;
                        }

                        int op = data[srcOffset++];

                        if (op == 0)
                        {
                            if (srcOffset + 1 >= data.Length)
                            {
                                return;
                            }

                            int run = data[srcOffset++];
                            byte value = data[srcOffset++];
                            for (int i = 0; i < run; i++)
                            {
                                if ((uint)dstOffset >= (uint)destination.Length)
                                {
                                    return;
                                }

                                destination[dstOffset] = xor ? (byte)(destination[dstOffset] ^ value) : value;
                                dstOffset += dstPitch;
                            }
                        }
                        else if (op < 0x80)
                        {
                            dstOffset += op * dstPitch;
                        }
                        else
                        {
                            int count = op & 0x7F;
                            for (int i = 0; i < count; i++)
                            {
                                if (srcOffset >= data.Length || (uint)dstOffset >= (uint)destination.Length)
                                {
                                    return;
                                }

                                byte value = data[srcOffset++];
                                destination[dstOffset] = xor ? (byte)(destination[dstOffset] ^ value) : value;
                                dstOffset += dstPitch;
                            }
                        }
                    }
                }
            }
        }

        private static void DecodeShortLongVerticalDelta7(byte[] destination, byte[] data, int width, int bitplanes, bool longData)
        {
            int planePitch = GetPlanePitch((ushort)width);
            int wordSize = longData ? 4 : 2;
            int ncolumns = longData ? ((width + 31) / 32) : ((width + 15) / 16);
            int dstPitch = planePitch * bitplanes;
            bool careBoundary = longData && (planePitch != ncolumns * 4);

            for (int plane = 0; plane < bitplanes; plane++)
            {
                int pOp = ReadInt32BigEndianSafe(data, plane * 4);
                int pDa = ReadInt32BigEndianSafe(data, 32 + plane * 4);

                if (pOp <= 0 || pOp >= data.Length)
                {
                    continue;
                }

                for (int column = 0; column < ncolumns; column++)
                {
                    int dstOffset = (column + plane * ncolumns) * wordSize;
                    if (careBoundary)
                    {
                        dstOffset -= 2 * plane;
                    }

                    if (pOp >= data.Length)
                    {
                        return;
                    }

                    int opCount = data[pOp++];
                    while (opCount-- > 0)
                    {
                        if (pOp >= data.Length)
                        {
                            return;
                        }

                        int op = data[pOp++];
                        bool write16 = wordSize == 2 || (careBoundary && (column + 1 == ncolumns));

                        if (op == 0)
                        {
                            if (pOp >= data.Length)
                            {
                                return;
                            }

                            int count = data[pOp++];

                            if (write16)
                            {
                                if (pDa + 2 > data.Length)
                                {
                                    return;
                                }

                                ushort raw = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(pDa, 2));
                                pDa += wordSize;

                                while (count-- > 0)
                                {
                                    if (dstOffset + 2 > destination.Length)
                                    {
                                        return;
                                    }

                                    BinaryPrimitives.WriteUInt16LittleEndian(destination.AsSpan(dstOffset, 2), raw);
                                    dstOffset += dstPitch;
                                }
                            }
                            else
                            {
                                if (pDa + 4 > data.Length)
                                {
                                    return;
                                }

                                uint raw = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pDa, 4));
                                pDa += 4;

                                while (count-- > 0)
                                {
                                    if (dstOffset + 4 > destination.Length)
                                    {
                                        return;
                                    }

                                    BinaryPrimitives.WriteUInt32LittleEndian(destination.AsSpan(dstOffset, 4), raw);
                                    dstOffset += dstPitch;
                                }
                            }
                        }
                        else if (op < 128)
                        {
                            dstOffset += dstPitch * op;
                        }
                        else
                        {
                            int count = op & 0x7F;
                            if (write16)
                            {
                                while (count-- > 0)
                                {
                                    if (pDa + 2 > data.Length || dstOffset + 2 > destination.Length)
                                    {
                                        return;
                                    }

                                    destination[dstOffset] = data[pDa];
                                    destination[dstOffset + 1] = data[pDa + 1];
                                    pDa += wordSize;
                                    dstOffset += dstPitch;
                                }
                            }
                            else
                            {
                                while (count-- > 0)
                                {
                                    if (pDa + 4 > data.Length || dstOffset + 4 > destination.Length)
                                    {
                                        return;
                                    }

                                    Buffer.BlockCopy(data, pDa, destination, dstOffset, 4);
                                    pDa += 4;
                                    dstOffset += dstPitch;
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void DecodeShortLongVerticalDelta8(byte[] destination, byte[] data, int width, int bitplanes, bool longData)
        {
            int planePitch = GetPlanePitch((ushort)width);
            int wordSize = longData ? 4 : 2;
            int ncolumns = longData ? ((width + 31) / 32) : ((width + 15) / 16);
            int dstPitch = planePitch * bitplanes;
            bool careBoundary = longData && (planePitch != ncolumns * 4);

            for (int plane = 0; plane < bitplanes; plane++)
            {
                int pOp = ReadInt32BigEndianSafe(data, plane * 4);
                if (pOp <= 0 || pOp >= data.Length)
                {
                    continue;
                }

                for (int column = 0; column < ncolumns; column++)
                {
                    bool write16 = wordSize == 2 || (careBoundary && (column + 1 == ncolumns));
                    int dstOffset = (column + plane * ncolumns) * wordSize;

                    if (wordSize == 4 && careBoundary)
                    {
                        dstOffset -= 2 * plane;
                    }

                    uint opCount;
                    if (write16)
                    {
                        if (pOp + 2 > data.Length)
                        {
                            return;
                        }

                        opCount = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(pOp, 2));
                        pOp += 2;
                    }
                    else
                    {
                        if (pOp + 4 > data.Length)
                        {
                            return;
                        }

                        opCount = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(pOp, 4));
                        pOp += 4;
                    }

                    while (opCount-- > 0)
                    {
                        uint op;
                        if (write16)
                        {
                            if (pOp + 2 > data.Length)
                            {
                                return;
                            }

                            op = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(pOp, 2));
                            pOp += 2;
                        }
                        else
                        {
                            if (pOp + 4 > data.Length)
                            {
                                return;
                            }

                            op = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(pOp, 4));
                            pOp += 4;
                        }

                        if (write16)
                        {
                            if (op == 0)
                            {
                                if (pOp + 4 > data.Length)
                                {
                                    return;
                                }

                                ushort count = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(pOp, 2));
                                pOp += 2;
                                ushort raw = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(pOp, 2));
                                pOp += 2;

                                while (count-- > 0)
                                {
                                    if (dstOffset + 2 > destination.Length)
                                    {
                                        return;
                                    }

                                    BinaryPrimitives.WriteUInt16LittleEndian(destination.AsSpan(dstOffset, 2), raw);
                                    dstOffset += dstPitch;
                                }
                            }
                            else if (op < 0x8000)
                            {
                                dstOffset += (int)op * dstPitch;
                            }
                            else
                            {
                                uint count = op & 0x7FFF;
                                while (count-- > 0)
                                {
                                    if (pOp + 2 > data.Length || dstOffset + 2 > destination.Length)
                                    {
                                        return;
                                    }

                                    destination[dstOffset] = data[pOp];
                                    destination[dstOffset + 1] = data[pOp + 1];
                                    pOp += 2;
                                    dstOffset += dstPitch;
                                }
                            }
                        }
                        else
                        {
                            if (op == 0)
                            {
                                if (pOp + 8 > data.Length)
                                {
                                    return;
                                }

                                uint count = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(pOp, 4));
                                pOp += 4;
                                uint raw = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pOp, 4));
                                pOp += 4;

                                while (count-- > 0)
                                {
                                    if (dstOffset + 4 > destination.Length)
                                    {
                                        return;
                                    }

                                    BinaryPrimitives.WriteUInt32LittleEndian(destination.AsSpan(dstOffset, 4), raw);
                                    dstOffset += dstPitch;
                                }
                            }
                            else if (op < 0x80000000)
                            {
                                dstOffset += (int)op * dstPitch;
                            }
                            else
                            {
                                uint count = op & 0x7FFFFFFF;
                                while (count-- > 0)
                                {
                                    if (pOp + 4 > data.Length || dstOffset + 4 > destination.Length)
                                    {
                                        return;
                                    }

                                    Buffer.BlockCopy(data, pOp, destination, dstOffset, 4);
                                    pOp += 4;
                                    dstOffset += dstPitch;
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void DecodeDeltaK(byte[] destination, byte[] deltaData, int width, int height, int bitplanes)
        {
            if (deltaData.Length >= 4)
            {
                int payloadOffset = ReadInt32BigEndianSafe(deltaData, 0);
                if (payloadOffset > 0 && payloadOffset < deltaData.Length - 2)
                {
                    byte[] payload = new byte[deltaData.Length - payloadOffset];
                    Buffer.BlockCopy(deltaData, payloadOffset, payload, 0, payload.Length);
                    DecodeDeltaJ(destination, payload, width, height, bitplanes);
                    return;
                }
            }

            DecodeDeltaJ(destination, deltaData, width, height, bitplanes);
        }

        private static void DecodeDeltaJ(byte[] destination, byte[] deltaData, int width, int height, int bitplanes)
        {
            int planepitchByte = (width + 7) / 8;
            int planePitch = GetPlanePitch((ushort)width);
            int pitch = planePitch * bitplanes;
            int kludgeJ = width < 320 ? (320 - width) / 8 / 2 : 0;

            int pos = 0;
            bool done = false;

            while (!done && pos + 2 <= deltaData.Length)
            {
                ushort type = BinaryPrimitives.ReadUInt16BigEndian(deltaData.AsSpan(pos, 2));
                pos += 2;

                switch (type)
                {
                    case 0:
                        done = true;
                        break;

                    case 1:
                    {
                        if (pos + 6 > deltaData.Length)
                        {
                            return;
                        }

                        ushort reversible = BinaryPrimitives.ReadUInt16BigEndian(deltaData.AsSpan(pos, 2));
                        pos += 2;
                        ushort bCount = BinaryPrimitives.ReadUInt16BigEndian(deltaData.AsSpan(pos, 2));
                        pos += 2;
                        ushort gCount = BinaryPrimitives.ReadUInt16BigEndian(deltaData.AsSpan(pos, 2));
                        pos += 2;

                        for (int g = 0; g < gCount; g++)
                        {
                            if (pos + 2 > deltaData.Length)
                            {
                                return;
                            }

                            ushort rawOffset = BinaryPrimitives.ReadUInt16BigEndian(deltaData.AsSpan(pos, 2));
                            pos += 2;
                            int offset = MapDeltaJOffset(rawOffset, planepitchByte, pitch, kludgeJ);

                            for (int b = 0; b < bCount; b++)
                            {
                                for (int d = 0; d < bitplanes; d++)
                                {
                                    if (pos >= deltaData.Length)
                                    {
                                        return;
                                    }

                                    int dstIndex = offset + (b * pitch) + (d * planePitch);
                                    byte v = deltaData[pos++];

                                    if ((uint)dstIndex >= (uint)destination.Length)
                                    {
                                        continue;
                                    }

                                    destination[dstIndex] = reversible != 0 ? (byte)(destination[dstIndex] ^ v) : v;
                                }
                            }

                            if (((bCount * bitplanes) & 1) != 0 && pos < deltaData.Length)
                            {
                                pos++;
                            }
                        }

                        break;
                    }

                    case 2:
                    {
                        if (pos + 8 > deltaData.Length)
                        {
                            return;
                        }

                        ushort reversible = BinaryPrimitives.ReadUInt16BigEndian(deltaData.AsSpan(pos, 2));
                        pos += 2;
                        ushort rowCount = BinaryPrimitives.ReadUInt16BigEndian(deltaData.AsSpan(pos, 2));
                        pos += 2;
                        ushort byteCount = BinaryPrimitives.ReadUInt16BigEndian(deltaData.AsSpan(pos, 2));
                        pos += 2;
                        ushort groupCount = BinaryPrimitives.ReadUInt16BigEndian(deltaData.AsSpan(pos, 2));
                        pos += 2;

                        for (int g = 0; g < groupCount; g++)
                        {
                            if (pos + 2 > deltaData.Length)
                            {
                                return;
                            }

                            ushort rawOffset = BinaryPrimitives.ReadUInt16BigEndian(deltaData.AsSpan(pos, 2));
                            pos += 2;
                            int offset = MapDeltaJOffset(rawOffset, planepitchByte, pitch, kludgeJ);

                            for (int r = 0; r < rowCount; r++)
                            {
                                for (int d = 0; d < bitplanes; d++)
                                {
                                    int dstBase = offset + (r * pitch) + (d * planePitch);

                                    for (int b = 0; b < byteCount; b++)
                                    {
                                        if (pos >= deltaData.Length)
                                        {
                                            return;
                                        }

                                        int dstIndex = dstBase + b;
                                        byte v = deltaData[pos++];

                                        if ((uint)dstIndex >= (uint)destination.Length)
                                        {
                                            continue;
                                        }

                                        destination[dstIndex] = reversible != 0 ? (byte)(destination[dstIndex] ^ v) : v;
                                    }
                                }
                            }

                            if (((rowCount * byteCount * bitplanes) & 1) != 0 && pos < deltaData.Length)
                            {
                                pos++;
                            }
                        }

                        break;
                    }

                    default:
                        return;
                }
            }
        }

        private static int MapDeltaJOffset(ushort rawOffset, int planepitchByte, int pitch, int kludgeJ)
        {
            if (kludgeJ != 0)
            {
                return ((rawOffset / (320 / 8)) * pitch) + (rawOffset % (320 / 8)) - kludgeJ;
            }

            return ((rawOffset / planepitchByte) * pitch) + (rawOffset % planepitchByte);
        }

        private static byte[] BuildLbmFromBuffer(BitmapHeader header, byte[] frameBody, byte[]? colorMap, uint? amigaMode, string frameFormat)
        {
            byte[] bmhd = new byte[20];
            BinaryPrimitives.WriteUInt16BigEndian(bmhd.AsSpan(0, 2), header.Width);
            BinaryPrimitives.WriteUInt16BigEndian(bmhd.AsSpan(2, 2), header.Height);
            BinaryPrimitives.WriteInt16BigEndian(bmhd.AsSpan(4, 2), header.XOrigin);
            BinaryPrimitives.WriteInt16BigEndian(bmhd.AsSpan(6, 2), header.YOrigin);
            bmhd[8] = header.NumPlanes;
            bmhd[9] = header.Masking;
            bmhd[10] = 0;
            bmhd[11] = 0;
            BinaryPrimitives.WriteUInt16BigEndian(bmhd.AsSpan(12, 2), header.TransparentColor);
            bmhd[14] = header.XAspect;
            bmhd[15] = header.YAspect;
            BinaryPrimitives.WriteInt16BigEndian(bmhd.AsSpan(16, 2), header.PageWidth);
            BinaryPrimitives.WriteInt16BigEndian(bmhd.AsSpan(18, 2), header.PageHeight);

            using var payload = new MemoryStream();
            WriteChunk(payload, "BMHD", bmhd);

            if (colorMap != null && colorMap.Length > 0)
            {
                WriteChunk(payload, "CMAP", colorMap);
            }

            if (amigaMode.HasValue)
            {
                byte[] camg = new byte[4];
                BinaryPrimitives.WriteUInt32BigEndian(camg, amigaMode.Value);
                WriteChunk(payload, "CAMG", camg);
            }

            WriteChunk(payload, "BODY", frameBody);

            byte[] payloadBytes = payload.ToArray();

            using var form = new MemoryStream();
            form.Write(Encoding.ASCII.GetBytes("FORM"));

            byte[] len = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(len, (uint)(4 + payloadBytes.Length));
            form.Write(len);

            form.Write(Encoding.ASCII.GetBytes(frameFormat));
            form.Write(payloadBytes);

            return form.ToArray();
        }

        private static void WriteChunk(Stream stream, string chunkId, byte[] data)
        {
            stream.Write(Encoding.ASCII.GetBytes(chunkId));

            byte[] len = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(len, (uint)data.Length);
            stream.Write(len);
            stream.Write(data, 0, data.Length);

            if ((data.Length & 1) != 0)
            {
                stream.WriteByte(0);
            }
        }

        private static int GetPlanePitch(ushort width)
        {
            return ((width + 15) / 16) * 2;
        }

        private static int GetPbmStride(ushort width)
        {
            return (width & 1) == 0 ? width : width + 1;
        }

        private static BitmapHeader ParseBitmapHeader(byte[] data, int offset)
        {
            return new BitmapHeader(
                Width: BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2)),
                Height: BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 2, 2)),
                XOrigin: BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(offset + 4, 2)),
                YOrigin: BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(offset + 6, 2)),
                NumPlanes: data[offset + 8],
                Masking: data[offset + 9],
                Compression: data[offset + 10],
                TransparentColor: BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 12, 2)),
                XAspect: data[offset + 14],
                YAspect: data[offset + 15],
                PageWidth: BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(offset + 16, 2)),
                PageHeight: BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(offset + 18, 2))
            );
        }

        private static AnimHeader ParseAnimHeader(byte[] data, int offset)
        {
            byte operation = data[offset];
            byte interleave = data[offset + 20];
            uint bits = ReadUInt32BigEndian(data, offset + 22);
            return new AnimHeader(operation, interleave, bits);
        }

        private static byte[] CopyBytes(byte[] source, int offset, int length)
        {
            byte[] result = new byte[length];
            Buffer.BlockCopy(source, offset, result, 0, length);
            return result;
        }

        private static int ReadInt32BigEndianSafe(byte[] data, int offset)
        {
            if (offset < 0 || offset + 4 > data.Length)
            {
                return 0;
            }

            return BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset, 4));
        }

        private static uint ReadUInt32BigEndian(byte[] data, int offset)
        {
            if (offset < 0 || offset + 4 > data.Length)
            {
                return 0;
            }

            return BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));
        }

        private static string ReadString(byte[] data, int offset, int length)
        {
            if (offset < 0 || offset + length > data.Length)
            {
                return string.Empty;
            }

            return Encoding.ASCII.GetString(data, offset, length);
        }
    }
}

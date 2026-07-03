using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.Amiga
{
    public static class SmsResourceDecoder
    {
        private const byte SmsRleMarker = 0xAE; // -0x52 in signed byte

        public static byte[] ConvertPlanarToLinear(SmsResource resource)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            if (resource.Width == 0 || resource.Height == 0) return Array.Empty<byte>();

            var width = resource.Width;
            var height = resource.Height;
            var bytesPerRow = resource.BytesPerRow;

            var output = new byte[width * height];

            for (var y = 0; y < height; y++)
            {
                var rowBase = y * bytesPerRow;
                var outRowBase = y * width;

                for (var x = 0; x < width; x++)
                {
                    var byteIndex = rowBase + (x >> 3);
                    var bitMask = (byte)(1 << (7 - (x & 7)));

                    byte pixel = 0;
                    for (var plane = 0; plane < resource.PlaneSlots.Length; plane++)
                    {
                        var slot = resource.PlaneSlots[plane];
                        var bit = slot.Kind switch
                        {
                            SmsPlaneSlotKind.ZeroFilled => 0,
                            SmsPlaneSlotKind.AllOnes => 1,
                            SmsPlaneSlotKind.BufferIndex => GetPlaneBit(resource, slot.BufferIndex, byteIndex, bitMask),
                            _ => 0
                        };

                        pixel |= (byte)(bit << plane);
                    }

                    output[outRowBase + x] = pixel;
                }
            }

            return output;
        }

        private static int GetPlaneBit(SmsResource resource, int bufferIndex, int byteIndex, byte bitMask)
        {
            if (bufferIndex < 0 || bufferIndex >= resource.PlaneBuffers.Length)
            {
                return 0;
            }

            var buffer = resource.PlaneBuffers[bufferIndex];
            if ((uint)byteIndex >= buffer.Length)
            {
                return 0;
            }

            return (buffer[byteIndex] & bitMask) != 0 ? 1 : 0;
        }

        public static int AmigaIntensity(int col)
        {
            // return (int)(pow((double)col / 15, 1.0 / 0.8) * 0xff);
            var normalized = (double)col / 15;
            var corrected = Math.Pow(normalized, 1.0 / 0.8);
            return (int)(corrected * 0xFF);
        }

        public static bool TryExtractPaletteRgb(SmsResource resource, out byte[]? rgb)
        {
            rgb = null;
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            if (resource.ExtraHeader == null || resource.ExtraHeader.Length < 2) return false;

            // SMS palette entries appear to be big-endian 12-bit Amiga colors (0x0RGB).
            // The extended header is 0x80 bytes -> 64 colors * 2 bytes each.
            var entryCount = Math.Min(64, resource.ExtraHeader.Length / 2);
            rgb = new byte[entryCount * 3];

            var offset = 0;
            for (var i = 0; i < entryCount; i++)
            {
                //var value = (ushort)((resource.ExtraHeader[offset] << 8) | resource.ExtraHeader[offset + 1]);
                int i2 = resource.ExtraHeader[offset];
                int i1 = resource.ExtraHeader[offset + 1];
                offset += 2;

                // var r4 = (value >> 8) & 0x0F;
                // var g4 = (value >> 4) & 0x0F;
                // var b4 = value & 0x0F;

                var r4 = i1 & 0x0F;
                var g4 = i2 >> 4;
                var b4 = i2 & 0x0F;

                rgb[i * 3] = (byte)AmigaIntensity(r4);
                rgb[i * 3 + 1] = (byte)AmigaIntensity(g4);
                rgb[i * 3 + 2] = (byte)AmigaIntensity(b4);
            }

            return true;
        }
        public static bool TryExtractPaletteRgbFromHighNibbleTriplets(SmsResource resource, out byte[] rgb)
        {
            rgb = null;
            if (resource?.ExtraHeader == null) return false;

            const int tripletBytes = 0x60;
            if (resource.ExtraHeader.Length < tripletBytes) return false;

            var zeroLowNibbleCount = 0;
            for (var i = 0; i < tripletBytes; i++)
            {
                if ((resource.ExtraHeader[i] & 0x0F) == 0)
                {
                    zeroLowNibbleCount++;
                }
            }

            if (zeroLowNibbleCount < tripletBytes * 9 / 10)
            {
                return false;
            }

            var colorCount = tripletBytes / 3;
            rgb = new byte[colorCount * 3];

            for (var i = 0; i < colorCount; i++)
            {
                var baseIndex = i * 3;
                var r4 = (resource.ExtraHeader[baseIndex] >> 4) & 0x0F;
                var g4 = (resource.ExtraHeader[baseIndex + 1] >> 4) & 0x0F;
                var b4 = (resource.ExtraHeader[baseIndex + 2] >> 4) & 0x0F;

                rgb[baseIndex] = (byte)(r4 * 0x11);
                rgb[baseIndex + 1] = (byte)(g4 * 0x11);
                rgb[baseIndex + 2] = (byte)(b4 * 0x11);
            }

            return true;
        }
        public static bool TryDecodeFromLzwOutput(byte[] lzwDecoded, int startOffset, out SmsResource? resource)
        {
            resource = null;
            if (lzwDecoded == null) throw new ArgumentNullException(nameof(lzwDecoded));
            if (startOffset < 0 || startOffset >= lzwDecoded.Length) return false;

            var reader = new ByteReader(lzwDecoded, startOffset);
            if (!reader.TryReadSmsMarker(out var variantByte))
            {
                return false;
            }

            if (!SmsHeader.TryRead(ref reader, out var header))
            {
                return false;
            }

            byte[] extraHeader = new byte[0x80];
            if (header.HasExtendedHeader)
            {
                if (!reader.TryReadBytes(0x80, out extraHeader))
                {
                    return false;
                }
            }

            var bytesPerRow = CalculateAlignedBytesPerRow(header.Width);
            var planeDataSize = bytesPerRow * header.Height;

            var realPlaneCount = CountRealPlaneBuffers(header.PlaneDescriptorNibbles, header.PlaneCount);
            var planeBuffers = new List<byte[]>(Math.Max(0, realPlaneCount));
            for (var i = 0; i < realPlaneCount; i++)
            {
                planeBuffers.Add(new byte[planeDataSize]);
            }

            var planeSlots = BuildPlaneSlotTable(header.PlaneDescriptorNibbles, planeBuffers);

            var payloadStart = reader.Offset;
            if (realPlaneCount > 0 && planeDataSize > 0 && header.Width > 1 && header.Height > 1 && header.PayloadSize > 1)
            {
                DecodePlanarRle(
                    lzwDecoded,
                    ref reader,
                    planeBuffers,
                    bytesPerRow,
                    header.Height,
                    (ushort)realPlaneCount,
                    header.LayoutMode
                );
            }

            if (header.PayloadSize > 0)
            {
                var expectedEnd = payloadStart + (int)header.PayloadSize;
                reader.Offset = Math.Min(expectedEnd, lzwDecoded.Length);
            }

            resource = new SmsResource(
                variantByte,
                header,
                extraHeader,
                bytesPerRow,
                planeDataSize,
                planeBuffers.ToArray(),
                planeSlots,
                reader.Offset
            );
            return true;
        }

        private static int CalculateAlignedBytesPerRow(ushort width)
        {
            var aligned = width;
            if ((aligned & 7) != 0) aligned += 8;
            if ((aligned & 8) != 0) aligned += 8;
            return aligned >> 3;
        }

        private static int CountRealPlaneBuffers(uint planeDescriptorNibbles, byte declaredPlaneCount)
        {
            int realCount = declaredPlaneCount;
            var nibbleStream = planeDescriptorNibbles;
            for (var i = 0; i < declaredPlaneCount; i++)
            {
                var nibble = (byte)(nibbleStream & 0xF);
                nibbleStream >>= 4;
                if (nibble == 0xC || nibble == 0xD || nibble == 0x8)
                {
                    realCount--;
                }
            }

            return Math.Max(0, realCount);
        }

        private static SmsPlaneSlot[] BuildPlaneSlotTable(uint planeDescriptorNibbles, List<byte[]> planeBuffers)
        {
            var slots = new SmsPlaneSlot[8];
            var nibbleStream = planeDescriptorNibbles;
            var bufferIndex = 0;

            for (var i = 0; i < 8; i++)
            {
                var nibble = (byte)(nibbleStream & 0xF);
                nibbleStream >>= 4;

                if (nibble == 0x8 || nibble == 0xC)
                {
                    slots[i] = SmsPlaneSlot.ZeroFilled;
                }
                else if (nibble == 0xD)
                {
                    slots[i] = SmsPlaneSlot.AllOnes;
                }
                else if (bufferIndex < planeBuffers.Count)
                {
                    slots[i] = SmsPlaneSlot.Buffer(bufferIndex);
                    bufferIndex++;
                }
                else
                {
                    slots[i] = SmsPlaneSlot.Unused;
                }
            }

            return slots;
        }

        // This mirrors FUN_0023f11a as closely as possible. It consumes RLE runs and writes into planar buffers.
        // The layout mode determines whether planes advance per column or per plane (observed via param_4.low).
        private static void DecodePlanarRle(
            byte[] source,
            ref ByteReader reader,
            List<byte[]> planeBuffers,
            int bytesPerRow,
            int height,
            ushort planeCount,
            byte layoutMode)
        {
            if (planeBuffers.Count == 0 || bytesPerRow <= 0 || height <= 0) return;

            var planes = planeBuffers;
            var planeIndex = 0;
            var currentPlane = planes[planeIndex];

            var columnOffset = 0;
            var rowCountdownStart = (short)(height - 1);
            var columnCountdownStart = (short)(bytesPerRow - 1);
            var planeCountdownStart = (short)(planeCount - 1);

            short rowCountdown;
            short columnCountdown;
            short planeCountdown;
            int writeIndex;

            bool AdvanceColumnMajor()
            {
                writeIndex += bytesPerRow;
                rowCountdown--;
                if (rowCountdown >= 0)
                {
                    return true;
                }

                planeCountdown--;
                if (planeCountdown >= 0)
                {
                    planeIndex++;
                    currentPlane = planes[planeIndex];
                    writeIndex = columnOffset;
                    rowCountdown = rowCountdownStart;
                    return true;
                }

                columnCountdown--;
                if (columnCountdown >= 0)
                {
                    columnOffset++;
                    planeIndex = 0;
                    currentPlane = planes[planeIndex];
                    writeIndex = columnOffset;
                    rowCountdown = rowCountdownStart;
                    planeCountdown = planeCountdownStart;
                    return true;
                }

                return false;
            }

            bool AdvancePlaneMajor()
            {
                writeIndex += bytesPerRow;
                rowCountdown--;
                if (rowCountdown >= 0)
                {
                    return true;
                }

                columnCountdown--;
                if (columnCountdown >= 0)
                {
                    columnOffset++;
                    writeIndex = columnOffset;
                    rowCountdown = rowCountdownStart;
                    return true;
                }

                planeCountdown--;
                if (planeCountdown >= 0)
                {
                    planeIndex++;
                    currentPlane = planes[planeIndex];
                    columnOffset = 0;
                    writeIndex = 0;
                    rowCountdown = rowCountdownStart;
                    columnCountdown = columnCountdownStart;
                    return true;
                }

                return false;
            }

            if (layoutMode != 1)
            {
                rowCountdown = rowCountdownStart;
                columnCountdown = columnCountdownStart;
                planeCountdown = planeCountdownStart;
                writeIndex = columnOffset;

                while (reader.Offset < source.Length)
                {
                    if (!reader.TryReadByte(out var value)) break;

                    var repeatCount = 1;
                    if (value == SmsRleMarker)
                    {
                        if (!reader.TryReadByte(out value)) break;
                        if (!reader.TryReadByte(out var countByte)) break;
                        repeatCount = countByte + 1;
                    }

                    for (var i = 0; i < repeatCount; i++)
                    {
                        if ((uint)writeIndex < currentPlane.Length)
                        {
                            currentPlane[writeIndex] = value;
                        }

                        if (!AdvanceColumnMajor()) return;
                    }
                }

                return;
            }

            rowCountdown = rowCountdownStart;
            columnCountdown = columnCountdownStart;
            planeCountdown = planeCountdownStart;
            writeIndex = columnOffset;

            while (reader.Offset < source.Length)
            {
                if (!reader.TryReadByte(out var value)) break;

                var repeatCount = 1;
                if (value == SmsRleMarker)
                {
                    if (!reader.TryReadByte(out value)) break;
                    if (!reader.TryReadByte(out var countByte)) break;
                    repeatCount = countByte + 1;
                }

                for (var i = 0; i < repeatCount; i++)
                {
                    if ((uint)writeIndex < currentPlane.Length)
                    {
                        currentPlane[writeIndex] = value;
                    }

                    if (!AdvancePlaneMajor()) return;
                }
            }
        }

        public sealed class ByteReader
        {
            private readonly byte[] _data;
            public int Offset { get; set; }

            public ByteReader(byte[] data, int offset)
            {
                _data = data;
                Offset = offset;
            }

            public bool TryReadByte(out byte value)
            {
                if (Offset >= _data.Length)
                {
                    value = 0;
                    return false;
                }

                value = _data[Offset++];
                return true;
            }

            public bool TryReadBytes(int count, out byte[] value)
            {
                if (count < 0 || Offset + count > _data.Length)
                {
                    value = new byte[0];
                    return false;
                }

                value = new byte[count];
                Buffer.BlockCopy(_data, Offset, value, 0, count);
                Offset += count;
                return true;
            }

            public bool TryReadSmsMarker(out byte variantByte)
            {
                variantByte = 0;
                if (Offset + 4 > _data.Length) return false;

                if (_data[Offset] != (byte)'S' || _data[Offset + 1] != (byte)'M' || _data[Offset + 2] != (byte)'S')
                {
                    return false;
                }

                variantByte = _data[Offset + 3];
                Offset += 4;
                return true;
            }

            public bool TryReadUInt32(out uint value)
            {
                value = 0;
                if (Offset + 4 > _data.Length) return false;

                value = (uint)(_data[Offset] << 24 | _data[Offset + 1] << 16 | _data[Offset + 2] << 8 | _data[Offset + 3]);
                Offset += 4;
                return true;
            }

            public bool TryReadUInt16(out ushort value)
            {
                value = 0;
                if (Offset + 2 > _data.Length) return false;

                value = (ushort)(_data[Offset] << 8 | _data[Offset + 1]);
                Offset += 2;
                return true;
            }
        }

        public readonly struct SmsHeader
        {
            public readonly uint PayloadSize;
            public readonly ushort Width;
            public readonly ushort Height;
            public readonly ushort UnknownA;
            public readonly ushort UnknownB;
            public readonly byte PlaneCount;
            public readonly byte LayoutMode;
            public readonly ushort UnknownC;
            public readonly uint PlaneDescriptorNibbles;
            public readonly ushort ExtendedHeaderFlags;

            public bool HasExtendedHeader => (ExtendedHeaderFlags & 0xFF00) != 0;

            public SmsHeader(
                uint payloadSize,
                ushort width,
                ushort height,
                ushort unknownA,
                ushort unknownB,
                byte planeCount,
                byte layoutMode,
                ushort unknownC,
                uint planeDescriptorNibbles,
                ushort extendedHeaderFlags)
            {
                PayloadSize = payloadSize;
                Width = width;
                Height = height;
                UnknownA = unknownA;
                UnknownB = unknownB;
                PlaneCount = planeCount;
                LayoutMode = layoutMode;
                UnknownC = unknownC;
                PlaneDescriptorNibbles = planeDescriptorNibbles;
                ExtendedHeaderFlags = extendedHeaderFlags;
            }

            public static bool TryRead(ref ByteReader reader, out SmsHeader header)
            {
                header = default;

                if (!reader.TryReadUInt32(out var payloadSize)) return false;
                if (!reader.TryReadUInt16(out var width)) return false;
                if (!reader.TryReadUInt16(out var height)) return false;
                if (!reader.TryReadUInt16(out var unknownA)) return false;
                if (!reader.TryReadUInt16(out var unknownB)) return false;
                if (!reader.TryReadByte(out var planeCount)) return false;
                if (!reader.TryReadByte(out var layoutMode)) return false;
                if (!reader.TryReadUInt16(out var unknownC)) return false;
                if (!reader.TryReadUInt32(out var planeDescriptorNibbles)) return false;
                if (!reader.TryReadUInt16(out var extendedHeaderFlags)) return false;

                header = new SmsHeader(
                    payloadSize,
                    width,
                    height,
                    unknownA,
                    unknownB,
                    planeCount,
                    layoutMode,
                    unknownC,
                    planeDescriptorNibbles,
                    extendedHeaderFlags);
                return true;
            }
        }
    }

    public sealed class SmsResource
    {
        public SmsResource(
            byte variantByte,
            SmsResourceDecoder.SmsHeader header,
            byte[] extraHeader,
            int bytesPerRow,
            int planeDataSize,
            byte[][] planeBuffers,
            SmsPlaneSlot[] planeSlots,
            int endOffset)
        {
            VariantByte = variantByte;
            PayloadSize = header.PayloadSize;
            Width = header.Width;
            Height = header.Height;
            UnknownA = header.UnknownA;
            UnknownB = header.UnknownB;
            PlaneCount = header.PlaneCount;
            LayoutMode = header.LayoutMode;
            UnknownC = header.UnknownC;
            PlaneDescriptorNibbles = header.PlaneDescriptorNibbles;
            ExtendedHeaderFlags = header.ExtendedHeaderFlags;
            ExtraHeader = extraHeader;
            BytesPerRow = bytesPerRow;
            PlaneDataSize = planeDataSize;
            PlaneBuffers = planeBuffers;
            PlaneSlots = planeSlots;
            EndOffset = endOffset;
        }

        public byte VariantByte { get; }
        public uint PayloadSize { get; }
        public ushort Width { get; }
        public ushort Height { get; }
        public ushort UnknownA { get; }
        public ushort UnknownB { get; }
        public byte PlaneCount { get; }
        public byte LayoutMode { get; }
        public ushort UnknownC { get; }
        public uint PlaneDescriptorNibbles { get; }
        public ushort ExtendedHeaderFlags { get; }
        public byte[] ExtraHeader { get; }
        public int BytesPerRow { get; }
        public int PlaneDataSize { get; }
        public byte[][] PlaneBuffers { get; }
        public SmsPlaneSlot[] PlaneSlots { get; }
        public int EndOffset { get; }
    }

    public readonly struct SmsPlaneSlot
    {
        public static readonly SmsPlaneSlot ZeroFilled = new SmsPlaneSlot(SmsPlaneSlotKind.ZeroFilled, -1);
        public static readonly SmsPlaneSlot AllOnes = new SmsPlaneSlot(SmsPlaneSlotKind.AllOnes, -1);
        public static readonly SmsPlaneSlot Unused = new SmsPlaneSlot(SmsPlaneSlotKind.Unused, -1);

        public static SmsPlaneSlot Buffer(int index) => new SmsPlaneSlot(SmsPlaneSlotKind.BufferIndex, index);

        private SmsPlaneSlot(SmsPlaneSlotKind kind, int index)
        {
            Kind = kind;
            BufferIndex = index;
        }

        public SmsPlaneSlotKind Kind { get; }
        public int BufferIndex { get; }
    }

    public enum SmsPlaneSlotKind
    {
        Unused,
        BufferIndex,
        ZeroFilled,
        AllOnes
    }


    public static class ICFTDLzwDecompressor
    {
        // Decompresses a single resource payload that begins with optional marker ("LZ" or "SMS")
        // followed by: 1 byte (headerByte), 4 bytes big-endian uncompressed size, then bitstream.
        public static byte[] Decompress(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length < 6) throw new ArgumentException("Data too small.", nameof(data));

            var offset = 0;
            if (data.Length >= 2 && data[0] == (byte)'L' && data[1] == (byte)'Z')
            {
                offset = 2;
            }
            else if (data.Length >= 3 && data[0] == (byte)'S' && data[1] == (byte)'M' && data[2] == (byte)'S')
            {
                offset = (data.Length >= 4 && data[3] == 0x00) ? 4 : 3;
            }

            if (data.Length < offset + 5)
            {
                throw new ArgumentException("Data too small for header.", nameof(data));
            }

            var headerByte = data[offset];
            var uncompressedSize = ReadUInt32BigEndian(data, offset + 1);
            if (uncompressedSize == 0)
            {
                return Array.Empty<byte>();
            }

            var bitstreamOffset = offset + 5;
            if (bitstreamOffset > data.Length)
            {
                throw new ArgumentException("Invalid header offsets.", nameof(data));
            }

            var output = new byte[uncompressedSize];

            var prefix = new ushort[0x1000];
            var suffix = new byte[0x1000];

            var codeWidth = 9;
            var maxCode = (1 << codeWidth) - 1;       // corresponds to A4+0x2b1e
            var maxCodeMinus1 = maxCode - 1;          // corresponds to A4+0x2b22
            var nextCode = 0x100;                     // corresponds to A4+0x2b24

            var reader = new BitReader(data, bitstreamOffset);

            int outPos = 0;
            int prevCode = reader.ReadBits(codeWidth, ref maxCode, ref maxCodeMinus1, ref codeWidth);
            if (prevCode < 0)
            {
                return output;
            }

            if (prevCode == maxCode)
            {
                WriteLastByte(output, headerByte);
                return output;
            }

            byte prevChar = (byte)prevCode;
            output[outPos++] = prevChar;

            while (outPos < output.Length)
            {
                int code = reader.ReadBits(codeWidth, ref maxCode, ref maxCodeMinus1, ref codeWidth);
                if (code < 0)
                {
                    break;
                }

                if (code == maxCodeMinus1)
                {
                    IncreaseCodeWidth(ref codeWidth, ref maxCode, ref maxCodeMinus1);
                    continue;
                }

                if (code == maxCode)
                {
                    break;
                }

                var stack = new byte[0x1000];
                int stackLen = 0;

                int cur = code;
                if (cur >= nextCode)
                {
                    stack[stackLen++] = prevChar;
                    cur = prevCode;
                }

                while (cur > 0xFF)
                {
                    stack[stackLen++] = suffix[cur];
                    cur = prefix[cur];
                }

                byte firstChar = (byte)cur;
                stack[stackLen++] = firstChar;

                for (int i = stackLen - 1; i >= 0 && outPos < output.Length; i--)
                {
                    output[outPos++] = stack[i];
                }

                if (nextCode < 0x1000)
                {
                    prefix[nextCode] = (ushort)prevCode;
                    suffix[nextCode] = firstChar;
                    nextCode++;
                }

                prevCode = code;
                prevChar = firstChar;
            }

            WriteLastByte(output, headerByte);
            return output;
        }

        private static void WriteLastByte(byte[] output, byte headerByte)
        {
            if (output.Length == 0) return;
            output[^1] = headerByte;
        }

        private static void IncreaseCodeWidth(ref int codeWidth, ref int maxCode, ref int maxCodeMinus1)
        {
            if (codeWidth < 12)
            {
                codeWidth++;
                maxCode = (1 << codeWidth) - 1;
                maxCodeMinus1 = maxCode - 1;
            }
        }

        private static uint ReadUInt32BigEndian(byte[] data, int offset)
        {
            if (offset + 4 > data.Length) return 0;
            return (uint)(data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3]);
        }

        private sealed class BitReader
        {
            private readonly byte[] _data;
            private int _pos;
            private uint _bitBuffer;
            private int _bitsInBuffer;

            public BitReader(byte[] data, int startOffset)
            {
                _data = data;
                _pos = startOffset;
                _bitBuffer = 0;
                _bitsInBuffer = 0;
            }

            public int ReadBits(int codeWidth, ref int maxCode, ref int maxCodeMinus1, ref int currentWidth)
            {
                // Mimic the 68k reader: ensure we have at least 25 bits cached.
                for (int s = _bitsInBuffer; s < 0x19; s += 8)
                {
                    if (_pos >= _data.Length)
                    {
                        return -1;
                    }

                    _bitBuffer |= (uint)_data[_pos++] << (0x18 - s);
                }

                _bitsInBuffer = _bitsInBuffer + 8 * ((25 - _bitsInBuffer + 7) / 8);
                _bitsInBuffer -= codeWidth;

                var value = (int)(_bitBuffer >> (0x20 - codeWidth));
                _bitBuffer <<= codeWidth;
                return value;
            }
        }
    }
}

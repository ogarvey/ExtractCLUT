using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ExtractCLUT.Games.PC.FamilyProductions
{
    public class DacSubrecord
    {
        public uint Size { get; set; }
        public ushort Type { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    public class DacFrame
    {
        public uint Size { get; set; }
        public ushort Marker { get; set; }
        public ushort Sequence { get; set; }
        public List<DacSubrecord> Subrecords { get; } = new List<DacSubrecord>();
        public byte[] Pixels { get; set; } = Array.Empty<byte>();
        public List<SixLabors.ImageSharp.Color> Palette { get; set; } = new List<SixLabors.ImageSharp.Color>();
    }

    public class DacFile
    {
        private const int MemberHeaderSize = 0x97;
        private const int FrameHeaderSize = 0x10;
        private const int Width = 320;
        private const int Height = 200;
        private const int PixelCount = Width * Height;

        public List<DacFrame> Frames { get; } = new List<DacFrame>();
        public List<SixLabors.ImageSharp.Color> Palette { get; private set; } = CreatePalette(Array.Empty<byte>());

        public static DacFile Load(string path)
        {
            var data = File.ReadAllBytes(path);
            if (data.Length < MemberHeaderSize + FrameHeaderSize)
                throw new InvalidDataException($"DAC file too small: {path}");

            var file = new DacFile();
            var paletteBytes = new byte[256 * 3];
            byte[] canvas = Array.Empty<byte>();
            int offset = MemberHeaderSize;

            while (offset < data.Length)
            {
                EnsureRemaining(data, offset, FrameHeaderSize, path, "frame header");
                uint frameSize = ReadUInt32(data, offset);
                if (frameSize < FrameHeaderSize)
                    throw new InvalidDataException($"DAC frame at 0x{offset:X} is smaller than its header: {path}");
                if (frameSize > data.Length - offset)
                    throw new InvalidDataException($"DAC frame at 0x{offset:X} exceeds the file: {path}");

                int frameEnd = checked(offset + (int)frameSize);
                var frame = new DacFrame
                {
                    Size = frameSize,
                    Marker = ReadUInt16(data, offset + 4),
                    Sequence = ReadUInt16(data, offset + 6)
                };

                int subrecordOffset = offset + FrameHeaderSize;
                while (subrecordOffset < frameEnd)
                {
                    EnsureRemaining(data, subrecordOffset, 6, path, "subrecord header");
                    uint subrecordSize = ReadUInt32(data, subrecordOffset);
                    if (subrecordSize < 6)
                        throw new InvalidDataException($"DAC subrecord at 0x{subrecordOffset:X} is too small: {path}");
                    if (subrecordSize > frameEnd - subrecordOffset)
                        throw new InvalidDataException($"DAC subrecord at 0x{subrecordOffset:X} exceeds its frame: {path}");

                    int payloadSize = checked((int)subrecordSize - 6);
                    var subrecord = new DacSubrecord
                    {
                        Size = subrecordSize,
                        Type = ReadUInt16(data, subrecordOffset + 4),
                        Data = new byte[payloadSize]
                    };
                    Buffer.BlockCopy(data, subrecordOffset + 6, subrecord.Data, 0, payloadSize);
                    frame.Subrecords.Add(subrecord);
                    ApplySubrecord(subrecord, ref canvas, paletteBytes, path, subrecordOffset);
                    subrecordOffset += checked((int)subrecordSize);
                }

                if (subrecordOffset != frameEnd)
                    throw new InvalidDataException($"DAC subrecords do not fill frame at 0x{offset:X}: {path}");

                file.Palette = CreatePalette(paletteBytes);
                frame.Palette = new List<SixLabors.ImageSharp.Color>(file.Palette);
                frame.Pixels = canvas.Length == 0 ? Array.Empty<byte>() : (byte[])canvas.Clone();
                file.Frames.Add(frame);
                offset = frameEnd;
            }

            if (offset != data.Length)
                throw new InvalidDataException($"DAC parser stopped at 0x{offset:X}: {path}");

            return file;
        }

        public void SaveImages(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            for (int i = 0; i < Frames.Count; i++)
            {
                var frame = Frames[i];
                if (frame.Pixels.Length != PixelCount)
                    continue;

                using var image = new Image<Rgba32>(Width, Height);
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        int pixelIndex = y * Width + x;
                        int paletteIndex = frame.Pixels[pixelIndex];
                        image[x, y] = (Rgba32)frame.Palette[paletteIndex];
                    }
                }

                var outputPath = Path.Combine(outputDirectory, $"frame_{i:D4}.png");
                image.Save(outputPath);
            }
        }

        private static void ApplySubrecord(
            DacSubrecord subrecord,
            ref byte[] canvas,
            byte[] palette,
            string path,
            int offset)
        {
            switch (subrecord.Type)
            {
                case 0x0B:
                    DecodePalette(subrecord.Data, palette, path, offset);
                    break;
                case 0x0C:
                    if (canvas.Length != PixelCount)
                        throw new InvalidDataException($"DAC delta appears before a full frame at 0x{offset:X}: {path}");
                    ApplyDelta(subrecord.Data, canvas, path, offset);
                    break;
                case 0x0F:
                    canvas = DecodeFullFrame(subrecord.Data, path, offset);
                    break;
            }
        }

        private static void DecodePalette(byte[] data, byte[] palette, string path, int offset)
        {
            if (data.Length < 2)
                throw new InvalidDataException($"DAC palette subrecord at 0x{offset:X} has no command count: {path}");

            int sourceOffset = 2;
            int paletteIndex = 0;
            int commandCount = ReadUInt16(data, 0);
            for (int command = 0; command < commandCount; command++)
            {
                EnsureRemaining(data, sourceOffset, 2, path, "palette command");
                paletteIndex += data[sourceOffset++];
                int count = data[sourceOffset++];
                int paletteCount = count == 0 ? 256 - paletteIndex : count;
                if (paletteIndex < 0 || paletteIndex + paletteCount > 256)
                    throw new InvalidDataException($"DAC palette command exceeds 256 colours at 0x{offset:X}: {path}");

                int byteCount = checked(paletteCount * 3);
                EnsureRemaining(data, sourceOffset, byteCount, path, "palette data");
                Buffer.BlockCopy(data, sourceOffset, palette, paletteIndex * 3, byteCount);
                sourceOffset += byteCount;
                paletteIndex += paletteCount;
            }

        }

        private static byte[] DecodeFullFrame(byte[] data, string path, int offset)
        {
            var pixels = new byte[PixelCount];
            int sourceOffset = 0;

            for (int y = 0; y < Height; y++)
            {
                EnsureRemaining(data, sourceOffset, 1, path, "full-frame row command count");
                int commandCount = data[sourceOffset++];
                int rowPixels = 0;

                for (int command = 0; command < commandCount; command++)
                {
                    EnsureRemaining(data, sourceOffset, 1, path, "full-frame command");
                    byte lengthByte = data[sourceOffset++];
                    if ((lengthByte & 0x80) != 0)
                    {
                        int count = 256 - lengthByte;
                        EnsureRemaining(data, sourceOffset, count, path, "full-frame literal run");
                        if (rowPixels + count > Width)
                            throw new InvalidDataException($"DAC full-frame row {y} exceeds 320 pixels at 0x{offset:X}: {path}");

                        Buffer.BlockCopy(data, sourceOffset, pixels, y * Width + rowPixels, count);
                        sourceOffset += count;
                        rowPixels += count;
                    }
                    else
                    {
                        EnsureRemaining(data, sourceOffset, 1, path, "full-frame repeated value");
                        if (rowPixels + lengthByte > Width)
                            throw new InvalidDataException($"DAC full-frame row {y} exceeds 320 pixels at 0x{offset:X}: {path}");

                        byte value = data[sourceOffset++];
                        for (int i = 0; i < lengthByte; i++)
                            pixels[y * Width + rowPixels++] = value;
                    }
                }

                if (rowPixels != Width)
                    throw new InvalidDataException($"DAC full-frame row {y} contains {rowPixels} pixels at 0x{offset:X}: {path}");
            }

            return pixels;
        }

        private static void ApplyDelta(byte[] data, byte[] canvas, string path, int offset)
        {
            if (data.Length < 4)
                throw new InvalidDataException($"DAC delta subrecord at 0x{offset:X} has no row header: {path}");

            int sourceOffset = 4;
            int startRow = ReadUInt16(data, 0);
            int rowCount = ReadUInt16(data, 2);
            if (startRow + rowCount > Height)
                throw new InvalidDataException($"DAC delta rows exceed 200 at 0x{offset:X}: {path}");

            for (int row = 0; row < rowCount; row++)
            {
                EnsureRemaining(data, sourceOffset, 1, path, "delta row command count");
                int commandCount = data[sourceOffset++];
                int x = 0;

                for (int command = 0; command < commandCount; command++)
                {
                    EnsureRemaining(data, sourceOffset, 2, path, "delta command");
                    x += data[sourceOffset++];
                    byte lengthByte = data[sourceOffset++];

                    if ((lengthByte & 0x80) != 0)
                    {
                        int count = 256 - lengthByte;
                        EnsureRemaining(data, sourceOffset, 1, path, "delta repeated value");
                        if (x + count > Width)
                            throw new InvalidDataException($"DAC delta row {startRow + row} exceeds 320 pixels at 0x{offset:X}: {path}");

                        byte value = data[sourceOffset++];
                        for (int i = 0; i < count; i++)
                            canvas[(startRow + row) * Width + x++] = value;
                    }
                    else
                    {
                        int count = lengthByte;
                        EnsureRemaining(data, sourceOffset, count, path, "delta literal run");
                        if (x + count > Width)
                            throw new InvalidDataException($"DAC delta row {startRow + row} exceeds 320 pixels at 0x{offset:X}: {path}");

                        Buffer.BlockCopy(data, sourceOffset, canvas, (startRow + row) * Width + x, count);
                        sourceOffset += count;
                        x += count;
                    }
                }
            }

        }

        private static List<SixLabors.ImageSharp.Color> CreatePalette(byte[] palette)
        {
            return ColorHelper.ConvertBytesToRgbIS(palette, true);
        }

        private static ushort ReadUInt16(byte[] data, int offset)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            return BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
        }

        private static void EnsureRemaining(byte[] data, int offset, int count, string path, string field)
        {
            if (offset < 0 || count < 0 || count > data.Length - offset)
                throw new InvalidDataException($"DAC {field} exceeds the data at 0x{offset:X}: {path}");
        }
    }
}

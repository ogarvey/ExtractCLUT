using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ExtractCLUT.Games.PC.BlackMagic
{
    public class ScmParser
    {
        public sealed class BlockRegion
        {
            public int Index { get; init; }
            public uint Offset { get; init; }
            public uint Size { get; init; }
            public bool IsEmpty => Size == 0;

            public override string ToString()
            {
                return $"Block{Index}: off=0x{Offset:X8} size=0x{Size:X8} ({Size})";
            }
        }

        public sealed class ScmHeader
        {
            public IReadOnlyList<BlockRegion> Blocks => _blocks;
            private readonly List<BlockRegion> _blocks;

            public ScmHeader(List<BlockRegion> blocks)
            {
                _blocks = blocks;
            }
        }

        public sealed class SpriteDescriptor
        {
            public int Index { get; init; }
            public ushort Width { get; init; }
            public ushort Height { get; init; }
            public uint PixelLength { get; init; }
            public uint PixelDataOffset { get; init; }

            public bool SizeMatchesLength => (uint)Width * Height == PixelLength;
        }

        public sealed class FramePartRef
        {
            public ushort SpriteDescriptorIndex { get; init; }
            public short OffsetX { get; init; }
            public short OffsetY { get; init; }
        }

        public sealed class CompositeFrameRecord
        {
            public int SourceOffset { get; init; }
            public ushort PartCount { get; init; }
            public short AnchorX { get; init; }
            public short AnchorY { get; init; }
            public ushort FrameWidth { get; init; }
            public ushort FrameHeight { get; init; }
            public IReadOnlyList<FramePartRef> Parts => _parts;

            private readonly List<FramePartRef> _parts;

            public CompositeFrameRecord(int sourceOffset, ushort partCount, short anchorX, short anchorY, ushort frameWidth, ushort frameHeight, List<FramePartRef> parts)
            {
                SourceOffset = sourceOffset;
                PartCount = partCount;
                AnchorX = anchorX;
                AnchorY = anchorY;
                FrameWidth = frameWidth;
                FrameHeight = frameHeight;
                _parts = parts;
            }

            public int EncodedSize => 10 + (PartCount * 6);
        }

        public sealed class ParsedScmFile
        {
            public string FilePath { get; }
            public long FileLength { get; }
            public ScmHeader Header { get; }
            public byte[][] RawBlocks { get; }
            public IReadOnlyList<uint> FrameOffsets => _frameOffsets;
            public IReadOnlyList<CompositeFrameRecord> CompositeRecords => _compositeRecords;
            public IReadOnlyList<SpriteDescriptor> SpriteDescriptors => _spriteDescriptors;
            public byte[] PaletteRgbx { get; }
            public byte[] PixelDataBlock { get; }

            private readonly List<uint> _frameOffsets;
            private readonly List<CompositeFrameRecord> _compositeRecords;
            private readonly List<SpriteDescriptor> _spriteDescriptors;

            public ParsedScmFile(
              string filePath,
              long fileLength,
              ScmHeader header,
              byte[][] rawBlocks,
              List<uint> frameOffsets,
              List<CompositeFrameRecord> compositeRecords,
              List<SpriteDescriptor> spriteDescriptors,
              byte[] paletteRgbx,
              byte[] pixelDataBlock)
            {
                FilePath = filePath;
                FileLength = fileLength;
                Header = header;
                RawBlocks = rawBlocks;
                _frameOffsets = frameOffsets;
                _compositeRecords = compositeRecords;
                _spriteDescriptors = spriteDescriptors;
                PaletteRgbx = paletteRgbx;
                PixelDataBlock = pixelDataBlock;
            }
        }

        public ParsedScmFile Parse(string scmPath)
        {
            if (!File.Exists(scmPath))
                throw new FileNotFoundException("SCM file not found", scmPath);

            using var fs = new FileStream(scmPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);

            if (fs.Length < 0x28)
                throw new InvalidDataException("SCM file too small for 0x28-byte header");

            var blocks = ReadHeaderBlocks(br, fs.Length);
            var header = new ScmHeader(blocks);
            var rawBlocks = ReadRawBlocks(br, blocks, fs.Length);

            var frameOffsets = ParseFrameOffsets(rawBlocks[0]);
            var compositeRecords = ParseCompositeRecords(rawBlocks[1], frameOffsets);
            var spriteDescriptors = ParseSpriteDescriptors(rawBlocks[2]);
            ResolvePaletteAndPixelData(rawBlocks[3], rawBlocks[4], out var palette, out var pixelData);

            ValidateDescriptorReferences(compositeRecords, spriteDescriptors.Count);

            return new ParsedScmFile(
              scmPath,
              fs.Length,
              header,
              rawBlocks,
              frameOffsets,
              compositeRecords,
              spriteDescriptors,
              palette,
              pixelData);
        }

        public void ExportAlignedCompositeFrames(string scmPath, string outputDirectory, byte transparentIndex = 0, bool saveRawFrames = false)
        {
            var parsed = Parse(scmPath);
            ExportAlignedCompositeFrames(parsed, outputDirectory, transparentIndex, saveRawFrames);
        }

        public void ExportAlignedCompositeFrames(ParsedScmFile parsed, string outputDirectory, byte transparentIndex = 0, bool saveRawFrames = false)
        {
            if (parsed.CompositeRecords.Count == 0)
                throw new InvalidOperationException("No composite records found in SCM file.");

            if (parsed.SpriteDescriptors.Count == 0)
                throw new InvalidOperationException("No sprite descriptors found in SCM file.");

            if (parsed.PixelDataBlock.Length == 0)
                throw new InvalidOperationException("No pixel-data block found in SCM file.");

            if (parsed.PaletteRgbx.Length < 1024)
                throw new InvalidOperationException("Palette block missing or too small (expected 1024-byte RGBX).");

            Directory.CreateDirectory(outputDirectory);
            string rawDirectory = Path.Combine(outputDirectory, "raw_frames");
            if (saveRawFrames)
                Directory.CreateDirectory(rawDirectory);

            Rgba32[] palette = ConvertRgbxPalette(parsed.PaletteRgbx, transparentIndex);

            int minLeft = int.MaxValue;
            int minTop = int.MaxValue;
            int maxRight = int.MinValue;
            int maxBottom = int.MinValue;

            foreach (var record in parsed.CompositeRecords)
            {
                minLeft = Math.Min(minLeft, record.AnchorX);
                minTop = Math.Min(minTop, record.AnchorY);
                maxRight = Math.Max(maxRight, record.AnchorX + record.FrameWidth);
                maxBottom = Math.Max(maxBottom, record.AnchorY + record.FrameHeight);
            }

            int sharedWidth = Math.Max(1, maxRight - minLeft);
            int sharedHeight = Math.Max(1, maxBottom - minTop);

            for (int i = 0; i < parsed.CompositeRecords.Count; i++)
            {
                var record = parsed.CompositeRecords[i];
                using var rawFrame = RenderCompositeFrame(parsed, record, palette, transparentIndex);

                if (saveRawFrames)
                {
                    string rawPath = Path.Combine(rawDirectory, $"frame_{i:D4}_off_{record.SourceOffset:X4}.png");
                    rawFrame.SaveAsPng(rawPath);
                }

                using var aligned = new Image<Rgba32>(sharedWidth, sharedHeight, new Rgba32(0, 0, 0, 0));
                int dstX = record.AnchorX - minLeft;
                int dstY = record.AnchorY - minTop;
                BlitImage(rawFrame, aligned, dstX, dstY);

                string alignedPath = Path.Combine(outputDirectory, $"frame_{i:D4}_off_{record.SourceOffset:X4}.png");
                aligned.SaveAsPng(alignedPath);
            }
        }

        private static List<BlockRegion> ReadHeaderBlocks(BinaryReader br, long fileLength)
        {
            var blocks = new List<BlockRegion>(5);
            for (int i = 0; i < 5; i++)
            {
                uint offset = br.ReadUInt32();
                uint size = br.ReadUInt32();

                if ((ulong)offset + size > (ulong)fileLength)
                    throw new InvalidDataException($"Header block {i} points outside file: off=0x{offset:X8} size=0x{size:X8}");

                blocks.Add(new BlockRegion
                {
                    Index = i + 1,
                    Offset = offset,
                    Size = size
                });
            }

            return blocks;
        }

        private static byte[][] ReadRawBlocks(BinaryReader br, List<BlockRegion> blocks, long fileLength)
        {
            var raw = new byte[5][];

            // We keep all 5 blocks to support descriptor->pixel data offsets.
            for (int i = 0; i < 5; i++)
            {
                var block = blocks[i];
                if (block.IsEmpty)
                {
                    raw[i] = Array.Empty<byte>();
                    continue;
                }

                br.BaseStream.Seek(block.Offset, SeekOrigin.Begin);
                raw[i] = br.ReadBytes(checked((int)block.Size));

                if (raw[i].Length != block.Size)
                    throw new EndOfStreamException($"Failed to read block {block.Index}: expected {block.Size} bytes");
            }

            return raw;
        }

        private static void ResolvePaletteAndPixelData(byte[] block4, byte[] block5, out byte[] palette, out byte[] pixelData)
        {
            // Most common: block4=palette (1024 RGBX), block5=pixels.
            if (block4.Length == 1024)
            {
                palette = block4;
                pixelData = block5;
                return;
            }

            // Fallback observed in a few engines/resources: swapped metadata blocks.
            if (block5.Length == 1024)
            {
                palette = block5;
                pixelData = block4;
                return;
            }

            // Unknown case: keep parser permissive.
            palette = Array.Empty<byte>();
            pixelData = block5.Length != 0 ? block5 : block4;
        }

        private static List<uint> ParseFrameOffsets(byte[] block)
        {
            var offsets = new List<uint>();
            if (block.Length == 0)
                return offsets;

            if ((block.Length % 4) != 0)
                throw new InvalidDataException($"Frame-offset block length must be multiple of 4 (got {block.Length})");

            using var ms = new MemoryStream(block, writable: false);
            using var br = new BinaryReader(ms);

            int count = block.Length / 4;
            offsets.Capacity = count;

            for (int i = 0; i < count; i++)
                offsets.Add(br.ReadUInt32());

            return offsets;
        }

        private static List<CompositeFrameRecord> ParseCompositeRecords(byte[] block, List<uint> frameOffsets)
        {
            var records = new List<CompositeFrameRecord>();
            if (block.Length == 0 || frameOffsets.Count == 0)
                return records;

            // Offsets often repeat (same composite reused by multiple frame indices).
            var visited = new HashSet<uint>();

            foreach (uint off in frameOffsets)
            {
                if (off >= block.Length)
                    continue;

                if (!visited.Add(off))
                    continue;

                records.Add(ParseOneCompositeRecord(block, checked((int)off)));
            }

            records.Sort((a, b) => a.SourceOffset.CompareTo(b.SourceOffset));
            return records;
        }

        private static CompositeFrameRecord ParseOneCompositeRecord(byte[] block, int offset)
        {
            if (offset + 10 > block.Length)
                throw new InvalidDataException($"Composite record header truncated at block2+0x{offset:X}");

            ushort partCount = ReadU16(block, offset + 0);
            short anchorX = unchecked((short)ReadU16(block, offset + 2));
            short anchorY = unchecked((short)ReadU16(block, offset + 4));
            ushort frameWidth = ReadU16(block, offset + 6);
            ushort frameHeight = ReadU16(block, offset + 8);

            int totalSize = 10 + (partCount * 6);
            if (offset + totalSize > block.Length)
                throw new InvalidDataException($"Composite record overflows block2 at 0x{offset:X}: partCount={partCount} totalSize={totalSize}");

            var parts = new List<FramePartRef>(partCount);
            int cursor = offset + 10;

            for (int i = 0; i < partCount; i++)
            {
                ushort spriteDescIndex = ReadU16(block, cursor + 0);
                short partOffsetX = unchecked((short)ReadU16(block, cursor + 2));
                short partOffsetY = unchecked((short)ReadU16(block, cursor + 4));

                parts.Add(new FramePartRef
                {
                    SpriteDescriptorIndex = spriteDescIndex,
                    OffsetX = partOffsetX,
                    OffsetY = partOffsetY
                });

                cursor += 6;
            }

            return new CompositeFrameRecord(offset, partCount, anchorX, anchorY, frameWidth, frameHeight, parts);
        }

        private static List<SpriteDescriptor> ParseSpriteDescriptors(byte[] block)
        {
            var descriptors = new List<SpriteDescriptor>();
            if (block.Length == 0)
                return descriptors;

            if ((block.Length % 12) != 0)
                throw new InvalidDataException($"Sprite descriptor block length must be multiple of 12 (got {block.Length})");

            using var ms = new MemoryStream(block, writable: false);
            using var br = new BinaryReader(ms);

            int count = block.Length / 12;
            descriptors.Capacity = count;

            for (int i = 0; i < count; i++)
            {
                ushort w = br.ReadUInt16();
                ushort h = br.ReadUInt16();
                uint pixelLen = br.ReadUInt32();
                uint pixelOffset = br.ReadUInt32();

                descriptors.Add(new SpriteDescriptor
                {
                    Index = i,
                    Width = w,
                    Height = h,
                    PixelLength = pixelLen,
                    PixelDataOffset = pixelOffset
                });
            }

            return descriptors;
        }

        private static void ValidateDescriptorReferences(List<CompositeFrameRecord> compositeRecords, int descriptorCount)
        {
            foreach (var record in compositeRecords)
            {
                foreach (var part in record.Parts)
                {
                    if (part.SpriteDescriptorIndex >= descriptorCount)
                    {
                        throw new InvalidDataException(
                          $"Composite record at block2+0x{record.SourceOffset:X} references sprite descriptor {part.SpriteDescriptorIndex}, " +
                          $"but descriptor count is {descriptorCount}");
                    }
                }
            }
        }

        private static Rgba32[] ConvertRgbxPalette(byte[] rgbxPalette, byte transparentIndex)
        {
            if (rgbxPalette.Length < 1024)
                throw new InvalidDataException("RGBX palette must contain 1024 bytes (256 * 4).");

            var palette = new Rgba32[256];
            for (int i = 0; i < 256; i++)
            {
                int p = i * 4;
                byte r = rgbxPalette[p + 0];
                byte g = rgbxPalette[p + 1];
                byte b = rgbxPalette[p + 2];
                byte a = i == transparentIndex ? (byte)0 : (byte)255;
                palette[i] = new Rgba32(r, g, b, a);
            }

            return palette;
        }

        private static Image<Rgba32> RenderCompositeFrame(ParsedScmFile parsed, CompositeFrameRecord record, Rgba32[] palette, byte transparentIndex)
        {
            int width = Math.Max((ushort)1, record.FrameWidth);
            int height = Math.Max((ushort)1, record.FrameHeight);
            var image = new Image<Rgba32>(width, height, new Rgba32(0, 0, 0, 0));

            // Draw order is preserved from part array order (part0 then part1, ...).
            foreach (var part in record.Parts)
            {
                if (part.SpriteDescriptorIndex >= parsed.SpriteDescriptors.Count)
                    continue;

                var descriptor = parsed.SpriteDescriptors[part.SpriteDescriptorIndex];
                ReadOnlySpan<byte> spritePixels = GetDescriptorPixels(parsed.PixelDataBlock, descriptor);

                int dstX = part.OffsetX - record.AnchorX;
                int dstY = part.OffsetY - record.AnchorY;

                BlitIndexedSprite(image, spritePixels, descriptor.Width, descriptor.Height, dstX, dstY, palette, transparentIndex);
            }

            return image;
        }

        private static ReadOnlySpan<byte> GetDescriptorPixels(byte[] pixelDataBlock, SpriteDescriptor descriptor)
        {
            int offset = checked((int)descriptor.PixelDataOffset);
            int length = checked((int)descriptor.PixelLength);

            if (offset < 0 || length < 0 || offset + length > pixelDataBlock.Length)
            {
                throw new InvalidDataException(
                  $"Descriptor {descriptor.Index} pixel range out of bounds: off=0x{descriptor.PixelDataOffset:X8} len=0x{descriptor.PixelLength:X8}");
            }

            return new ReadOnlySpan<byte>(pixelDataBlock, offset, length);
        }

        private static void BlitIndexedSprite(
          Image<Rgba32> dst,
          ReadOnlySpan<byte> src,
          int srcWidth,
          int srcHeight,
          int dstX,
          int dstY,
          Rgba32[] palette,
          byte transparentIndex)
        {
            if (srcWidth <= 0 || srcHeight <= 0)
                return;

            int expected = srcWidth * srcHeight;
            if (src.Length < expected)
                throw new InvalidDataException($"Sprite pixel data truncated: expected {expected} bytes, got {src.Length}");

            for (int y = 0; y < srcHeight; y++)
            {
                int ty = dstY + y;
                if ((uint)ty >= (uint)dst.Height)
                    continue;

                int row = y * srcWidth;
                for (int x = 0; x < srcWidth; x++)
                {
                    int tx = dstX + x;
                    if ((uint)tx >= (uint)dst.Width)
                        continue;

                    byte index = src[row + x];
                    if (index == transparentIndex)
                        continue;

                    dst[tx, ty] = palette[index];
                }
            }
        }

        private static void BlitImage(Image<Rgba32> src, Image<Rgba32> dst, int dstX, int dstY)
        {
            for (int y = 0; y < src.Height; y++)
            {
                int ty = dstY + y;
                if ((uint)ty >= (uint)dst.Height)
                    continue;

                for (int x = 0; x < src.Width; x++)
                {
                    int tx = dstX + x;
                    if ((uint)tx >= (uint)dst.Width)
                        continue;

                    var c = src[x, y];
                    if (c.A == 0)
                        continue;

                    dst[tx, ty] = c;
                }
            }
        }

        private static ushort ReadU16(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }
    }
}

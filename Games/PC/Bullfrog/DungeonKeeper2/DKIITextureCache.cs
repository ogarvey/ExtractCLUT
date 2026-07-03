using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ExtractCLUT.Games.PC.Bullfrog.DungeonKeeper2
{
    /// <summary>
    /// Dungeon Keeper 2 texture cache (DK2TextureCache\EngineTextures.dat + .dir) extractor.
    ///
    /// .dir: "TCHC", u32 fileSize, u32 version, u32 entryCount, then per entry:
    ///       NUL-terminated name, u32 offset into the .dat file.
    /// .dat record at each offset:
    ///       u32 width, u32 height, u32 size (of the rest of the record, from sResX on),
    ///       u16 sResX, u16 sResY, u32 flags (bit7 = has alpha), then (size - 8) bytes
    ///       of compressed data, decoded by <see cref="Dk2TextureDecoder"/>.
    ///
    /// Decompression is a DCT-based codec; port of the OpenKeeper implementation
    /// (Java port of C decoding code by George Gensure).
    /// </summary>
    public static class DKIITextureCache
    {
        private const string DirMagic = "TCHC";

        public sealed class TextureEntry
        {
            public required string Name { get; init; }
            public uint Offset { get; init; }
        }

        /// <summary>Reads the TCHC directory file.</summary>
        public static List<TextureEntry> ReadDir(string dirPath)
        {
            var data = File.ReadAllBytes(dirPath);
            if (Encoding.ASCII.GetString(data, 0, 4) != DirMagic)
                throw new InvalidDataException("Bad .dir magic, expected 'TCHC'.");

            uint entryCount = BitConverter.ToUInt32(data, 12);
            var entries = new List<TextureEntry>((int)entryCount);

            int pos = 16;
            for (int i = 0; i < entryCount && pos < data.Length; i++)
            {
                int start = pos;
                while (data[pos] != 0) pos++;
                string name = Encoding.ASCII.GetString(data, start, pos - start);
                pos++; // NUL

                uint offset = BitConverter.ToUInt32(data, pos);
                pos += 4;

                entries.Add(new TextureEntry { Name = name, Offset = offset });
            }

            return entries;
        }

        /// <summary>Decodes a single texture record from the .dat data.</summary>
        public static Image<Rgba32> DecodeTexture(byte[] dat, TextureEntry entry)
        {
            int o = (int)entry.Offset;
            int width = BitConverter.ToInt32(dat, o);
            int height = BitConverter.ToInt32(dat, o + 4);
            int size = BitConverter.ToInt32(dat, o + 8) - 8; // remaining after the 8 header bytes below
            uint flags = BitConverter.ToUInt32(dat, o + 16);
            bool alphaFlag = (flags >> 7) != 0;

            int words = size / 4;
            var buf = new uint[words];
            for (int i = 0; i < words; i++)
                buf[i] = BitConverter.ToUInt32(dat, o + 20 + i * 4);

            var decoder = new Dk2TextureDecoder();
            byte[] pixels = decoder.DecodeTexture(buf, width, height, alphaFlag);

            return Image.LoadPixelData<Rgba32>(pixels, width, height);
        }

        /// <summary>Extracts every texture in the cache as PNG files.</summary>
        public static void ExtractAll(string cacheDir, string outputDir)
        {
            var dirPath = Path.Combine(cacheDir, "EngineTextures.dir");
            var datPath = Path.Combine(cacheDir, "EngineTextures.dat");

            var entries = ReadDir(dirPath);
            var dat = File.ReadAllBytes(datPath);
            Console.WriteLine($"EngineTextures cache: {entries.Count} textures.");

            int failures = 0;
            foreach (var entry in entries)
            {
                var relative = entry.Name.Replace('/', '\\').TrimStart('\\');
                if (relative.Contains(".."))
                {
                    Console.WriteLine($"  Skipping suspicious entry name: {entry.Name}");
                    continue;
                }

                var target = Path.Combine(outputDir, relative + ".png");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);

                try
                {
                    using var image = DecodeTexture(dat, entry);
                    image.SaveAsPng(target);
                }
                catch (Exception ex)
                {
                    failures++;
                    Console.WriteLine($"  FAILED {entry.Name}: {ex.Message}");
                }
            }

            Console.WriteLine($"  Done. {entries.Count - failures} extracted, {failures} failed.");
        }
    }

    /// <summary>
    /// DK2 engine-texture decompressor. Direct port of OpenKeeper's
    /// Dk2TextureDecoder / EngineTextureDecoder (GPL-3.0, original C code by
    /// George Gensure) - a DCT-style codec working on 8x8 blocks with
    /// delta-coded DC per channel and Huffman-ish AC coefficient coding.
    /// </summary>
    public sealed class Dk2TextureDecoder
    {
        #region Tables

        private static readonly int[] MagicInputTable =
        {
            0x2000, 0x1712, 0x187E, 0x1B37, 0x2000, 0x28BA, 0x3B21, 0x73FC, 0x1712,
            0x10A2, 0x11A8, 0x139F, 0x1712, 0x1D5D, 0x2AA1, 0x539F, 0x187E, 0x11A8,
            0x12BF, 0x14D4, 0x187E, 0x1F2C, 0x2D41, 0x58C5, 0x1B37, 0x139F, 0x14D4,
            0x1725, 0x1B37, 0x22A3, 0x3249, 0x62A3, 0x2000, 0x1712, 0x187E, 0x1B37,
            0x2000, 0x28BA, 0x3B21, 0x73FC, 0x28BA, 0x1D5D, 0x1F2C, 0x22A3, 0x28BA,
            0x33D6, 0x4B42, 0x939F, 0x3B21, 0x2AA1, 0x2D41, 0x3249, 0x3B21, 0x4B42,
            0x6D41, 0xD650, 0x73FC, 0x539F, 0x58C5, 0x62A3, 0x73FC, 0x939F, 0xD650,
            0x1A463
        };

        private static readonly int[] DcControlTable =
        {
            0x00000000, 0x0000003f, 0x00000037, 0x0000003e,
            0x0000003d, 0x00000036, 0x0000002f, 0x00000027,
            0x0000002e, 0x00000035, 0x0000003c, 0x0000003b,
            0x00000034, 0x0000002d, 0x00000026, 0x0000001f,
            0x00000017, 0x0000001e, 0x00000025, 0x0000002c,
            0x00000033, 0x0000003a, 0x00000039, 0x00000032,
            0x0000002b, 0x00000024, 0x0000001d, 0x00000016,
            0x0000000f, 0x00000007, 0x0000000e, 0x00000015,
            0x0000001c, 0x00000023, 0x0000002a, 0x00000031,
            0x00000038, 0x00000030, 0x00000029, 0x00000022,
            0x0000001b, 0x00000014, 0x0000000d, 0x00000006,
            0x00000005, 0x0000000c, 0x00000013, 0x0000001a,
            0x00000021, 0x00000028, 0x00000020, 0x00000019,
            0x00000012, 0x0000000b, 0x00000004, 0x00000003,
            0x0000000a, 0x00000011, 0x00000018, 0x00000010,
            /* 60 */
            0x00000009, 0x00000002, 0x00000001, 0x00000008,
            0x00040102, 0x00040301, 0x00030201, 0x00030201,
            0x00024100, 0x00024100, 0x00024100, 0x00024100,
            /* 72 */
            0x00020101, 0x00020101, 0x00020101, 0x00020101,
            0x00064200, 0x00064200, 0x00064200, 0x00064200,
            0x00070302, 0x00070302, 0x00070a01, 0x00070a01,
            0x00070104, 0x00070104, 0x00070901, 0x00070901,
            0x00060801, 0x00060801, 0x00060801, 0x00060801,
            0x00060701, 0x00060701, 0x00060701, 0x00060701,
            0x00060202, 0x00060202, 0x00060202, 0x00060202,
            0x00060601, 0x00060601, 0x00060601, 0x00060601,
            0x00080e01, 0x00080106, 0x00080d01, 0x00080c01,
            0x00080402, 0x00080203, 0x00080105, 0x00080b01,
            0x00050103, 0x00050103, 0x00050103, 0x00050103,
            0x00050103, 0x00050103, 0x00050103, 0x00050103,
            0x00050501, 0x00050501, 0x00050501, 0x00050501,
            0x00050501, 0x00050501, 0x00050501, 0x00050501,
            /* 128 */
            0x00050401, 0x00050401, 0x00050401, 0x00050401,
            0x00050401, 0x00050401, 0x00050401, 0x00050401,
            0x000a1101, 0x000a0602, 0x000a0107, 0x000a0303,
            0x000a0204, 0x000a1001, 0x000a0f01, 0x000a0502,
            /* 144 */
            0x000c010b, 0x000c0902, 0x000c0503, 0x000c010a,
            0x000c0304, 0x000c0802, 0x000c1601, 0x000c1501,
            0x000c0109, 0x000c1401, 0x000c1301, 0x000c0205,
            0x000c0403, 0x000c0108, 0x000c0702, 0x000c1201,
            /* 160 */
            0x000d0b02, 0x000d0a02, 0x000d0603, 0x000d0404,
            0x000d0305, 0x000d0207, 0x000d0206, 0x000d010f,
            0x000d010e, 0x000d010d, 0x000d010c, 0x000d1b01,
            0x000d1a01, 0x000d1901, 0x000d1801, 0x000d1701,
            /* 176 */
            0x000e011f, 0x000e011e, 0x000e011d, 0x000e011c,
            0x000e011b, 0x000e011a, 0x000e0119, 0x000e0118,
            0x000e0117, 0x000e0116, 0x000e0115, 0x000e0114,
            0x000e0113, 0x000e0112, 0x000e0111, 0x000e0110,
            /* 192 */
            0x000f0128, 0x000f0127, 0x000f0126, 0x000f0125,
            0x000f0124, 0x000f0123, 0x000f0122, 0x000f0121,
            0x000f0120, 0x000f020e, 0x000f020d, 0x000f020c,
            0x000f020b, 0x000f020a, 0x000f0209, 0x000f0208,
            0x00100212, 0x00100211, 0x00100210, 0x0010020f,
            0x00100703, 0x00101102, 0x00101002, 0x00100f02,
            0x00100e02, 0x00100d02, 0x00100c02, 0x00102001,
            0x00101f01, 0x00101e01, 0x00101d01, 0x00101c01
        };

        private static readonly short[] JumpTable =
        {
            0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2,
            0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2,
            0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2,
            0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2,
            0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2,
            0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x2, 0x12, 0x12,
            0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12,
            0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12,
            0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12,
            0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12,
            0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12,
            0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x12, 0x22, 0x22, 0x22, 0x22,
            0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22,
            0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22,
            0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22,
            0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22,
            0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22, 0x22,
            0x22, 0x22, 0x22, 0x22, 0x22, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33,
            0x33, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33,
            0x33, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33,
            0x33, 0x33, 0x33, 0x33, 0x44, 0x44, 0x44, 0x44, 0x44, 0x44, 0x44,
            0x44, 0x44, 0x44, 0x44, 0x44, 0x44, 0x44, 0x44, 0x44, 0x55, 0x55,
            0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x66, 0x66, 0x66, 0x66, 0x77,
            0x77, 0x88, 0x0
        };

        private static readonly int[] MagicOutputTable = BuildMagicOutputTable();

        private static int[] BuildMagicOutputTable()
        {
            var table = new int[64];
            for (int i = 0; i < 64; i++)
            {
                int d = (int)((MagicInputTable[i] & 0xfffe0000) >> 3);
                int a = (MagicInputTable[i] & 0x0001ffff) << 3;
                table[i] = d + a;
            }
            return table;
        }

        private const int Norm = 0x5A82799A;
        private const float F03c = 5.4119611e-1f;
        private const float F040 = 1.306563f;
        private const float F044 = 3.8268343e-1f;

        private const float F000 = 1.048576e6f;
        private const float F004 = 8.388608e6f;
        private const float F008 = 1.169f;
        private const float F00c = -8.1300002e-1f;
        private const float F010 = -3.91e-1f;
        private const float F014 = 1.602f;
        private const float F018 = 2.0250001f;
        private const double D048 = 6.75539944108852e15;

        #endregion

        private uint[] _bs = Array.Empty<uint>();
        private long _bsIndex;
        private long _red, _green, _blue, _alpha;
        private readonly int[] _chunk2 = new int[256];
        private readonly int[] _chunk3 = new int[288];
        private readonly int[] _chunk4 = new int[512];

        /// <summary>Decodes a full texture into RGBA8 pixel bytes (width*height*4).</summary>
        public byte[] DecodeTexture(uint[] buf, int width, int height, bool alphaFlag)
        {
            int stride = width * 4;
            var outBuf = new byte[width * height * 4];

            _bs = buf;
            _bsIndex = 0;
            _red = _green = _blue = _alpha = 0;

            for (int y = 0; y < height; y += 8)
                for (int x = 0; x < width; x += 8)
                    DecompressBlock(outBuf, y * stride + x * 4, stride, alphaFlag);

            return outBuf;
        }

        private void DecompressBlock(byte[] outBuf, int outPos, int stride, bool alphaFlag)
        {
            Decompress(alphaFlag);

            int inp = 0;
            for (int j = 0; j < 8; j++)
            {
                for (int i = 0; i < 8; i++)
                {
                    float r = _chunk4[inp + i];
                    float g = _chunk4[inp + i + 18];
                    float b = _chunk4[inp + i + 9];
                    int a = _chunk4[inp + i + 27];

                    double d = F014 * (g - F004) + F008 * (r - F000) + D048;
                    int ir = (int)((long)(d + (d > 0 ? 0.5f : -0.5f)) & 0xFFFFFFFFL);
                    d = F018 * (b - F004) + F008 * (r - F000) + D048;
                    int ig = (int)((long)(d + (d > 0 ? 0.5f : -0.5f)) & 0xFFFFFFFFL);
                    d = F010 * (b - F004) + F00c * (g - F004) + F008 * (r - F000) + D048;
                    int ib = (int)((long)(d + (d > 0 ? 0.5f : -0.5f)) & 0xFFFFFFFFL);

                    int value = Clamp(ir >> 16, 0, 255);
                    value |= Clamp(ig >> 16, 0, 255) << 16;
                    value |= Clamp(ib >> 16, 0, 255) << 8;
                    value |= alphaFlag ? Clamp(a >> 16, 0, 255) << 24 : unchecked((int)0xff000000);

                    int p = outPos + i * 4;
                    if (p + 3 < outBuf.Length)
                    {
                        outBuf[p] = (byte)value;             // R
                        outBuf[p + 1] = (byte)(value >> 8);  // G
                        outBuf[p + 2] = (byte)(value >> 16); // B
                        outBuf[p + 3] = (byte)(value >> 24); // A
                    }
                }
                outPos = Math.Min(outBuf.Length, outPos + stride);
                inp += 64;
            }
        }

        private void Decompress(bool alphaFlag)
        {
            _red = DecompressChannel(_red, 0);
            _green = DecompressChannel(_green, 9);
            _blue = DecompressChannel(_blue, 18);
            if (alphaFlag)
                _alpha = DecompressChannel(_alpha, 27);
        }

        /// <summary>
        /// Decodes one channel of the 8x8 block into _chunk4 at the given offset,
        /// returns the updated channel DC accumulator.
        /// </summary>
        private long DecompressChannel(long dc, int channelOffset)
        {
            int bsPos = (int)_bsIndex;
            int value = 0;

            int jtIndex = (int)BsRead(bsPos, 8);
            int jtValue = JumpTable[jtIndex];
            bsPos += jtValue & 0xf;
            jtValue >>= 4;
            if (jtValue != 0)
            {
                // Signed value
                value = (int)BsRead(bsPos, jtValue);
                if ((value & (1 << (jtValue - 1))) == 0)
                    value -= (1 << jtValue) - 1;
                bsPos += jtValue;
            }

            dc += value;

            int blanketFill = (int)BsRead(bsPos, 2);
            if (blanketFill == 2)
            {
                bsPos += 2;
                for (int j = 0; j < 8; j++)
                    for (int i = 0; i < 8; i++)
                        _chunk4[j * 64 + i + channelOffset] = (int)dc << 16;
                _bsIndex = bsPos;
            }
            else
            {
                _bsIndex = PrepareDecompress((int)dc, bsPos);
                for (int i = 0; i < 8; i++)
                    Func1(_chunk2, i * 8, _chunk3, i);
                for (int i = 0; i < 8; i++)
                    Func2(_chunk3, i * 9, _chunk4, i * 64 + channelOffset);
            }

            return dc;
        }

        private long BsRead(int pos, int bits)
        {
            int wordIndex = pos >> 5;
            int shamt = pos & 0x1f;
            ulong w1 = ((ulong)_bs[wordIndex] << shamt) & 0xFFFFFFFFUL;
            ulong w2 = shamt != 0 && wordIndex + 1 < _bs.Length
                ? ((ulong)_bs[wordIndex + 1] >> (32 - shamt)) & 0xFFFFFFFFUL
                : 0;
            w1 |= w2;
            return (long)(w1 >> (32 - bits));
        }

        private long PrepareDecompress(int value, int pos)
        {
            int xindex = 0, index = 0, controlWord = 0;
            int magicIndex = 0x3f;
            bool areWeDone = false;

            _chunk2[0] = value * MagicOutputTable[0];
            Array.Clear(_chunk2, 1, _chunk2.Length - 1);

            while (true)
            {
                if (!areWeDone)
                    xindex = index = (int)BsRead(pos, 17);

                if (index >= 0x8000 || areWeDone)
                {
                    if (!areWeDone)
                    {
                        index >>= 13;
                        controlWord = DcControlTable[60 + index];
                    }
                    areWeDone = false;

                    if ((controlWord & 0xff00) == 0x4100)
                        return pos + (controlWord >> 16);

                    if ((controlWord & 0xff00) > 0x4100)
                    {
                        // Escape: read explicit run/level
                        pos += controlWord >> 16;
                        int unk14 = (int)BsRead(pos, 14);
                        pos += 14;
                        magicIndex -= (unk14 & 0xff00) >> 8;
                        unk14 &= 0xff;
                        if (unk14 != 0)
                        {
                            if (unk14 != 0x80)
                            {
                                if (unk14 > 0x80)
                                    unk14 -= 0x100;
                                magicIndex--;
                            }
                            else
                            {
                                unk14 = (int)BsRead(pos, 8);
                                pos += 8;
                                unk14 -= 0x100;
                            }
                        }
                        else
                        {
                            unk14 = (int)BsRead(pos, 8);
                            pos += 8;
                        }
                        controlWord = unk14;
                    }
                    else
                    {
                        int rem = controlWord >> 16;
                        int xoramt = 0;

                        magicIndex -= (controlWord & 0xff00) >> 8;
                        int bitToTest = 16 - rem;
                        if ((xindex & (1 << bitToTest)) != 0)
                            xoramt = ~0;
                        controlWord &= 0xff;
                        controlWord ^= xoramt;
                        pos++;
                        controlWord -= xoramt;
                        pos += rem;
                    }

                    int outIndex = DcControlTable[magicIndex + 1];
                    _chunk2[outIndex] = unchecked((short)controlWord * MagicOutputTable[outIndex]);
                }
                else if (index >= 0x800)
                {
                    index >>= 9;
                    controlWord = DcControlTable[72 + index];
                    areWeDone = true;
                }
                else if (index >= 0x400)
                {
                    index >>= 7;
                    controlWord = DcControlTable[128 + index];
                    areWeDone = true;
                }
                else if (index >= 0x200)
                {
                    index >>= 5;
                    controlWord = DcControlTable[128 + index];
                    areWeDone = true;
                }
                else if (index >= 0x100)
                {
                    index >>= 4;
                    controlWord = DcControlTable[144 + index];
                    areWeDone = true;
                }
                else if (index >= 0x80)
                {
                    index >>= 3;
                    controlWord = DcControlTable[160 + index];
                    areWeDone = true;
                }
                else if (index >= 0x40)
                {
                    index >>= 2;
                    controlWord = DcControlTable[176 + index];
                    areWeDone = true;
                }
                else if (index >= 0x20)
                {
                    index >>= 1;
                    controlWord = DcControlTable[192 + index];
                    areWeDone = true;
                }
            }
        }

        /// <summary>Column pass of the inverse transform (8 inputs -> 8 outputs spaced 9 apart).</summary>
        private static void Func1(int[] input, int inOff, int[] output, int outOff)
        {
            unchecked
            {
                if ((input[inOff + 1] | input[inOff + 2] | input[inOff + 3] | input[inOff + 4]
                    | input[inOff + 6] | input[inOff + 7]) == 0)
                {
                    int v = input[inOff];
                    output[outOff] = v;
                    output[outOff + 9] = v;
                    output[outOff + 18] = v;
                    output[outOff + 27] = v;
                    output[outOff + 36] = v;
                    output[outOff + 45] = v;
                    output[outOff + 54] = v;
                    output[outOff + 63] = v;
                    return;
                }

                int b = input[inOff + 5] - input[inOff + 3];
                int c = input[inOff + 1] - input[inOff + 7];
                int i = input[inOff + 3] + input[inOff + 5];
                int a = input[inOff + 7] + input[inOff + 1];
                double xf = b, xg = c;
                int p = i + a;
                a -= i;

                double rxs = xg + xf;
                double rxf = xf * F03c + F044 * rxs;
                double rxg = xg * F040 - F044 * rxs;
                int ra = (int)(rxf + (rxf > 0 ? 0.5f : -0.5f));
                int rb = (int)(rxg + (rxg > 0 ? 0.5f : -0.5f));

                long rx = (long)a * Norm;
                int d = (int)(rx >> 32);

                b = input[inOff + 6];
                d += d;
                a = input[inOff + 2];

                c = ra;
                i = rb;
                c += d;
                d += i;
                i += p;
                long sc = c & 0xFFFFFFFFL;
                long sd = d & 0xFFFFFFFFL;
                long si = i & 0xFFFFFFFFL;
                c = input[inOff];
                d = input[inOff + 4];
                int s = b + a;
                a -= b;
                b = d + c;
                c -= d;

                rx = (long)a * Norm;
                d = (int)(rx >> 32);

                d += d;
                output[outOff + 18] = (int)((c - d) + sc);
                output[outOff + 45] = (int)((c - d) - sc);
                output[outOff + 27] = (b - (s + d)) + ra;
                output[outOff + 36] = (b - (s + d)) - ra;
                output[outOff + 0] = (int)((s + d) + b + si);
                output[outOff + 9] = (int)(sd + d + c);
                output[outOff + 54] = (int)(d + c - sd);
                output[outOff + 63] = (int)((s + d) + b - si);
            }
        }

        /// <summary>Row pass of the inverse transform (9-spaced inputs -> 8 contiguous outputs).</summary>
        private static void Func2(int[] input, int inOff, int[] output, int outOff)
        {
            unchecked
            {
                int b = input[inOff + 5] - input[inOff + 3];
                int c = input[inOff + 1] - input[inOff + 7];
                int i = input[inOff + 3] + input[inOff + 5];
                int a = input[inOff + 7] + input[inOff + 1];
                double xf = b, xg = c;
                int p = i + a;
                a -= i;

                double rxs = xg + xf;
                double rxf = xf * F03c + F044 * rxs;
                double rxg = xg * F040 - F044 * rxs;
                int ra = (int)(rxf + (rxf > 0 ? 0.5f : -0.5f));
                int rb = (int)(rxg + (rxg > 0 ? 0.5f : -0.5f));

                long rx = (long)a * Norm;
                int d = (int)(rx >> 32);

                b = input[inOff + 6];
                d += d;
                a = input[inOff + 2];

                c = ra;
                i = rb;
                c += d;
                d += i;
                i += p;
                long sc = c & 0xFFFFFFFFL;
                long sd = d & 0xFFFFFFFFL;
                long si = i & 0xFFFFFFFFL;
                c = input[inOff];
                d = input[inOff + 4];
                int s = b + a;
                a -= b;
                b = d + c;
                c -= d;

                rx = (long)a * Norm;
                d = (int)(rx >> 32);

                d += d;
                p = (int)sc;
                s += d;
                a = d + c;
                c -= d;
                d = s + b;
                b -= s;
                s = c + p;
                c -= p;
                p = ra;
                output[outOff + 2] = s;
                s = (int)sd;
                output[outOff + 5] = c;
                c = b + p;
                b -= p;
                p = (int)si;
                output[outOff + 3] = c;
                output[outOff + 4] = b;
                b = s + a;
                a -= s;
                c = d + p;
                d -= p;
                output[outOff + 0] = c;
                output[outOff + 1] = b;
                output[outOff + 6] = a;
                output[outOff + 7] = d;
            }
        }

        private static int Clamp(int n, int min, int max) => n < min ? min : (n > max ? max : n);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.Septerra
{
    public static class SepterraDumper
    {

        private static readonly string[] KnownTags = { "LV25", "AM04", "CH14" };

        public static void DumpFromFile(string path)
        {
            var data = File.ReadAllBytes(path);
            Dump(data, Path.GetFileName(path));
        }

        public static void Dump(byte[] data, string label = "<buffer>")
        {
            if (data.Length < 8)
            {
                Console.WriteLine($"{label}: too small ({data.Length} bytes)");
                return;
            }

            var tagOffset = FindTagOffset(data);
            var tag = tagOffset >= 0 ? Encoding.ASCII.GetString(data, tagOffset, 4) : "????";
            var baseOffset = tagOffset >= 0 ? tagOffset : 0;

            Console.WriteLine($"=== {label} ===");
            Console.WriteLine($"Size: {data.Length} bytes");
            Console.WriteLine($"Tag: {tag} (offset {baseOffset})");
            Console.WriteLine($"Header bytes (first 128): {HexPreview(data, 0, Math.Min(128, data.Length))}");

            var dwords = ReadDwords(data, baseOffset, 64);
            Console.WriteLine("Header dwords (0..63):");
            for (var i = 0; i < dwords.Count; i++)
            {
                Console.WriteLine($"  [{i:D2}] 0x{dwords[i]:X8}");
            }

            if (tag == "LV25")
            {
                DumpLv25(dwords, baseOffset);
            }
            else if (tag == "AM04")
            {
                DumpAm04(dwords, baseOffset);
            }
            else if (tag == "CH14")
            {
                DumpCh14(dwords, baseOffset);
            }

            Console.WriteLine();
        }

        private static void DumpLv25(IReadOnlyList<uint> dwords, int baseOffset)
        {
            Console.WriteLine("LV25 (raw offsets to rebase):");
            var indices = new[]
            {
            4, 6, 8, 10, 0x0C, 0x0E, 0x10, 0x12, 0x14, 0x16,
            0x19, 0x1B, 0x1D, 0x1F, 0x21, 0x23, 0x25, 0x27,
            0x29, 0x2B, 0x2D, 0x2F, 0x31
        };

            DumpOffsets(indices, dwords, baseOffset);

            DumpIfPresent("Table @ +0x40", dwords, 0x40 / 4, baseOffset);
            DumpIfPresent("Count @ +0x44", dwords, 0x44 / 4, baseOffset, isCount: true);
            DumpIfPresent("Table @ +0x20", dwords, 0x20 / 4, baseOffset);
            DumpIfPresent("Count @ +0x24", dwords, 0x24 / 4, baseOffset, isCount: true);
            DumpIfPresent("Region count @ +0x68", dwords, 0x68 / 4, baseOffset, isCount: true);
            DumpIfPresent("Region table @ +0x64", dwords, 0x64 / 4, baseOffset);
            DumpIfPresent("Point list @ +0x28", dwords, 0x28 / 4, baseOffset);
            DumpIfPresent("Table @ +0x84", dwords, 0x84 / 4, baseOffset);
            DumpIfPresent("Count @ +0x88", dwords, 0x88 / 4, baseOffset, isCount: true);
            DumpIfPresent("Bounds base @ +0x8C", dwords, 0x8C / 4, baseOffset);
            DumpIfPresent("Bounds count @ +0x90", dwords, 0x90 / 4, baseOffset, isCount: true);
        }

        private static void DumpAm04(IReadOnlyList<uint> dwords, int baseOffset)
        {
            Console.WriteLine("AM04 (raw offsets to rebase):");
            var indices = new[] { 2, 4, 6, 8, 0x0C, 0x0E, 0x10 };
            DumpOffsets(indices, dwords, baseOffset);

            DumpIfPresent("Entry count @ [7]", dwords, 7, baseOffset, isCount: true);
        }

        private static void DumpCh14(IReadOnlyList<uint> dwords, int baseOffset)
        {
            Console.WriteLine("CH14 (raw offsets to rebase):");
            var indices = Enumerable.Range(2, 0x1B - 2 + 1).ToArray();
            DumpOffsets(indices, dwords, baseOffset);

            DumpIfPresent("Record list @ [2]", dwords, 2, baseOffset);
            DumpIfPresent("Record count @ [3]", dwords, 3, baseOffset, isCount: true);
            DumpIfPresent("TX00 id @ [1]", dwords, 1, baseOffset);
            DumpIfPresent("VSSS list @ [0x1A]", dwords, 0x1A, baseOffset);
            DumpIfPresent("VSSS count @ [0x1B]", dwords, 0x1B, baseOffset, isCount: true);
        }

        private static void DumpOffsets(IEnumerable<int> indices, IReadOnlyList<uint> dwords, int baseOffset)
        {
            foreach (var idx in indices)
            {
                if (idx < dwords.Count)
                {
                    var value = dwords[idx];
                    var absolute = baseOffset + (int)value;
                    Console.WriteLine($"  [{idx:X2}] 0x{value:X8} -> base+0x{value:X8} (abs {absolute})");
                }
            }
        }

        private static void DumpIfPresent(string label, IReadOnlyList<uint> dwords, int idx, int baseOffset, bool isCount = false)
        {
            if (idx >= dwords.Count) return;
            var value = dwords[idx];
            if (isCount)
            {
                Console.WriteLine($"  {label}: {value}");
                return;
            }
            var absolute = baseOffset + (int)value;
            Console.WriteLine($"  {label}: 0x{value:X8} -> abs {absolute}");
        }

        private static int FindTagOffset(byte[] data)
        {
            for (var i = 0; i <= Math.Min(16, data.Length - 4); i++)
            {
                var tag = Encoding.ASCII.GetString(data, i, 4);
                if (KnownTags.Contains(tag))
                {
                    return i;
                }
            }
            return -1;
        }

        private static List<uint> ReadDwords(byte[] data, int offset, int count)
        {
            var list = new List<uint>(count);
            for (var i = 0; i < count; i++)
            {
                var pos = offset + i * 4;
                if (pos + 4 > data.Length)
                {
                    break;
                }
                list.Add(BitConverter.ToUInt32(data, pos));
            }
            return list;
        }

        private static string HexPreview(byte[] data, int offset, int length)
        {
            var sb = new StringBuilder(length * 3);
            for (var i = 0; i < length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(data[offset + i].ToString("X2"));
            }
            return sb.ToString();
        }

    }
}

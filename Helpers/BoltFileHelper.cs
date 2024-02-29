using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Helpers
{
    public static class BoltFileHelper
    {
        public static int GetEndOfBoltData(byte[] data)
        {
            return BitConverter.ToInt32(data.Skip(0xC).Take(4).Reverse().ToArray(), 0);
        }

        public static int GetStartOfBoltData(byte[] data)
        {
            return BitConverter.ToInt32(data.Skip(0x18).Take(4).Reverse().ToArray(), 0);
        }

        public static List<int> GetBoltOffsets(byte[] data)
        {
            var offsets = new List<int>();

            var value = GetStartOfBoltData(data);
            offsets.Add(value);
            var index = 0x20;
            while (index < offsets[0])
            {
                value = BitConverter.ToInt32(data.Skip(index+8).Take(4).Reverse().ToArray(), 0);
                offsets.Add(value);
                index+=16;
            }

            return offsets;
        }

        public static List<BoltOffset> GetBoltOffsetData(byte[] data)
        {
            var offsets = new List<BoltOffset>();

            var dataStart = GetStartOfBoltData(data);
            var offsetData = new BoltOffset
            {
                Offset = dataStart,
                InitialDataLength = data.Skip(0x13).Take(1).First() * 0x10,
                SecondaryDataLength = BitConverter.ToInt32(data.Skip(0x14).Take(4).Reverse().ToArray(), 0)
            };
            offsets.Add(offsetData);
            var index = 0x20;
            while (index < dataStart)
            {
                var initDataLen = data.Skip(index+3).Take(1).First() * 0x10;
                var secDataLen = BitConverter.ToInt32(data.Skip(index + 4).Take(4).Reverse().ToArray(), 0);
                var offset = BitConverter.ToInt32(data.Skip(index + 8).Take(4).Reverse().ToArray(), 0);
                offsetData = new BoltOffset
                {
                    Offset = offset,
                    InitialDataLength = initDataLen,
                    SecondaryDataLength = secDataLen
                };
                offsets.Add(offsetData);
                index += 16;
            }

            return offsets;
        }

        public static void ExtractBoltData(string inputFile, string outputFolder)
        {
            var data = File.ReadAllBytes(inputFile);
            var offsets = GetBoltOffsetData(data);
            for (var i = 0; i < offsets.Count; i++)
            {
                var offset = offsets[i];
                var initialData = data.Skip(offset.Offset).Take(offset.InitialDataLength).ToArray();
                var secondaryData = data.Skip(offset.Offset + offset.InitialDataLength).Take(offset.SecondaryDataLength).ToArray();
                var boltOutputFolder = Path.Combine(outputFolder, "bolts");
                if (!Directory.Exists(boltOutputFolder))
                {
                    Directory.CreateDirectory(boltOutputFolder);
                }
                File.WriteAllBytes($"{boltOutputFolder}\\{offset.Offset}_Initial.bin", initialData);
                File.WriteAllBytes($"{boltOutputFolder}\\{offset.Offset}_Secondary.bin", secondaryData);

                var subDataOffset = 0;
                for (int j = 0, k = 0; j < initialData.Length; j += 16, k++)
                {
                    var byteCount = BitConverter.ToInt16(initialData.Skip(j + 6).Take(2).Reverse().ToArray());
                    var subFileBytes = secondaryData.Skip(subDataOffset + k + 1).Take(byteCount - 1).ToArray();
                    subDataOffset += byteCount;
                    var subBoltOutputFolder = Path.Combine(boltOutputFolder, "sub-bolts");
                    if (!Directory.Exists(subBoltOutputFolder))
                    {
                        Directory.CreateDirectory(subBoltOutputFolder);
                    }
                    File.WriteAllBytes($@"{subBoltOutputFolder}\sb_{offset.Offset}_{j}.bin", subFileBytes);
                }
            }
        }

        public static StringBuilder ParseInitialData1B(byte[] initialData)
        {
            var sb = new StringBuilder();
            var index = 0;
            while (index < initialData.Length)
            {
                var subData = initialData.Skip(index).Take(16).ToArray();
                var subDataOffset = subData.Take(1).First();
                var subDataLength = subData.Skip(1).Take(1).First();
                var subDataLength2 = subData.Skip(2).Take(1).First();
                var subDataLength3 = subData.Skip(3).Take(1).First();
                var subDataLength4 = subData.Skip(4).Take(1).First();
                var subDataLength5 = subData.Skip(5).Take(1).First();
                var subDataLength6 = subData.Skip(6).Take(1).First();
                var subDataLength7 = subData.Skip(7).Take(1).First();
                var subDataLength8 = subData.Skip(8).Take(1).First();
                var subDataLength9 = subData.Skip(9).Take(1).First();
                var subDataLength10 = subData.Skip(10).Take(1).First();
                var subDataLength11 = subData.Skip(11).Take(1).First();
                var subDataLength12 = subData.Skip(12).Take(1).First();
                var subDataLength13 = subData.Skip(13).Take(1).First();
                var subDataLength14 = subData.Skip(14).Take(1).First();
                var subDataLength15 = subData.Skip(15).Take(1).First();

                sb.AppendLine($"Index: {index}");
                sb.AppendLine("");
                sb.AppendLine($"Offset: {subDataOffset}, Length: {subDataLength}, Length2: {subDataLength2}, Length3: {subDataLength3}");
                sb.AppendLine($"Length4: {subDataLength4}, Length5: {subDataLength5}, Length6: {subDataLength6}, Length7: {subDataLength7}");
                sb.AppendLine($"Length8: {subDataLength8}, Length9: {subDataLength9}, Length10: {subDataLength10}, Length11: {subDataLength11}");
                sb.AppendLine($"Length12: {subDataLength12}, Length13: {subDataLength13}, Length14: {subDataLength14}, Length15: {subDataLength15}");
                sb.AppendLine("");

                index += 16;
            }
            return sb;
        }

        public static StringBuilder ParseInitialData2B(byte[] initialData)
        {
            var sb = new StringBuilder();
            var index = 0;
            while (index < initialData.Length)
            {
                var subData = initialData.Skip(index).Take(16).ToArray();
                var subDataOffset = BitConverter.ToUInt16(subData.Take(2).Reverse().ToArray(), 0);
                var subDataLength = BitConverter.ToUInt16(subData.Skip(2).Take(2).Reverse().ToArray(), 0);
                var subDataLength2 = BitConverter.ToUInt16(subData.Skip(4).Take(2).Reverse().ToArray(), 0);
                var subDataLength3 = BitConverter.ToUInt16(subData.Skip(6).Take(2).Reverse().ToArray(), 0);
                var subDataLength4 = BitConverter.ToUInt16(subData.Skip(8).Take(2).Reverse().ToArray(), 0);
                var subDataLength5 = BitConverter.ToUInt16(subData.Skip(10).Take(2).Reverse().ToArray(), 0);
                var subDataLength6 = BitConverter.ToUInt16(subData.Skip(12).Take(2).Reverse().ToArray(), 0);
                var subDataLength7 = BitConverter.ToUInt16(subData.Skip(14).Take(2).Reverse().ToArray(), 0);

                sb.AppendLine($"Index: {index}");
                sb.AppendLine("");
                sb.AppendLine($"Offset: {subDataOffset}, Length: {subDataLength}, Length2: {subDataLength2}, Length3: {subDataLength3}");
                sb.AppendLine($"Length4: {subDataLength4}, Length5: {subDataLength5}, Length6: {subDataLength6}, Length7: {subDataLength7}");
                sb.AppendLine("");

                index += 16;
            }
            return sb;
        }

        public static StringBuilder ParseInitialData4B(byte[] initialData)
        {
            var sb = new StringBuilder();
            var index = 0;
            while (index < initialData.Length)
            {
                var subData = initialData.Skip(index).Take(16).ToArray();
                var subDataOffset = BitConverter.ToUInt32(subData.Take(4).Reverse().ToArray(), 0);
                var subDataLength = BitConverter.ToUInt32(subData.Skip(4).Take(4).Reverse().ToArray(), 0);
                var subDataLength2 = BitConverter.ToUInt32(subData.Skip(8).Take(4).Reverse().ToArray(), 0);
                var subDataLength3 = BitConverter.ToUInt32(subData.Skip(12).Take(4).Reverse().ToArray(), 0);

                sb.AppendLine($"Index: {index}");
                sb.AppendLine("");
                sb.AppendLine($"Offset: {subDataOffset}, Length: {subDataLength}, Length2: {subDataLength2}, Length3: {subDataLength3}");
                sb.AppendLine("");

                index += 16;
            }
            return sb;
        }
    }

    public class BoltOffset
    {
        public int Offset { get; set; }
        public int InitialDataLength { get; set; }
        public int SecondaryDataLength { get; set; }
    }
}

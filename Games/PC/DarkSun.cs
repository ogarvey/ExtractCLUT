using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC
{
    public static class DarkSun
    {
        private static readonly byte GffRecordLength = 0xC;
        public static List<GffRecord> GffRecords { get; } = [];
        public static List<GffRecord> BmpRecords => GffRecords.Where(r => r.SectionType == GffSectionType.BMP).ToList();
        public static List<GffRecord> PalRecords => GffRecords.Where(r => r.SectionType == GffSectionType.PAL).ToList();
        
        public static void ParseGffFile(string filePath)
        {
            using var br = new BinaryReader(File.OpenRead(filePath));
            br.ReadBytes(0xc);
            var tableOffset = br.ReadUInt32();
            br.BaseStream.Seek(tableOffset+8, SeekOrigin.Begin);
            var sectionCount = br.ReadUInt16();
            for (int i = 0; i < sectionCount; i++)
            {
                var sectionType = br.ReadChars(4);
                var count = br.ReadUInt32();
                switch (new string(sectionType))
                {
                    case "BMP ":
                        // Process bitmap section // RLE of some sort (X >> 1) + 1 times following byte
                        for (int j = 0; j < count; j++)
                        {
                            var id = br.ReadUInt32();
                            var offset = br.ReadUInt32();
                            var size = br.ReadUInt32();
                            GffRecords.Add(new GffRecord(GffSectionType.BMP, id, offset, size));
                        }
                        break;
                    case "PAL ":
                        // Process palette section
                        for (int j = 0; j < count; j++)
                        {
                            var id = br.ReadUInt32();
                            var offset = br.ReadUInt32();
                            var size = br.ReadUInt32();
                            GffRecords.Add(new GffRecord(GffSectionType.PAL, id, offset, size));
                        }
                        break;
                    default:
                        // Unknown section type
                        for (int j = 0; j < count; j++)
                        {
                            var id = br.ReadUInt32();
                            var offset = br.ReadUInt32();
                            var size = br.ReadUInt32();
                        }
                        break;
                }
            }
        }
    }

    public record GffRecord(GffSectionType SectionType, uint Id, uint Offset, uint Size);

    public enum GffSectionType
    {
        MERR,
        ADV,
        CSEQ,
        GSEQ,
        LSEQ,
        PSEQ,
        BMP,
        PAL,
        BMA,
        ACF
    }
}

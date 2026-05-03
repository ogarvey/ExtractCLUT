using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.Septerra
{
    public class IdxEntry
    {
        // 32 byte record
        public ushort FileId { get; set; }
        public ushort Unk1 { get; set; }
        public uint VolumeIndex { get; set; }
        public uint Offset { get; set; }
        public uint UncompressedLength { get; set; }
        public byte IsCompressed { get; set; }
        public byte Unk3 { get; set; }
        public ushort Unk4 { get; set; }
        public uint CompressedLength { get; set; }
        public long FilenameHash { get; set; }
    }
}

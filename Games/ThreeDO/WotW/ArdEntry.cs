using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.ThreeDO.WotW
{
    public class ArdEntry
    {
        public byte Unk1 { get; set; }
        public byte Unk2 { get; set; }
        public byte Unk3 { get; set; }
        public byte Unk4 { get; set; }
        public byte StageId { get; set; }
        public byte ImageIndex { get; set; }
        public byte Unk5 { get; set; }
        public byte Unk6 { get; set; }
        public ushort Width { get; set; }
        public ushort Height { get; set; }
        public short OffsetX { get; set; }
        public short Unk7 { get; set; }
        public short OffsetY { get; set; }
        public short Unk8 { get; set; }
        public byte[] UnkRemainder { get; set; } = new byte[28];
    }
}

//0x765A8 AR1.ard entries

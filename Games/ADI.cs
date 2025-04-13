using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games
{
    public class ADI
    {
        
    }

    public class ADIIndexHeader
    {
        // 16 characters
        public string Filename { get; set; }
        // 4 byte BE
        public uint SectorIndex { get; set; }
        // 4 byte BE
        public uint Unknown { get; set; }
    }

    // whole header is 128 bytes
    public class ADISubFileHeader
    {
        // 15 characters
        public string Filename { get; set; }
        // 2 byte LE
        public ushort Type { get; set; }
        // 2 byte LE
        public ushort Unknown { get; set; }
        // 2 byte LE
        public ushort Channel { get; set; }
        // 4 byte LE
        public uint StartSector { get; set; } // offset from sectorIndex of the header sector
                                              // 4 byte LE
        public uint EndSector { get; set; } // actually seems to be next sector after this with EOR flag
    }
}

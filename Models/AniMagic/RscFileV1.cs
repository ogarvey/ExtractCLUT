using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Models.AniMagic
{
    public class RscFileV1 : RscFileBase
    {
        public RscFileV1(string filePath) : base(filePath)
        {
            _version = 1;
            using var rscReader = new BinaryReader(File.OpenRead(filePath));
            rscReader.BaseStream.Position = 0x4;
            _headerSize = rscReader.ReadUInt32(); // size of the header
            rscReader.BaseStream.Position = 0xC;
            _headerOffsetAdjustment = 0x10; // adjust the offset for version 1
            _headerCount = rscReader.ReadUInt16(); // number of headers
            rscReader.BaseStream.Position = _headerOffsetAdjustment; // move to the start of the headers

            ReadTableOffsets(rscReader);
        }
    }
}

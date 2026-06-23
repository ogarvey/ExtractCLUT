using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Models.AniMagic
{
    public class RscFileV0 : RscFileBase
    {
        public RscFileV0(string filePath) : base(filePath)
        {
            _version = 0;

            using var rscReader = new BinaryReader(File.OpenRead(filePath));
            
            _headerSize = rscReader.ReadUInt32(); // size of the header
            _headerCount = rscReader.ReadUInt16(); // number of headers
            _headerOffsetAdjustment = 0x6; // adjust the offset for version 0
            
            rscReader.BaseStream.Position = _headerOffsetAdjustment; // move to the start of the headers

            ReadTableOffsets(rscReader);
        }
    }
}

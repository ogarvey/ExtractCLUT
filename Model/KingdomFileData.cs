using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Model
{
    public class KingdomFileData
    {
        public int BytesToRead { get; set; }
        public int Filenumber { get; set; }
        public string? Filename { get; set; }
        public int Filesize { get; set; }
        public int Height { get; set; }
        public int PotentialSubFileCount { get; set; }
        public int Width { get; set; }
    }
}

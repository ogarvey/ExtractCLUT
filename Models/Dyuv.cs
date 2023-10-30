using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Models
{
    public class Dyuv
    {
        
    }

    public class DyuvFrameContainer
    {
        public int Lines { get; set; }
        public List<DyuvFrame> Frames { get; set; }
    }

    public class DyuvFrame 
    {
        public int Offset { get; set; }
        public int Sets { get; set; }
        public byte[] Bytes { get; set; }
    }
}

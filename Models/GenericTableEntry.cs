using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Models
{
    public class GenericTableEntry
    {
        public string Name { get; set; } = string.Empty;
        public uint Offset { get; set; }
        public uint Size { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.Wintermute
{
    public class MainData
    {
        MainDataHeader Header { get; set; }
    }

    public class MainDataHeader {
        public UInt32 DeadCode { get; set; } // 0x DE AD C0 DE
        public UInt32 Junk { get; set; } // "JUNK"
        public UInt32 PackageVersion { get; set; } // 0x200
        public UInt32 GameVersion { get; set; }
        public Byte Priority { get; set; }
        public Byte Cd { get; set; }
        public Boolean MasterIndex { get; set; }
        public UInt32 CreationTime { get; set; }
        public String Desc { get; set; }
        public UInt32 NumDirs { get; set; }
        public UInt32 DirOffset { get; set; }
    }
}

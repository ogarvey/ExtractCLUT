using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.AniMagic
{
    public class RscFile
    {
        public List<RscFileResource> BmpResources { get; set; }
        public List<RscFileResource> ScriptResources { get; set; }
        public List<RscFileResource> AnimResources { get; set; }
        public List<RscFileResource> PaletteResources { get; set; }
        public List<RscFileResource> WavResources { get; set; }
        public List<RscFileResource> BtnResources { get; set; }
        public List<RscFileResource> EventResources { get; set; }
        public List<RscFileResource> VarResources { get; set; }

        public RscFile(string filePath)
        {
            BmpResources = new List<RscFileResource>();
            ScriptResources = new List<RscFileResource>();
            AnimResources = new List<RscFileResource>();
            PaletteResources = new List<RscFileResource>();
            WavResources = new List<RscFileResource>();
            BtnResources = new List<RscFileResource>();
            EventResources = new List<RscFileResource>();
            VarResources = new List<RscFileResource>();

            using var br = new BinaryReader(File.OpenRead(filePath));
            var magic = new string(br.ReadChars(4));
            if (magic != "LBRC")
            {
                throw new InvalidDataException($"Invalid RSC file magic: {magic}");
            }
            br.ReadUInt32();
            br.ReadUInt32();
            br.ReadUInt32();

            var numResourceTypes = br.ReadUInt16();
            br.BaseStream.Seek(0x20, SeekOrigin.Begin);
            for (int i = 0; i < numResourceTypes; i++)
            {
                var tag = new string(br.ReadChars(4));
                var tableOffset = br.ReadUInt32();
                tableOffset += 0x20;

                var oldPosition = br.BaseStream.Position;
                br.BaseStream.Seek(tableOffset, SeekOrigin.Begin);

                while (true)
                {
                    uint offset, size;
                    ushort id, flags;
                    offset = br.ReadUInt32();
                    if (offset == 0)
                        break;
                    size = br.ReadUInt32();
                    br.ReadUInt32();
                    id = br.ReadUInt16();
                    flags = br.ReadUInt16();
                    br.ReadUInt32();
                    RscFileResource resource = new RscFileResource
                    {
                        Offset = offset,
                        Size = size,
                        Id = id,
                        Flags = flags
                    };
                    switch (tag)
                    {
                        case "BMAP":
                            BmpResources.Add(resource);
                            break;
                        case "SCRP":
                            ScriptResources.Add(resource);
                            break;
                        case "ANIM":
                            AnimResources.Add(resource);
                            break;
                        case "CTBL":
                            PaletteResources.Add(resource);
                            break;
                        case "WAVE":
                            WavResources.Add(resource);
                            break;
                        case "BUTN":
                            BtnResources.Add(resource);
                            break;
                        case "EVNT":
                            EventResources.Add(resource);
                            break;
                        case "VARI":
                            VarResources.Add(resource);
                            break;
                    }
                }

                br.BaseStream.Seek(oldPosition, SeekOrigin.Begin);
            }
        }

    }

    public class RscFileResource
    {
        public uint Offset { get; set; }
        public uint Size { get; set; }
        public ushort Id { get; set; }
        public ushort Flags { get; set; }
    }
}

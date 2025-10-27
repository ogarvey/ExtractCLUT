using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.SCUMM
{
    public class ScummV4
    {
        public List<RoomResourceV4> Rooms { get; set; } = new List<RoomResourceV4>();
        public List<RoomResourceV4> Costumes { get; set; } = new List<RoomResourceV4>();
        public List<RoomResourceV4> Scripts { get; set; } = new List<RoomResourceV4>();
        public List<RoomResourceV4> Sounds { get; set; } = new List<RoomResourceV4>();

        private BinaryReader _reader;

        public ScummV4(string filePath)
        {
            _reader = new BinaryReader(File.OpenRead(filePath));
        }

        public void ReadIndexFile()
        {
            ushort blockType = 0;
            uint itemSize = 0;


        }

        public int ReadResourceTypeList(ResType type)
        {
            int count = _reader.ReadInt16();

            for (int i = 0; i < count; i++)
            {
                switch (type)
                {
                    case ResType.rtRoom:
                        Rooms.Add(new RoomResourceV4
                        {
                            RoomId = _reader.ReadInt32(),
                            Offset = _reader.ReadUInt32()
                        });
                        break;
                    case ResType.rtScript:
                        Scripts.Add(new RoomResourceV4
                        {
                            RoomId = _reader.ReadInt32(),
                            Offset = _reader.ReadUInt32()
                        });
                        break;
                    case ResType.rtCostume:
                        Costumes.Add(new RoomResourceV4
                        {
                            RoomId = _reader.ReadInt32(),
                            Offset = _reader.ReadUInt32()
                        });
                        break;
                    case ResType.rtSound:
                        Sounds.Add(new RoomResourceV4
                        {
                            RoomId = _reader.ReadInt32(),
                            Offset = _reader.ReadUInt32()
                        });
                        break;
                }
            }

            return count;
        }
    }

    public class RoomResourceV4
    {
        public int RoomId { get; set; }
        public uint Offset { get; set; }
    }

    public enum ResType
    {
        rtInvalid = 0,
        rtFirst = 1,
        rtRoom = 1,
        rtScript = 2,
        rtCostume = 3,
        rtSound = 4,
        rtInventory = 5,
        rtCharset = 6,
        rtString = 7,
        rtVerb = 8,
        rtActorName = 9,
        rtBuffer = 10,
        rtScaleTable = 11,
        rtTemp = 12,
        rtFlObject = 13,
        rtMatrix = 14,
        rtBox = 15,
        rtObjectName = 16,
        rtRoomScripts = 17,
        rtRoomImage = 18,
        rtImage = 19,
        rtTalkie = 20,
        rtSpoolBuffer = 21,
        rtLast = 21
    };
}

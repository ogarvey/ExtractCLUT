using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.Delphine
{
    public class PartFile
    {
        public string FilePath { get; set; }
        public List<SubFile> SubFiles { get; set; } = new();

        public PartFile(string filePath)
        {
            FilePath = filePath;
        }

        public bool ParseFile()
        {
            using var br = new BinaryReader(File.OpenRead(FilePath));
            var subFileCount = br.ReadBigEndianUInt16();
            var entryLength = br.ReadBigEndianUInt16();
            for (int i = 0; i < subFileCount; i++)
            {
                var nameBytes = br.ReadBytes(14);
                var name = System.Text.Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                var offset = br.ReadBigEndianUInt32();
                var compressedSize = br.ReadBigEndianUInt32();
                var size = br.ReadBigEndianUInt32();
                br.ReadBytes(4); // unk
                var sub = new SubFile
                {
                    Name = name,
                    Size = size,
                    CompressedSize = compressedSize,
                    Offset = offset
                };
                var currentPos = br.BaseStream.Position;
                br.BaseStream.Seek(offset, SeekOrigin.Begin);
                var compressedData = br.ReadBytes((int)compressedSize);
                try
                {
                    sub.Data = sub.Size == sub.CompressedSize ? compressedData : CineUnpack.unpack(compressedData, size);
                }
                catch (Exception)
                {
                    sub.Data = compressedData;
                    sub.Name += ".UNPACK-ERROR";
                }
                SubFiles.Add(sub);
                br.BaseStream.Seek(currentPos, SeekOrigin.Begin);
            }
            // Implement file parsing logic here
            return true;
        }
    }

    public class SubFile
    {
        public string Name { get; set; }
        public uint Size { get; set; }
        public uint CompressedSize { get; set; }
        public uint Offset { get; set; }

        public byte[]? Data { get; set; }
    }
}

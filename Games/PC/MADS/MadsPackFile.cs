
using System.Text;

namespace ExtractCLUT.Games.PC.MADS
{
    public class MadsPackFile
    {
        private const string Magic = "MADSPACK";
        private int _count { get; set; }
        private List<MadsPackEntry> _entries { get; set; }
        private int _dataOffset { get; set; }
        public int DataOffset => _dataOffset;

        public MadsPackFile(string filePath)
        {
            var reader = new BinaryReader(File.OpenRead(filePath));
            var magic = Encoding.ASCII.GetString(reader.ReadBytes(8));
            if (magic != Magic)
            {
                throw new Exception("Invalid MadsPack file");
            }
            reader.BaseStream.Seek(14, SeekOrigin.Begin);
            _count = reader.ReadInt16();
            _entries = new List<MadsPackEntry>();

            var headerEntrySize = 10;
            var headerData = reader.ReadBytes(0xA0);
            for (int i = 0; i < _count; i++)
            {
                var entry = new MadsPackEntry();
                entry.CompressionType = (CompressionType)headerData[i * headerEntrySize];
                entry.Priority = headerData[i * headerEntrySize + 1];
                entry.Size = BitConverter.ToUInt32(headerData, i * headerEntrySize + 2);
                entry.CompressedSize = BitConverter.ToUInt32(headerData, i * headerEntrySize + 6);
                var sourceData = reader.ReadBytes((int)entry.CompressedSize);
                switch (entry.CompressionType)
                {
                    case CompressionType.None:
                        entry.Data = sourceData;
                        break;
                    case CompressionType.Compressed:
                        var dReader = new BinaryReader(new MemoryStream(sourceData));
                        entry.Data = FabDecompressor.ReadFab(dReader, (int)entry.Size).ToArray();
                        break;
                }
                _entries.Add(entry);
            }
            _dataOffset = (int)reader.BaseStream.Position;
        }
    
        public byte[] GetEntryData(int index)
        {
            return _entries[index].Data ?? throw new Exception("Entry data is null");
        }

        public BinaryReader GetEntryDataReader(int index)
        {
            return new BinaryReader(new MemoryStream(GetEntryData(index)));
        }
    }

    internal class MadsPackEntry
    {
        public CompressionType CompressionType { get; set; }
        public byte Priority { get; set; }
        public uint Size { get; set; }
        public uint CompressedSize { get; set; }
        public byte[]? Data { get; set; }
    }

    internal enum CompressionType
    {
        None = 0,
        Compressed = 1,
    }
}

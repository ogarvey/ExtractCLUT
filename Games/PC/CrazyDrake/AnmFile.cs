using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.CrazyDrake
{
    public class AnmFile(string path)
    {
        public string Path { get; init; } = path;
        public string Filename => System.IO.Path.GetFileName(Path);
        public List<List<AnmFrame>> Frames { get; set; } = [];

        public List<List<AnmFrame>> ParseFrames()
        {
            if (!File.Exists(Path))
                return [];
            using var anReader = new BinaryReader(File.OpenRead(Path));
            // MAGIC - 41 4C 20 20
            var magic = Encoding.ASCII.GetString(anReader.ReadBytes(4));
            if (magic != "AL  ")
                throw new InvalidDataException($"Invalid ANM file magic: {magic}");

            var recordLength = anReader.ReadUInt32(); // so far always 0x15
            if (recordLength != 0x15)
                Console.WriteLine($"Warning: unexpected ANM record length: {recordLength}");
            var unknown1 = anReader.ReadUInt32(); // so far always 0x3e8
            if (unknown1 != 0x3e8)
                Console.WriteLine($"Warning: unexpected ANM unknown1 value: {unknown1}");
            // skip to animation count at 0x19
            anReader.BaseStream.Seek(0x19, SeekOrigin.Begin);
            var animCount = anReader.ReadUInt32();
            // should now be at first 'ANM ' header, at 0x1d
            for (int i = 0; i < animCount; i++)
            {
                var animMagic = Encoding.ASCII.GetString(anReader.ReadBytes(4));
                if (animMagic != "ANM ")
                    throw new InvalidDataException($"Invalid ANM animation magic: {animMagic}");
                anReader.ReadUInt32(); // skip header size, always 0x04 for the 4 bytes that make the count value
                var frameCount = anReader.ReadUInt32();
                var frames = new List<AnmFrame>();
                for (int f = 0; f < frameCount; f++)
                {
                    var magic2 = Encoding.ASCII.GetString(anReader.ReadBytes(4)); // 'ANMF'
                    if (magic2 != "ANMF")
                        throw new InvalidDataException($"Invalid ANM frame magic: {magic2}");
                    anReader.ReadUInt32(); // skip header size, always 0x0D for the 13 bytes that make the frame data
                    var x = anReader.ReadInt32();
                    var y = anReader.ReadInt32();
                    var frameIndex = anReader.ReadUInt32();
                    var unk = anReader.ReadByte();
                    frames.Add(new AnmFrame(x, y, frameIndex, unk));
                }
                Frames.Add(frames);
            }
            return Frames;
        }
    }

    public record AnmFrame(int X, int Y, uint FrameIndex, byte Unk);
}

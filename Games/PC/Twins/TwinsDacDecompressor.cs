using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.Twins
{
    public class TwinsDacFile
    {
        public List<DacSection1Entry> Section1Entries { get; set; } = new List<DacSection1Entry>();
        public List<DacFrame> Frames { get; set; } = new List<DacFrame>();

        /// <summary>
        /// Parses a decompressed DAC file stream.
        /// </summary>
        /// <param name="data">The decompressed byte array of the DAC file.</param>
        public void Parse(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream))
            {
                // --- Section 1: Configuration/Header Data ---
                // Reads a count of entries (max 4).
                ushort section1Count = reader.ReadUInt16();
                if (section1Count > 4)
                    throw new Exception($"Invalid DAC file: Section 1 count {section1Count} > 4");

                for (int i = 0; i < section1Count; i++)
                {
                    // Each entry starts with a byte count (max 20).
                    byte valueCount = reader.ReadByte();
                    if (valueCount > 20)
                        throw new Exception($"Invalid DAC file: Section 1 value count {valueCount} > 20");

                    var entry = new DacSection1Entry();
                    for (int j = 0; j < valueCount; j++)
                    {
                        // Followed by 'valueCount' shorts.
                        entry.Values.Add(reader.ReadUInt16());
                    }
                    Section1Entries.Add(entry);
                }

                // --- Section 2: Frame Data ---
                // Reads the number of frames in this file.
                ushort frameCount = reader.ReadUInt16();

                for (int i = 0; i < frameCount; i++)
                {
                    var frame = new DacFrame();

                    // Read 2 bytes (Unknown/Unused?)
                    frame.Unknown1 = reader.ReadUInt16();

                    // Read Frame Dimensions and Position
                    frame.X = reader.ReadUInt16();
                    frame.Y = reader.ReadUInt16();
                    frame.Width = reader.ReadUInt16();
                    frame.Height = reader.ReadUInt16();

                    // Read 1 byte (Unknown/Unused?)
                    frame.Unknown2 = reader.ReadByte();

                    // Read Data Size
                    ushort dataSize = reader.ReadUInt16();

                    // Read Frame Data (Pixel data, likely planar or compressed)
                    frame.Data = reader.ReadBytes(dataSize);

                    Frames.Add(frame);
                }
            }
        }
    }

    public class DacSection1Entry
    {
        public List<ushort> Values { get; set; } = new List<ushort>();
    }

    public class DacFrame
    {
        public ushort Unknown1 { get; set; }
        public ushort X { get; set; }
        public ushort Y { get; set; }
        public ushort Width { get; set; }
        public ushort Height { get; set; }
        public byte Unknown2 { get; set; }
        public byte[] Data { get; set; }
    }

}

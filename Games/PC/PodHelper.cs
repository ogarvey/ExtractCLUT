namespace ExtractCLUT.Games.PC
{
    public static class PodHelper
    {
        public static void ExtractPod(string podFile)
        {
            using var podReader = new BinaryReader(File.OpenRead(podFile));
            var podOutputDir = Path.Combine(Path.GetDirectoryName(podFile)!, "output", Path.GetFileNameWithoutExtension(podFile));

            podReader.BaseStream.Position = 0xC; // skip the first 16 bytes

            var offsetsAndLengths = new List<(uint offset, uint length)>();
            var names = new List<string>();
            while (podReader.BaseStream.Position < podReader.BaseStream.Length)
            {
                var offsetsCount = podReader.ReadUInt32();
                var offsetsStart = podReader.ReadUInt32();
                podReader.BaseStream.Position = offsetsStart;
                for (var i = 0; i < offsetsCount; i++)
                {
                    var offset = podReader.ReadUInt32();
                    var length = podReader.ReadUInt32();
                    podReader.ReadBytes(24); // skip the rest of the header
                    offsetsAndLengths.Add((offset, length));
                }
                break;
            }
            Console.WriteLine($"Current position: {podReader.BaseStream.Position:X8}");
            // We're now at the start of the list of names, varying lengths null terminated
            while (podReader.BaseStream.Position < podReader.BaseStream.Length)
            {
                var name = podReader.ReadNullTerminatedString();
                if (string.IsNullOrEmpty(name)) break;
                names.Add(name);
            }

            for (int i = 0; i < offsetsAndLengths.Count; i++)
            {
                var (offset, length) = offsetsAndLengths[i];
                var name = names[i];
                podReader.BaseStream.Position = offset;
                var data = podReader.ReadBytes((int)length);
                var outputFile = Path.Combine(podOutputDir, name);
                Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
                File.WriteAllBytes(outputFile, data);
            }
        }
    }
}

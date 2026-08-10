namespace ExtractCLUT.Games.PC.FamilyProductions
{
    public static class DatFileHelper
    {
        public static void ExtractDatFile(string datFilePath, string outputDirectory, string fileExtension = ".BIN")
        {
            var indexFile = Path.ChangeExtension(datFilePath, ".IND");

            using var indexReader = new BinaryReader(File.OpenRead(indexFile));
            using var datReader = new BinaryReader(File.OpenRead(datFilePath));

            var offsets = new List<int>();
            while (indexReader.BaseStream.Position < indexReader.BaseStream.Length)
            {
                offsets.Add(indexReader.ReadInt32());
            }

            for (int i = 0; i < offsets.Count; i++)
            {
                int offset = offsets[i];
                int nextOffset = (i + 1 < offsets.Count) ? offsets[i + 1] : (int)datReader.BaseStream.Length;

                int length = nextOffset - offset;
                datReader.BaseStream.Seek(offset, SeekOrigin.Begin);
                byte[] data = datReader.ReadBytes(length);

                string outputFilePath = Path.Combine(outputDirectory, $"file_{i:D4}{fileExtension}");
                File.WriteAllBytes(outputFilePath, data);
            }
        }
    }
}

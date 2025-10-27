using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC
{
    public static class ClifDanger
    {
        public static Dictionary<int, string> IdToFileNameMap = new()
        {
            { 0, "MainSprites.spr" },
            { 1, "MainSprites.siz" },
            { 2, "MainSprites.ofs" },
            { 3, "MainSprites.dim" },
            { 4, "MainSprites.pos" },
            { 5, "UISprites1.spr"},
            { 6, "UISprites1.siz"},
            { 7, "UISprites1.ofs"},
            { 8, "UISprites1.dim"},
            { 9, "UISprites1.pos"},
            { 10, "EnemySprites.spr"},
            { 11, "EnemySprites.siz"},
            { 12, "EnemySprites.ofs"},
            { 13, "EnemySprites.dim"},
            { 14, "EnemySprites.pos"},
            { 15, "1.lbm" },
            { 16, "2.lbm" },
            { 17, "3.lbm" },
            { 18, "4.lbm" },
            { 39, "lvlFile1.bin" },
            { 40, "lvlFile2.bin" },
            { 41, "lvlFile3.bin" },
            { 42, "lvlFile4.bin" },
            { 63, "lvlFile1a.bin" },
            { 64, "lvlFile2a.bin" },
            { 65, "lvlFile3a.bin" },
            { 66, "lvlFile4a.bin" },
            { 87, "5.lbm" },
            { 96, "6.lbm" },
            { 103, "7.lbm" },
            { 106, "8.lbm" },
        };
        
        public static void ExtractIdfFiles(string idfPath, string outputPath)
        {
            using var idfReader = new BinaryReader(File.OpenRead(idfPath));
            var tableOffset = idfReader.ReadInt32();
            idfReader.BaseStream.Seek(tableOffset, SeekOrigin.Begin);
            var fileEntries = new List<(uint Offset, uint Size)>();
            while (idfReader.BaseStream.Position < idfReader.BaseStream.Length)
            {
                var offset = idfReader.ReadUInt32();
                var size = idfReader.ReadUInt32();
                fileEntries.Add((offset, size));
            }

            foreach (var ((offset, size), index) in fileEntries.WithIndex())
            {
                var fileName = IdToFileNameMap.ContainsKey(index) ? IdToFileNameMap[index] : $"not_found";
                if (fileName == "not_found")
                {
                    Console.WriteLine($"Warning: No filename mapping for index {index}, skipping file.");
                    continue;
                }
                idfReader.BaseStream.Seek(offset, SeekOrigin.Begin);
                var fileData = idfReader.ReadBytes((int)size);
                var outputFilePath = Path.Combine(outputPath, fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);
                File.WriteAllBytes(outputFilePath, fileData);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC
{
    public static class Sanitarium
    {
        public static void ExtractResFiles(string sanResFolder)
        {
            var sanResFiles = Directory.GetFiles(sanResFolder, "RES*", SearchOption.AllDirectories);

            foreach (var resFile in sanResFiles)
            {
                var resData = File.ReadAllBytes(resFile);
                var resCount = BitConverter.ToUInt32(resData.Take(4).ToArray(), 0);
                var resOffsets = new List<uint>();
                for (int i = 0; i < resCount; i++)
                {
                    resOffsets.Add(BitConverter.ToUInt32(resData.Skip(4 + (i * 4)).Take(4).ToArray(), 0));
                }
                var outputFolder = Path.Combine(Path.GetDirectoryName(resFile), $"{resFile.Replace(".", "")}_output");
                Directory.CreateDirectory(outputFolder);

                for (int i = 0; i < resOffsets.Count; i++)
                {
                    var offset = resOffsets[i];
                    var nextOffset = (i == resOffsets.Count - 1) ? resData.Length - 6 : (int)resOffsets[i + 1];
                    var length = nextOffset - offset;
                    var data = resData.Skip((int)offset).Take((int)length).ToArray();
                    File.WriteAllBytes(Path.Combine(outputFolder, $"{i}.bin"), data);
                }
            }
        }

        static List<string> GetPaletteFiles(string folder)
        {
            var files = Directory.GetFiles(folder, "*.bin");
            var palFiles = new List<string>();
            foreach (var file in files)
            {
                var data = File.ReadAllBytes(file);
                if (data[5] == 0x20)
                {
                    palFiles.Add(file);
                }
            }
            return palFiles;
        }

        static List<string> GetWavFiles(string folder)
        {
            var files = Directory.GetFiles(folder, "*.bin");
            var wavFiles = new List<string>();
            foreach (var file in files)
            {
                var data = File.ReadAllBytes(file);
                if (Encoding.ASCII.GetString(data.Take(4).ToArray()) == "RIFF")
                {
                    wavFiles.Add(file);
                }
            }
            return wavFiles;
        }
    }

    public enum SanitariumFileFlags{
        Palette = 0x20
    }
}

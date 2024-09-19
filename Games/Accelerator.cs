using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games
{
    public class Accelerator
    {
        public static void ExtractData()
        {
            // var paletteData = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Accelerator\CMDS\cdi_appl02_1_1_0.bin").Skip(39154).Take(0x180).ToArray();
            // var palette = ConvertBytesToRGB(paletteData);

            // var spriteData = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Accelerator\RTF\out\game.rtf_1_0_0\0\1.bin");
            // spriteData = spriteData.Concat(File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Accelerator\RTF\out\game.rtf_1_0_0\0\2.bin")).ToArray();
            // spriteData = spriteData.Concat(File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Accelerator\RTF\out\game.rtf_1_0_0\0\3.bin")).ToArray();
            // spriteData = spriteData.Concat(File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Accelerator\RTF\out\game.rtf_1_0_0\0\4.bin")).ToArray();
            // spriteData = spriteData.Concat(File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Accelerator\RTF\out\game.rtf_1_0_0\0\5.bin")).ToArray();
            // spriteData = spriteData.Concat(File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Accelerator\RTF\out\game.rtf_1_0_0\0\6.bin")).ToArray();
            // spriteData = spriteData.Concat(File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Accelerator\RTF\out\game.rtf_1_0_0\0\7.bin")).ToArray();

            // var startIndex = 0;
            // var spriteIndex = 0;
            // var outputFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Accelerator\RTF\out\game.rtf_1_0_0\0\sprites";

            // Directory.CreateDirectory(outputFolder);

            // for (int i = 0; i < spriteData.Length; i++)
            // {
            // 		if (spriteData[i] == 0x4e && spriteData[i + 1] == 0x75)
            // 		{
            // 				var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, startIndex, 0x180);
            // 				var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
            // 				CropImage(image, 64, 64, 0, 1).Save($@"{outputFolder}\{spriteIndex}.png", ImageFormat.Png);
            // 				spriteIndex++;
            // 				startIndex = i + 2;
            // 		}
            // }
            var gameFile = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Accelerator\RTF\out\game.rtf_1_0_0.bin";
            var levelFile = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Accelerator\RTF\out\level1.rtf_1_0_0.bin";

            var gameData = new VisionFactoryFile(gameFile);
            var levelData = new VisionFactoryFile(levelFile);

            var outputDir = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\Accelerator\RTF\out";

            if (gameData.SubFiles.Count > 0)
            {
                var outputFolder = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(gameFile));
                Directory.CreateDirectory(outputFolder);
                foreach (var (blob, index) in gameData.SubFiles.WithIndex())
                {
                    File.WriteAllBytes(Path.Combine(outputFolder, $"{index}.bin"), blob);
                }
            }

            if (levelData.SubFiles.Count > 0)
            {
                var outputFolder = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(levelFile));
                Directory.CreateDirectory(outputFolder);
                foreach (var (blob, index) in levelData.SubFiles.WithIndex())
                {
                    File.WriteAllBytes(Path.Combine(outputFolder, $"{index}.bin"), blob);
                }
            }
        }
    }
}

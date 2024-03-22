using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using M68k.CPU;
using M68k.Memory;

namespace ExtractCLUT.Helpers
{
    public static class CompiledSpriteHelper
    {

        // var mainCdiFile = new CdiFile(mainDataFile);

        // var dataSectors = mainCdiFile.Sectors.Where(x => x.SubMode.IsData).OrderBy(x => x.Channel)
        //   .ThenBy(x => x.SectorIndex).Select(x => x.GetSectorData()).ToList();

        // var mainData = dataSectors.SelectMany(x => x).ToArray();
        // //File.WriteAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\BODYSLAM\Output\mainData.bin", mainData);

        // var rgbPalOffsets = FileHelpers.FindSequenceOffsets(mainData, [0x49, 0x44, 0x41, 0x54]);

        // var clutPalOffsets = FileHelpers.FindSequenceOffsets(mainData, [0xc3, 0x00, 0x00, 0x00]);
        // clutPalOffsets.AddRange(FileHelpers.FindSequenceOffsets(mainData, [0xc3, 0x00, 0x00, 0x01]));
        // clutPalOffsets.AddRange(FileHelpers.FindSequenceOffsets(mainData, [0xc3, 0x00, 0x00, 0x02]));
        // clutPalOffsets.AddRange(FileHelpers.FindSequenceOffsets(mainData, [0xc3, 0x00, 0x00, 0x03]));
        // var offsets = FileHelpers.FindSequenceOffsets(mainData, [0x43, 0x50, 0x4c, 0x30]);

        // var paletteData = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\ZELDA - The Wand of Gamelon\Output\Palettes\clut_2910208.bin");
        // var palette = ReadClutBankPalettes(paletteData,2);

        // //var files = Directory.GetFiles(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\BODYSLAM\Output\Clut4 Data\sprites", "*.bin");

        // foreach (var (offset, oIndex) in offsets.WithIndex()) {
        //   var chunkLength = BitConverter.ToUInt32(mainData.Skip((int)offset + 4).Take(4).Reverse().ToArray(), 0);
        //   chunkLength += BitConverter.ToUInt32(mainData.Skip((int)offset + 8).Take(4).Reverse().ToArray(), 0);

        //   var data = mainData.Skip((int)offset).Take((int)chunkLength).ToArray();

        //   var dataLength = BitConverter.ToInt32(data.Skip(0x4).Take(4).Reverse().ToArray(), 0);

        //   var dataStart = BitConverter.ToInt32(data.Skip(0x8).Take(4).Reverse().ToArray(), 0);

        //   var offsetsStart = 0x10;

        //   var spriteOffsets = new List<int>() { 0 };


        //   for (int i = 0; i < 0x5c; i += 4)
        //   {
        //     var spriteOffset = BitConverter.ToInt32(data.Skip(offsetsStart + i).Take(4).Reverse().ToArray(), 0);
        //     spriteOffsets.Add(spriteOffset);
        //   }
        //   var folder = @$"C:\Dev\Projects\Gaming\CD-i\ZELDA - The Wand of Gamelon\Output\sprites\{oIndex}";
        //   Directory.CreateDirectory(folder);

        //   data = data.Skip(dataStart).ToArray();
        //   foreach (var (sOffset, index) in spriteOffsets.WithIndex())
        //   {
        //     var bytesToTake = index == spriteOffsets.Count - 1 ? dataLength - sOffset : spriteOffsets[index + 1] - sOffset;
        //     var chunk = data.Skip(sOffset).Take(bytesToTake).ToArray();
        //     if (chunk.Length == 0 || chunk.Length < 8) continue;
        //     var output = CompiledSpriteHelper.DecodeCompiledSprite(chunk).Skip(chunk.Length + 0x20).ToArray();
        //     //File.WriteAllBytes($@"C:\Dev\Projects\Gaming\CD-i\ZELDA - The Wand of Gamelon\Output\{oIndex}_sprite_{index}.bin", output);
        //     var image = GenerateClutImage(palette, output, 416, 240, true);
        //     var outputName = Path.Combine(folder, $"sprite_{index}.png");
        //     image.Save(outputName, ImageFormat.Png);

        //   }
        // }

        public static byte[] DecodeCompiledSprite(byte[] compiledSpriteData, int startPC = 0x22)
        {
            var outputOffset = compiledSpriteData.Length + 32;
            using (var newMemory = new MemorySpace(65536))
            {
                var newCpu = new MC68000();
                newCpu.SetAddressSpace(newMemory);
                newCpu.Reset();
                newCpu.SetAddrRegisterLong(7, 65536);
                newCpu.SetAddrRegisterLong(0, outputOffset);
                newCpu.SetPC(startPC);
                for (var i = 0; i < compiledSpriteData.Length; i++)
                {
                    newMemory.WriteByte(i, compiledSpriteData[i]);
                }
                while (newCpu.GetPC() < compiledSpriteData.Length && newMemory.ReadByte(newCpu.GetPC()) != 0x4E && newMemory.ReadByte(newCpu.GetPC() + 1) != 0x75)
                {
                    try
                    {
                        Console.WriteLine($"Executing at PC: {newCpu.GetPC():X4}");
                        newCpu.Execute();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception encountered at PC: {newCpu.GetPC()} with message: {ex.Message}");
                        continue;
                    }
                }
                var bytes = new byte[65536];
                for (var i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = newMemory.ReadByte(i);
                }
                return bytes;
            }
        }
    }
}

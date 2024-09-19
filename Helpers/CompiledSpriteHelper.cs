using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        //     var image = ImageFormatHelper.GenerateClutImage(palette, output, 416, 240, true);
        //     var outputName = Path.Combine(folder, $"sprite_{index}.png");
        //     image.Save(outputName, ImageFormat.Png);

        //   }
        // }

        public static byte[] DecodeCompiledSprite(byte[] compiledSpriteData, int startPC = 0, int outputOffset = 0x180, bool logEnabled = false)
        {
            var log = new StringBuilder();
            using (var newMemory = new MemorySpace(8388607))
            {
                var newCpu = new MC68000();
                newCpu.SetAddressSpace(newMemory);
                newCpu.Reset();
                newCpu.SetAddrRegisterLong(7, 8388352);
                newCpu.SetAddrRegisterLong(0, outputOffset);
                newCpu.SetAddrRegisterLong(1, outputOffset);
                newCpu.SetAddrRegisterLong(2, outputOffset);
                newCpu.SetAddrRegisterLong(3, outputOffset);
                newCpu.SetAddrRegisterLong(4, outputOffset);
                newCpu.SetDataRegisterLong(0,0);
                newCpu.SetPC(0x20000+startPC);
                for (var i = 0x20000; i < 0x20000 + compiledSpriteData.Length; i++)
                {
                    newMemory.WriteByte(i, compiledSpriteData[i- 0x20000]);
                }
                var errorCount = 0;
                var cycleCount = 0;
                while (newCpu.GetPC() < compiledSpriteData.Length + 0x20000 && newMemory.ReadByte(newCpu.GetPC()) != 0x4E && newMemory.ReadByte(newCpu.GetPC() + 1) != 0x75)
                {
                    if (newMemory.ReadByte(newCpu.GetPC()) == 0x41 && newMemory.ReadByte(newCpu.GetPC() + 1) == 0x57)
                    {
                        newCpu.SetPC(newCpu.GetPC() + 2);
                        continue;
                    }
                    try
                    {
                        if (logEnabled) {
                            log.AppendLine($"Executing at PC: {newCpu.GetPC():X4}");
                            log.AppendLine($"Instruction: '{newCpu.GetInstructionAt(newCpu.GetPC())}'\t a[0] value = {newCpu.GetAddrRegisterLong(0):X8}");
                            log.AppendLine($"a[1] value = {newCpu.GetAddrRegisterLong(1):X8}\t a[2] value = {newCpu.GetAddrRegisterLong(2):X8} \t a[3] value = {newCpu.GetAddrRegisterLong(3):X8}");
                            log.AppendLine($"a[4] value = {newCpu.GetAddrRegisterLong(4):X8}\t a[5] value = {newCpu.GetAddrRegisterLong(5):X8} \t a[6] value = {newCpu.GetAddrRegisterLong(6):X8}");
                            log.AppendLine($"a[7] value = {newCpu.GetAddrRegisterLong(7):X8}");
                            log.AppendLine($"d[0] value = {newCpu.GetDataRegisterLong(0):X8}\t d[1] value = {newCpu.GetDataRegisterLong(1):X8} \t d[2] value = {newCpu.GetDataRegisterLong(2):X8}");
                            log.AppendLine($"d[3] value = {newCpu.GetDataRegisterLong(3):X8}\t d[4] value = {newCpu.GetDataRegisterLong(4):X8} \t d[5] value = {newCpu.GetDataRegisterLong(5):X8}");
                            log.AppendLine($"d[6] value = {newCpu.GetDataRegisterLong(6):X8}\t d[7] value = {newCpu.GetDataRegisterLong(7):X8}");
                        }
                        newCpu.Execute();
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        if (errorCount > 10)
                        {
                            Console.WriteLine($"Too many errors encountered at PC: {newCpu.GetPC()} with message: {ex.Message}, breaking loop");
                            var eBytes = new byte[131072];
                            for (var i = 0; i < eBytes.Length; i++)
                            {
                                eBytes[i] = newMemory.ReadByte(i);
                            }
                            return eBytes;
                        }
                        continue;
                    }
                }
                var bytes = new byte[131072];
                for (var i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = newMemory.ReadByte(i);
                }
                File.WriteAllText("log.txt", log.ToString());
                return bytes;
            }
        }

        public static List<byte[]> DecodeCompiledSprites(byte[] compiledSpriteData, int startPC = 0, int outputOffset = 0x180)
        {
            var byteArrays = new List<byte[]>();
            using (var newMemory = new MemorySpace(8388607))
            {
                var newCpu = new MC68000();
                newCpu.SetAddressSpace(newMemory);
                newCpu.Reset();
                newCpu.SetAddrRegisterLong(7, 8388607);
                newCpu.SetAddrRegisterLong(0, outputOffset);
                newCpu.SetAddrRegisterLong(1, outputOffset);
                newCpu.SetAddrRegisterLong(2, outputOffset);
                newCpu.SetAddrRegisterLong(3, outputOffset);
                newCpu.SetAddrRegisterLong(4, outputOffset);
                newCpu.SetDataRegisterLong(0, 0x180);
                newCpu.SetPC(0x10000 + startPC);
                for (var i = 0x10000; i < 0x10000 + compiledSpriteData.Length; i++)
                {
                    newMemory.WriteByte(i, compiledSpriteData[i - 0x10000]);
                }
                var errorCount = 0;
                var cycleCount = 0;
                while (newCpu.GetPC() < compiledSpriteData.Length + 0x10000)
                {
                    if ((newCpu.GetPC() + 1 >= compiledSpriteData.Length + 0x10000) || newMemory.ReadByte(newCpu.GetPC()) == 0x4E && newMemory.ReadByte(newCpu.GetPC() + 1) == 0x75)
                    {
                        var bytes = new byte[107520];
                        for (var i = 0; i < bytes.Length; i++)
                        {
                            bytes[i] = newMemory.ReadByte(i);
                        }
                        byteArrays.Add(bytes);
                        newCpu.SetPC(newCpu.GetPC() + 2);
                        newCpu.SetAddrRegisterLong(0, outputOffset);
                        newCpu.SetAddrRegisterLong(1, outputOffset);
                        newCpu.SetAddrRegisterLong(2, outputOffset);
                        newCpu.SetAddrRegisterLong(3, outputOffset);
                        newCpu.SetAddrRegisterLong(4, outputOffset);
                        newCpu.SetDataRegisterLong(0, 0x180);
                    }
                    if(newMemory.ReadByte(newCpu.GetPC()) == 0x41 && newMemory.ReadByte(newCpu.GetPC() + 1) == 0x57)
                    {
                        newCpu.SetPC(newCpu.GetPC() + 2);
                        continue;
                    }
                    try
                    {
                        //Console.WriteLine($"Executing at PC: {newCpu.GetPC():X4}");
                        //cycleCount++;
                        newCpu.Execute();
                        //Console.WriteLine($"PC:{newCpu.GetPC():X4}\t Instruction: '{newCpu.GetInstructionAt(newCpu.GetPC())}'\t a[1] value = {newCpu.GetAddrRegisterLong(1):X8}");

                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        if (errorCount > 10)
                        {
                            Console.WriteLine($"Too many errors encountered at PC: {newCpu.GetPC()} with message: {ex.Message}, breaking loop");
                            var bytes = new byte[107520];
                            for (var i = 0; i < bytes.Length; i++)
                            {
                                bytes[i] = newMemory.ReadByte(i);
                            }
                            byteArrays.Add(bytes);
                            newCpu.SetPC(newCpu.GetPC() + 1);
                            newCpu.SetAddrRegisterLong(0, outputOffset);
                            newCpu.SetAddrRegisterLong(1, outputOffset);
                            newCpu.SetAddrRegisterLong(2, outputOffset);
                            newCpu.SetAddrRegisterLong(3, outputOffset);
                            newCpu.SetAddrRegisterLong(4, outputOffset);
                            newCpu.SetDataRegisterLong(0, 0x184);
                        }
                        continue;
                    }
                }
                
                return byteArrays;
            }
        }
    }
}

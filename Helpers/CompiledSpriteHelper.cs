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
        public static byte[] DecodeCompiledSprite(byte[] compiledSpriteData)
        {
            var outputOffset = compiledSpriteData.Length + 32;
            using (var newMemory = new MemorySpace(65536))
            {
                var newCpu = new MC68000();
                newCpu.SetAddressSpace(newMemory);
                newCpu.Reset();
                newCpu.SetDataRegisterByte(2, 16); // pyramid adventures
                newCpu.SetDataRegisterByte(3, 3); // pyramid adventures
                newCpu.SetDataRegisterByte(5, 1); // pyramid adventures
                newCpu.SetAddrRegisterLong(3, 72); // pyramid adventures
                newCpu.SetAddrRegisterLong(7, 65536);
                newCpu.SetAddrRegisterLong(0, outputOffset);
                newCpu.SetPC(16);
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

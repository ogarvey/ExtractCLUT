using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.MADS.Palette
{
    public class RGB6
    {
        byte R;
        byte G;
        byte B;
        byte PalIndex;
        byte U2;
        byte Flags;

        public RGB6(byte[] data)
        {
            R = VgaTranslate(data[0]);
            G = VgaTranslate(data[1]);
            B = VgaTranslate(data[2]);
            PalIndex = data[3];
            U2 = data[4];
            Flags = data[5];
        }

        private byte VgaTranslate(byte value)
        {
            return (byte)(value * 255 / 63);
        }
    }
}

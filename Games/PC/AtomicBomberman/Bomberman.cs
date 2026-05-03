using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.AtomicBomberman
{
    public sealed class RmpData
    {
        public byte[] Map = new byte[256]; // bytes 0x000..0x0FF
        public byte R;                     // byte 0x100
        public byte G;                     // byte 0x101
        public byte B;                     // byte 0x102
    }

    public static class Bomberman
    {
        public static RmpData LoadRmp(string path)
        {
            var data = File.ReadAllBytes(path);
            if (data.Length < 259) throw new InvalidDataException("RMP must be at least 259 bytes.");

            var r = new RmpData();

            var map = new Span<byte>(data, 0, 256);

            for (int i = 0; i < 256; i++)
            {
                if (map[i] == 0)
                    map[i] = (byte)i;
            }
            r.Map = map.ToArray();
            r.R = data[256];
            r.G = data[257];
            r.B = data[258];
            return r;
        }

        // lut15To8: 32768 bytes from DAT_00495390-equivalent table
        // decoded16: output of DecodeMode11Rle16 (one ushort per pixel)
        // transparentKeyEnabled/transparentKey optional if your FRAM flags require it.
        public static byte[] BuildFinalIndexed(
            byte[] decoded16,
            byte[] lut15To8,
            RmpData? rmp,
            bool transparentKeyEnabled = false,
            ushort transparentKey = 0)
        {
            if (lut15To8.Length < 32768) throw new ArgumentException("LUT must be 32768 bytes.");

            var dst = new byte[decoded16.Length / 2];

            for (int i = 0; i < decoded16.Length / 2; i++)
            {
                ushort px16 = BitConverter.ToUInt16(decoded16, i * 2);

                if (transparentKeyEnabled && px16 == transparentKey)
                {
                    dst[i] = 0;
                    continue;
                }

                byte idx = lut15To8[px16 & 0x7FFF]; // 15-bit domain
                if (rmp != null && idx != 0)
                    idx = rmp.Map[idx];

                dst[i] = idx;
            }

            return dst;
        }

        // paletteRgb: 256*3 bytes (R,G,B) or adapt channel order if your source is B,G,R.
        public static byte[] IndexedToRgba(byte[] indexed, byte[] paletteRgb)
        {
            var rgba = new byte[indexed.Length * 4];
            for (int i = 0; i < indexed.Length; i++)
            {
                int p = indexed[i] * 3;
                int o = i * 4;
                rgba[o + 0] = paletteRgb[p + 0];
                rgba[o + 1] = paletteRgb[p + 1];
                rgba[o + 2] = paletteRgb[p + 2];
                rgba[o + 3] = indexed[i] == 0 ? (byte)0 : (byte)255; // optional transparency convention
            }
            return rgba;
        }
    }
}

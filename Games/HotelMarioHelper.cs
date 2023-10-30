using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using ExtractCLUT.Writers;
using Color = System.Drawing.Color;

namespace ExtractCLUT.Games
{
  public static class HotelMarioHelper
  {
    static string _baseDir = @"C:\Dev\Projects\Gaming\CD-i\Hotel Mario\records";
    static string _introDir = $@"{_baseDir}\intro\video";
    static string _introOutputDir = $@"{_baseDir}\intro\video\output";
    static int[] _introRL7_2_Offsets = new int[] { 0x0, 0x5ac8, 0x6cf0, 0xbea4, 0xf51c, 0x158f8 };
    static int[] _introRL7_2_OffsetLengths = new int[] { 0x51df, 0x9c1, 0x495c, 0x2ec2, 0x51ca, 0x2888 };
    static int[] _introPaletteOffsets = new int[] { 0x2df18, 0x34428, 0x35fb8, 0x3bb98, 0x3fbe8, 0x47358 };
    static int[] _introRL7_3_Offsets = new int[] { 
			0x0, 0x914, 0x1228, 0x2450, 0x3678, 0x48a0, 0x5ac8, 0x6cf0,0x7f18, 0x9140,0xa368,0xb590,0xc7b8,0xd9e0,
			0xec08,0xfe30,0x11058,0x1196c,0x12280,0x12b94,0x134a8,0x13dbc,0x146d0,0x14fe4,0x1620c
			};
    static int[] _introRL7_3_OffsetLengths = new int[] { 
			0x63a, 0x8f5, 0xb66, 0xc17, 0xce8, 0xc8e, 0xd41, 0xd08 ,0xd43,0xcb6,0xc27,0xa9f,0xb52,0x9f3,
			0x9c6,0x980,0x89c,0x766,0x75d,0x6c0,0x6db,0x6b3,0x5dc,0xe8e,0x11e8
			};
    static int[] _introPalette2Offsets = new int[] { 0x5a288 };
    public static void ExtractIntroFiles()
    {
      var introPalette = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Hotel Mario\intro.rtf");

      var file = Path.Combine(_introDir, "intro_v_1_15_RL7_Normal_2.bin");
      var image = File.ReadAllBytes(file);
      for (int i = 0; i < _introRL7_2_Offsets.Length; i++)
      {
        var palette1 = ColorHelper.ConvertBytesToRGB(introPalette.Skip(_introPaletteOffsets[i]).Take(0x180).ToArray()).ToList();
        var offset = _introRL7_2_Offsets[i];
        var length = _introRL7_2_OffsetLengths[i];
        var imageBytes = image.Skip(offset).Take(length).ToArray();
        var img = ImageFormatHelper.GenerateRle7Image(palette1, imageBytes, 384, 280);
        img.Save(Path.Combine(_introOutputDir, $"intro_RL7_2_{i}.png"), ImageFormat.Png);
      }

      file = Path.Combine(_introDir, "intro_v_1_15_RL7_Normal_3.bin");
      var palette = ColorHelper.ConvertBytesToRGB(introPalette.Skip(_introPalette2Offsets[0]).Take(0x180).ToArray()).ToList();
			var anim = new List<Bitmap>();
      palette[0] = Color.Transparent;
			var chunks = FileHelpers.SplitBinaryFileIntoChunks(file, new byte[]{0x00,0x00,0x00},true,true,null);
			foreach (var (chunk, index) in chunks.WithIndex())
			{
        var lChunk = chunk.ToList();
				while (lChunk.Count > 0 && lChunk.First() == 0x00)
        {
          lChunk.RemoveAt(0);
        }
        if(lChunk.Count == 0)
				{
					continue;
				}
        var img = ImageFormatHelper.GenerateRle7Image(palette, lChunk.ToArray(), 384, 280);
        //img.Save(Path.Combine(_introOutputDir, $"intro_RL7_3_{index}.png"), ImageFormat.Png);
        anim.Add(img);
			}
			using(var gifWriter = new GifWriter(Path.Combine(_introOutputDir, $"intro_RL7_3.gif"), 100))
			{
				foreach(var img in anim)
				{
					gifWriter.WriteFrame(img);
				}
			}
    }
  }
}

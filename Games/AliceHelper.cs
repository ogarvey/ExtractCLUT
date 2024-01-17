using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static OGLibCDi.Helpers.ColorHelper;
using static OGLibCDi.Helpers.ImageFormatHelper;

namespace ExtractCLUT.Games
{
    public static class AliceHelper
    {
        public static void ExtractInventory()
        {
            var llInventory = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\ALICE IN WONDERLAND\Output\Alice_Rooms\alice_rooms.rtf_1_7_66.bin");

            var palBytes = llInventory.Take(0x80).ToArray();
            var palette = ReadPalette(palBytes);

            var imageBytes = llInventory.Skip(0x80).Take(0x4080).ToArray();
            var tileList = new List<byte[]>();

            for (int i = 0; i < 0x8000; i += 64)
            {
                tileList.Add(imageBytes.Skip(i).Take(64).ToArray());
            }

            for (int i = 0; i < tileList.Count; i += 64)
            {
                var listOfQuads = tileList.Skip(i).Take(64).ToList();
                // image is made up of 4 tiles, each tile is 8x8 pixels
                for (int j = 0; j < 32; j += 2)
                {
                    var quadTopLeft = listOfQuads[j];
                    var quadTopRight = listOfQuads[j + 1];
                    var quadBottomLeft = listOfQuads[j + 32];
                    var quadBottomRight = listOfQuads[j + 33];
                    var combinedQuads = new byte[256];
                    for (int x = 0; x < 16; x++)
                    {
                        for (int y = 0; y < 16; y++)
                        {
                            if (x < 8 && y < 8)
                            {
                                combinedQuads[x + y * 16] = quadTopLeft[x + y * 8];
                            }
                            else if (x >= 8 && y < 8)
                            {
                                combinedQuads[x + y * 16] = quadTopRight[x - 8 + y * 8];
                            }
                            else if (x < 8 && y >= 8)
                            {
                                combinedQuads[x + y * 16] = quadBottomLeft[x + (y - 8) * 8];
                            }
                            else if (x >= 8 && y >= 8)
                            {
                                combinedQuads[x + y * 16] = quadBottomRight[x - 8 + (y - 8) * 8];
                            }
                        }
                    }
                    var image = GenerateClutImage(palette, combinedQuads, 16, 16);
                    image.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\ALICE IN WONDERLAND\Output\Alice_Rooms\combined\{i}_{j}.png");
                }
            }
        }
    }
}

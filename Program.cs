using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using ExtractCLUT.Games.PSX.Alundra;
using ExtractCLUT.Games.PC.EoL;
using ExtractCLUT.Helpers;
using ExtractCLUT.Models.AniMagic;
using SixLabors.ImageSharp;

var alundraDataFile = @"C:\Dev\Gaming\Sony\PSX\Games\Alundra\DATA\DATAS.BIN";
var alundraOutDir = @"C:\Dev\Gaming\Sony\PSX\Games\Alundra\Extracted";
AlundraHelper.ExtractDatasBin(alundraDataFile, alundraOutDir, renderMapSamples: 12);
Console.WriteLine("Alundra DATAS.BIN extracted");

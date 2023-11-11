using Color = System.Drawing.Color;
using ExtractCLUT.Games;
using ExtractCLUT.Helpers;
using ExtractCLUT.Model;
using ExtractCLUT.Writers;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Drawing;
using static ExtractCLUT.Helpers.AudioHelper;
using static ExtractCLUT.Helpers.ImageFormatHelper;
using static ExtractCLUT.Utils;
using static LotRSHelpers;

var actualPath = @"C:\Dev\Projects\Gaming\CD-i\PhantomExpress";

FileHelpers.ExtractAll(actualPath, "rtb");

// var sectorInfo = new SectorInfo("music.rtr", new byte[2352])
// {
//   SectorIndex = 0,
//   OriginalOffset = 0,
//   FileNumber = 1,
//   Channel = 1,
//   SubMode = 64,
//   CodingInformation = 5
// };

// Console.WriteLine($"SectorInfo: {sectorInfo}");


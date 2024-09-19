using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using ExtractCLUT.Helpers;
using OGLibCDi.Models;
using static ExtractCLUT.Helpers.ColorHelper;
using static ExtractCLUT.Helpers.ImageFormatHelper;

namespace ExtractCLUT.Games
{
	// Level Files
	// Palettes in First Blob @ 0x78
	// StatusBar in Second Blob - 320 * 21 pixels
	// Sprites in Third Blob, or 4th for level 3/4 - Unsure which atm (Possibly Background Sprites)
	public static class TheApprentice
	{
		public static void ExtractBinaryData()
		{
			var cdiFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release";

			var datFiles = Directory.GetFiles(cdiFolder, "*.dat");

			var mainFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis";

			foreach (var dat in datFiles)
			{
				if (!dat.Contains("levelb"))
				{
					continue;
				}
				var cdiFile = new CdiFile(dat);
				var data = cdiFile.DataSectors.SelectMany(s => s.GetSectorData()).ToArray();
				var outputName = Path.Combine(mainFolder, $"{Path.GetFileNameWithoutExtension(dat)}.bin");
				File.WriteAllBytes(outputName, data);
			}

			var binFiles = Directory.GetFiles(mainFolder, "*.bin");

			var apprenticeFiles = new List<VisionFactoryFile>();

			foreach (var file in binFiles)
			{
				if (!file.Contains("levelb"))
				{
					continue;
				}
				var aFile = new VisionFactoryFile(file);
				apprenticeFiles.Add(aFile);
				if (aFile.SubFiles.Count == 0)
				{
					continue;
				}
				var outputFolder = Path.Combine(mainFolder, Path.GetFileNameWithoutExtension(file));
				Directory.CreateDirectory(outputFolder);
				foreach (var (blob, index) in aFile.SubFiles.WithIndex())
				{
					File.WriteAllBytes(Path.Combine(outputFolder, $"{index}.bin"), blob);
				}

				if (Path.GetFileNameWithoutExtension(file).Contains("level"))
				{
					var paletteData = aFile.SubFiles[0].Skip(0x78).Take(0x300).ToArray();
					var palFolder = Path.Combine(outputFolder, "Palettes");
					Directory.CreateDirectory(palFolder);
					var palettes = new List<List<Color>>();
					for (int i = 0; i < paletteData.Length; i += 0x180)
					{
						var bytes = paletteData.Skip(i).Take(0x180).ToArray();
						File.WriteAllBytes(Path.Combine(palFolder, $"{i}.bin"), bytes);
						var palette = ConvertBytesToRGB(bytes);
						CreateLabelledPalette(palette).Save(Path.Combine(palFolder, $"{i}.png"), ImageFormat.Png);
						palettes.Add(palette);
					}

					var statusBar = aFile.SubFiles[1].Take(0x1a40).ToArray();
					var statusBarImage = ImageFormatHelper.GenerateClutImage(palettes[0], statusBar, 320, 21, true);
					statusBarImage.Save(Path.Combine(outputFolder, "StatusBar.png"), ImageFormat.Png);

					var spriteBlobs = new List<byte[]>();
					var spriteOffsets = new List<int>();
					var sIndex = file.Contains("level3") || file.Contains("level4") ? 3 : 2;
					for (int i = 2; i < aFile.SubFiles[sIndex].Length; i += 4)
					{
						if (aFile.SubFiles[sIndex][i] == 0xFF && aFile.SubFiles[sIndex][i + 1] == 0xFF)
						{
							break;
						}
						var offset = BitConverter.ToInt32(aFile.SubFiles[sIndex].Skip(i).Take(4).Reverse().ToArray(), 0) + 2;
						spriteOffsets.Add(offset);
					}

					var blobOutputFolder = Path.Combine(outputFolder, "Blobs");
					//Directory.CreateDirectory(blobOutputFolder);

					for (int i = 0; i < spriteOffsets.Count; i++)
					{
						var bytesToTake = i == spriteOffsets.Count - 1 ? aFile.SubFiles[sIndex].Length - spriteOffsets[i] : spriteOffsets[i + 1] - spriteOffsets[i];
						var blob = aFile.SubFiles[sIndex].Skip(spriteOffsets[i]).Take(bytesToTake).ToArray();
						spriteBlobs.Add(blob);
						//File.WriteAllBytes(Path.Combine(blobOutputFolder, $"{i}.bin"), blob);
					}

					var tileFolder = Path.Combine(outputFolder, "FGTiles");
					Directory.CreateDirectory(tileFolder);
					var images = new List<Image>();
					foreach (var (blob, index) in spriteBlobs.WithIndex())
					{
						var decodedBlob = CompiledSpriteHelper.DecodeCompiledSprite(blob, 0, 0x180);
						var image = ImageFormatHelper.GenerateClutImage(palettes[1], decodedBlob, 384, 240, true);
						image = (Bitmap)CropImage(image, 20, 5, 0, 1);
						images.Add(image);
						var outputName = Path.Combine(tileFolder, $"{index / 4}.png");
						if (OperatingSystem.IsWindowsVersionAtLeast(6, 1) && images.Count == 4)
						{
							var finalImage = CombineImages(images, 20, 5, 20);
							finalImage.Save(outputName, ImageFormat.Png);
							images.Clear();
							//image.Save(outputName, ImageFormat.Png);
						}
					}
				}
			}

		}
		public static void ExtractLevel1Data(int level = 1)
		{

			var palFile = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\Palettes\0.bin";
			var palette = ConvertBytesToRGB(File.ReadAllBytes(palFile).Take(0x180).ToArray());
			var palFile2 = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\Palettes\384.bin";
			var palette2 = ConvertBytesToRGB(File.ReadAllBytes(palFile2).Take(0x180).ToArray());


			var data = File.ReadAllBytes(@$"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\2.bin");

			var spriteData = data.Skip(0x16F1A).Take(1514).ToArray();
			var startIndex = 0;
			var spriteIndex = 0;
			var outputFolder = @$"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\sprites";
			Directory.CreateDirectory(outputFolder);

			spriteData = spriteData.Concat(data.Skip(0x178c4).Take(20954).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x1CA9E).Take(29510).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x23DE4).Take(11960).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x26C9C).Take(22274).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x2C39E).Take(0xe8e0).ToArray()).ToArray();

			for (int i = 0; i < spriteData.Length; i++)
			{
				if (spriteData[i] == 0x4e && spriteData[i + 1] == 0x75)
				{
					var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, startIndex, 0x180);
					var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
					CropImage(image, 128, 128, 0, 1).Save($@"{outputFolder}\{spriteIndex}.png", ImageFormat.Png);
					spriteIndex++;
					startIndex = i + 2;
				}
			}

			data = File.ReadAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\3.bin");

			var bgTileData = data.Skip(0x14662).Take(25600).ToArray();
			bgTileData = bgTileData.Concat(data.Skip(0x1AF12).Take(4800).ToArray()).ToArray();
			bgTileData = bgTileData.Concat(data.Skip(0x1C812).Take(4800).ToArray()).ToArray();
			bgTileData = bgTileData.Concat(data.Skip(0x1E112).Take(3200).ToArray()).ToArray();
			var bgFolder = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\BGTiles";
			Directory.CreateDirectory(bgFolder);

			for (int i = 0; i < bgTileData.Length; i += 1600)
			{
				var tile = bgTileData.Skip(i).Take(1600).ToArray();
				var image = ImageFormatHelper.GenerateClutImage(palette2, tile, 80, 20);
				image.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\BGTiles\{i / 1600}.png", ImageFormat.Png);
			}

			var sideTileData = data.Skip(0x1AA62).Take(0x4b0).ToArray();
			sideTileData = sideTileData.Concat(data.Skip(0x1C1D2).Take(0x640).ToArray()).ToArray();
			sideTileData = sideTileData.Concat(data.Skip(0x1DAD2).Take(0x640).ToArray()).ToArray();
			var sideFolder = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\SideTiles";
			Directory.CreateDirectory(sideFolder);

			for (int i = 0; i < sideTileData.Length; i += 400)
			{
				var tile = sideTileData.Skip(i).Take(400).ToArray();
				var image = ImageFormatHelper.GenerateClutImage(palette2, tile, 20, 20);
				image.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\SideTiles\{i / 80}.png", ImageFormat.Png);
			}

			startIndex = 0;
			spriteData = data.Skip(2068).Take(81486).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x1ED92).Take(27684).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x259B6).Take(22742).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x2B28C).Take(0x83de).ToArray()).ToArray();

			for (int i = 0; i < spriteData.Length; i++)
			{
				if (spriteData[i] == 0x4e && spriteData[i + 1] == 0x75)
				{
					var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, startIndex, 0x180);
					var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
					CropImage(image, 192, 128, 0, 1).Save($@"{outputFolder}\{spriteIndex}.png", ImageFormat.Png);
					spriteIndex++;
					startIndex = i + 2;
				}
			}
		}
		public static void ExtractLevel2Data(int level)
		{

			var palFile = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\Palettes\0.bin";
			var palette = ConvertBytesToRGB(File.ReadAllBytes(palFile).Take(0x180).ToArray());
			var palFile2 = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\Palettes\384.bin";
			var palette2 = ConvertBytesToRGB(File.ReadAllBytes(palFile2).Take(0x180).ToArray());


			var data = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\2.bin");

			var spriteData = data.Skip(117544).Take(1514).ToArray();
			var startIndex = 0;
			var spriteIndex = 0;
			var outputFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\sprites";
			Directory.CreateDirectory(outputFolder);

			spriteData = spriteData.Concat(data.Skip(0x1D4D2).Take(23224).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x22F8A).Take(20188).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x27E66).Take(22964).ToArray()).ToArray();

			for (int i = 0; i < spriteData.Length; i++)
			{
				if (spriteData[i] == 0x4e && spriteData[i + 1] == 0x75)
				{
					var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, startIndex, 0x180);
					var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
					CropImage(image, 128, 128, 0, 1).Save($@"{outputFolder}\{spriteIndex}.png", ImageFormat.Png);
					spriteIndex++;
					startIndex = i + 2;
				}
			}

			data = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\3.bin");

			var bgTileData = data.Skip(87512).Take(25600).ToArray();
			var bgFolder = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\BGTiles";
			Directory.CreateDirectory(bgFolder);

			for (int i = 0; i < bgTileData.Length; i += 1600)
			{
				var tile = bgTileData.Skip(i).Take(1600).ToArray();
				var image = ImageFormatHelper.GenerateClutImage(palette2, tile, 80, 20);
				image.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\BGTiles\{i / 1600}.png", ImageFormat.Png);
			}

			var sideTileData = data.Skip(113112).Take(0xe10).ToArray();
			var sideFolder = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\SideTiles";
			Directory.CreateDirectory(sideFolder);

			for (int i = 0; i < sideTileData.Length; i += 400)
			{
				var tile = sideTileData.Skip(i).Take(400).ToArray();
				var image = ImageFormatHelper.GenerateClutImage(palette2, tile, 20, 20);
				image.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\SideTiles\{i / 80}.png", ImageFormat.Png);
			}

			startIndex = 0;
			spriteData = data.Skip(2672).Take(15148).ToArray();
			spriteData = spriteData.Concat(data.Skip(17820).Take(15270).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(33090).Take(15240).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(48330).Take(10830).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(59160).Take(28352).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x1C7E8).Take(21320).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x21B30).Take(71736).ToArray()).ToArray();

			for (int i = 0; i < spriteData.Length; i++)
			{
				if (spriteData[i] == 0x4e && spriteData[i + 1] == 0x75)
				{
					var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, startIndex, 0x180);
					var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
					CropImage(image, 192, 128, 0, 1).Save($@"{outputFolder}\{spriteIndex}.png", ImageFormat.Png);
					spriteIndex++;
					startIndex = i + 2;
				}
			}
		}

		public static void ExtractLevel3Data(int level = 3)
		{

			var palFile = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\Palettes\0.bin";
			var palette = ConvertBytesToRGB(File.ReadAllBytes(palFile).Take(0x180).ToArray());
			var palFile2 = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\Palettes\384.bin";
			var palette2 = ConvertBytesToRGB(File.ReadAllBytes(palFile2).Take(0x180).ToArray());


			var data = File.ReadAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\2.bin");

			var spriteData = data.Skip(0x10b8).Take(1514).ToArray();
			var startIndex = 0;
			var spriteIndex = 0;
			var outputFolder = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\sprites";
			Directory.CreateDirectory(outputFolder);

			spriteData = spriteData.Concat(data.Skip(0x1A62).Take(18438).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x6268).Take(29510).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0xd5ae).Take(11960).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x10466).Take(19114).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x14F10).Take(81184).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x28C30).Take(19986).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x2DA42).Take(40270).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x37790).Take(13332).ToArray()).ToArray();

			for (int i = 0; i < spriteData.Length; i++)
			{
				if (spriteData[i] == 0x4e && spriteData[i + 1] == 0x75)
				{
					var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, startIndex, 0x180);
					var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
					CropImage(image, 192, 128, 0, 1).Save($@"{outputFolder}\{spriteIndex}.png", ImageFormat.Png);
					spriteIndex++;
					startIndex = i + 2;
				}
			}

			data = File.ReadAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\3.bin");

			var bgTileData = data.Skip(0x1CF20).Take(25600).ToArray();
			var bgFolder = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\BGTiles";
			Directory.CreateDirectory(bgFolder);

			for (int i = 0; i < bgTileData.Length; i += 1600)
			{
				var tile = bgTileData.Skip(i).Take(1600).ToArray();
				var image = ImageFormatHelper.GenerateClutImage(palette2, tile, 80, 20);
				image.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\BGTiles\{i / 1600}.png", ImageFormat.Png);
			}

			var sideTileData = data.Skip(0x23320).Take(0x1130).ToArray();
			var sideFolder = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\SideTiles";
			Directory.CreateDirectory(sideFolder);

			for (int i = 0; i < sideTileData.Length; i += 400)
			{
				var tile = sideTileData.Skip(i).Take(400).ToArray();
				var image = ImageFormatHelper.GenerateClutImage(palette2, tile, 20, 20);
				image.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\SideTiles\{i / 80}.png", ImageFormat.Png);
			}

			startIndex = 0;
			spriteData = data.Skip(0x24450).Take(22518).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x29C46).Take(10434).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x2C508).Take(0x65b2).ToArray()).ToArray();

			for (int i = 0; i < spriteData.Length; i++)
			{
				if (spriteData[i] == 0x4e && spriteData[i + 1] == 0x75)
				{
					var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, startIndex, 0x180);
					var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
					CropImage(image, 192, 128, 0, 1).Save($@"{outputFolder}\{spriteIndex}.png", ImageFormat.Png);
					spriteIndex++;
					startIndex = i + 2;
				}
			}
		}

		public static void ExtractLevel4Data(int level = 4)
		{

			var palFile = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\Palettes\0.bin";
			var palette = ConvertBytesToRGB(File.ReadAllBytes(palFile).Take(0x180).ToArray());
			var palFile2 = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\Palettes\384.bin";
			var palette2 = ConvertBytesToRGB(File.ReadAllBytes(palFile2).Take(0x180).ToArray());


			var data = File.ReadAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\2.bin");

			var spriteData = data.Skip(0x1394).Take(1514).ToArray();
			var startIndex = 0;
			var spriteIndex = 0;
			var outputFolder = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\sprites";
			Directory.CreateDirectory(outputFolder);

			spriteData = spriteData.Concat(data.Skip(0x1d3e).Take(117052).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x1e67a).Take(16778).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x22804).Take(9572).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x24D68).Take(18758).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x296AE).Take(29510).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x309F4).Take(11960).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x338AC).Take(28030).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x3A62A).Take(0x4d4).ToArray()).ToArray();

			for (int i = 0; i < spriteData.Length; i++)
			{
				if (spriteData[i] == 0x4e && spriteData[i + 1] == 0x75)
				{
					var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, startIndex, 0x180);
					var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
					CropImage(image, 192, 128, 0, 1).Save($@"{outputFolder}\{spriteIndex}.png", ImageFormat.Png);
					spriteIndex++;
					startIndex = i + 2;
				}
			}

			data = File.ReadAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\3.bin");

			var bgTileData = data.Skip(0x124DE).Take(25600).ToArray();
			var bgFolder = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\BGTiles";
			Directory.CreateDirectory(bgFolder);

			for (int i = 0; i < bgTileData.Length; i += 1600)
			{
				var tile = bgTileData.Skip(i).Take(1600).ToArray();
				var image = ImageFormatHelper.GenerateClutImage(palette2, tile, 80, 20);
				image.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\BGTiles\{i / 1600}.png", ImageFormat.Png);
			}

			var sideTileData = data.Skip(0x188DE).Take(0xe10).ToArray();
			var sideFolder = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\SideTiles";
			Directory.CreateDirectory(sideFolder);

			for (int i = 0; i < sideTileData.Length; i += 400)
			{
				var tile = sideTileData.Skip(i).Take(400).ToArray();
				var image = ImageFormatHelper.GenerateClutImage(palette2, tile, 20, 20);
				image.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\SideTiles\{i / 80}.png", ImageFormat.Png);
			}

			startIndex = 0;
			spriteData = data.Skip(0x1594).Take(68958).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x122f2).Take(492).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x196F6).Take(0x65b2).ToArray()).ToArray();

			for (int i = 0; i < spriteData.Length; i++)
			{
				if (spriteData[i] == 0x4e && spriteData[i + 1] == 0x75)
				{
					var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, startIndex, 0x180);
					var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
					CropImage(image, 192, 128, 0, 1).Save($@"{outputFolder}\{spriteIndex}.png", ImageFormat.Png);
					spriteIndex++;
					startIndex = i + 2;
				}
			}
		}

		public static void ExtractLevel5Data(int level = 5)
		{

			var palFile = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\Palettes\0.bin";
			var palette = ConvertBytesToRGB(File.ReadAllBytes(palFile).Take(0x180).ToArray());
			var palFile2 = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\Palettes\384.bin";
			var palette2 = ConvertBytesToRGB(File.ReadAllBytes(palFile2).Take(0x180).ToArray());


			var data = File.ReadAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\2.bin");

			var spriteData = data.Skip(0x1BC54).Take(1514).ToArray();
			var startIndex = 0;
			var spriteIndex = 0;
			var outputFolder = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\sprites";
			Directory.CreateDirectory(outputFolder);

			spriteData = spriteData.Concat(data.Skip(0x1C5FE).Take(20954).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x217D8).Take(29510).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x28B1E).Take(11960).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x2B9D6).Take(19362).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x30578).Take(22952).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x35F20).Take(0x50ac).ToArray()).ToArray();

			for (int i = 0; i < spriteData.Length; i++)
			{
				if (spriteData[i] == 0x4e && spriteData[i + 1] == 0x75)
				{
					var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, startIndex, 0x180);
					var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
					CropImage(image, 192, 128, 0, 1).Save($@"{outputFolder}\{spriteIndex}.png", ImageFormat.Png);
					spriteIndex++;
					startIndex = i + 2;
				}
			}

			data = File.ReadAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\3.bin");

			var bgTileData = data.Skip(0xa14).Take(25600).ToArray();
			var bgFolder = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\BGTiles";
			Directory.CreateDirectory(bgFolder);

			for (int i = 0; i < bgTileData.Length; i += 1600)
			{
				var tile = bgTileData.Skip(i).Take(1600).ToArray();
				var image = ImageFormatHelper.GenerateClutImage(palette2, tile, 80, 20);
				image.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\BGTiles\{i / 1600}.png", ImageFormat.Png);
			}

			var sideTileData = data.Skip(0x6E14).Take(0xe10).ToArray();
			var sideFolder = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\SideTiles";
			Directory.CreateDirectory(sideFolder);

			for (int i = 0; i < sideTileData.Length; i += 400)
			{
				var tile = sideTileData.Skip(i).Take(400).ToArray();
				var image = ImageFormatHelper.GenerateClutImage(palette2, tile, 20, 20);
				image.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\SideTiles\{i / 80}.png", ImageFormat.Png);
			}

			startIndex = 0;
			spriteData = data.Skip(0x7C24).Take(41800).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x11F6C).Take(19044).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x169D0).Take(18454).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x1B1E6).Take(25430).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x2153C).Take(0x12256).ToArray()).ToArray();

			for (int i = 0; i < spriteData.Length; i++)
			{
				if (spriteData[i] == 0x4e && spriteData[i + 1] == 0x75)
				{
					var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, startIndex, 0x180);
					var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
					CropImage(image, 192, 128, 0, 1).Save($@"{outputFolder}\{spriteIndex}.png", ImageFormat.Png);
					spriteIndex++;
					startIndex = i + 2;
				}
			}
		}
		public static void ExtractLevel6Data(int level = 6)
		{

			var palFile = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\Palettes\0.bin";
			var palette = ConvertBytesToRGB(File.ReadAllBytes(palFile).Take(0x180).ToArray());
			var palFile2 = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\Palettes\384.bin";
			var palette2 = ConvertBytesToRGB(File.ReadAllBytes(palFile2).Take(0x180).ToArray());


			var data = File.ReadAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\2.bin");

			var spriteData = data.Skip(0x1E480).Take(1514).ToArray();
			var startIndex = 0;
			var spriteIndex = 0;
			var outputFolder = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\sprites";
			Directory.CreateDirectory(outputFolder);

			spriteData = spriteData.Concat(data.Skip(0x1EE2A).Take(19120).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x238DA).Take(17134).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x27BC8).Take(16928).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x2BDE8).Take(16798).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x2FF86).Take(0xa088).ToArray()).ToArray();

			for (int i = 0; i < spriteData.Length; i++)
			{
				if (spriteData[i] == 0x4e && spriteData[i + 1] == 0x75)
				{
					var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, startIndex, 0x180);
					var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
					CropImage(image, 192, 128, 0, 1).Save($@"{outputFolder}\{spriteIndex}.png", ImageFormat.Png);
					spriteIndex++;
					startIndex = i + 2;
				}
			}

			data = File.ReadAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\3.bin");

			var bgTileData = data.Skip(0xB7C).Take(25600).ToArray();
			var bgFolder = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\BGTiles";
			Directory.CreateDirectory(bgFolder);

			for (int i = 0; i < bgTileData.Length; i += 1600)
			{
				var tile = bgTileData.Skip(i).Take(1600).ToArray();
				var image = ImageFormatHelper.GenerateClutImage(palette2, tile, 80, 20);
				image.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\BGTiles\{i / 1600}.png", ImageFormat.Png);
			}

			var sideTileData = data.Skip(0x6F7C).Take(0x1770).ToArray();
			var sideFolder = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\SideTiles";
			Directory.CreateDirectory(sideFolder);

			for (int i = 0; i < sideTileData.Length; i += 400)
			{
				var tile = sideTileData.Skip(i).Take(400).ToArray();
				var image = ImageFormatHelper.GenerateClutImage(palette2, tile, 20, 20);
				image.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\SideTiles\{i / 80}.png", ImageFormat.Png);
			}

			startIndex = 0;
			spriteData = data.Skip(0x86ec).Take(0x28bb2).ToArray();

			for (int i = 0; i < spriteData.Length; i++)
			{
				if (spriteData[i] == 0x4e && spriteData[i + 1] == 0x75)
				{
					var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, startIndex, 0x180);
					var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
					CropImage(image, 192, 128, 0, 1).Save($@"{outputFolder}\{spriteIndex}.png", ImageFormat.Png);
					spriteIndex++;
					startIndex = i + 2;
				}
			}
		}

		public static void ExtractLevelBData(string level = "b")
		{

			var palFile = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\Palettes\0.bin";
			var palette = ConvertBytesToRGB(File.ReadAllBytes(palFile).Take(0x180).ToArray());
			var palFile2 = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\Palettes\384.bin";
			var palette2 = ConvertBytesToRGB(File.ReadAllBytes(palFile2).Take(0x180).ToArray());


			var data = File.ReadAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\2.bin");

			var spriteData = data.Skip(0x21500).Take(19732).ToArray();
			var startIndex = 0;
			var spriteIndex = 0;
			var outputFolder = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\sprites";
			Directory.CreateDirectory(outputFolder);

			spriteData = spriteData.Concat(data.Skip(0x26214).Take(29510).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x2D55A).Take(0x7e22).ToArray()).ToArray();

			for (int i = 0; i < spriteData.Length; i++)
			{
				if (spriteData[i] == 0x4e && spriteData[i + 1] == 0x75)
				{
					var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, startIndex, 0x180);
					var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
					CropImage(image, 192, 128, 0, 1).Save($@"{outputFolder}\{spriteIndex}.png", ImageFormat.Png);
					spriteIndex++;
					startIndex = i + 2;
				}
			}

			data = File.ReadAllBytes($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\3.bin");

			var bgTileData = data.Skip(0x1E32).Take(57600).ToArray();
			var bgFolder = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\BGTiles";
			Directory.CreateDirectory(bgFolder);

			for (int i = 0; i < bgTileData.Length; i += 1600)
			{
				var tile = bgTileData.Skip(i).Take(1600).ToArray();
				var image = ImageFormatHelper.GenerateClutImage(palette2, tile, 80, 20);
				image.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\BGTiles\{i / 1600}.png", ImageFormat.Png);
			}

			var sideTileData = data.Skip(0xFF32).Take(0x1C20).ToArray();
			var sideFolder = $@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\SideTiles";
			Directory.CreateDirectory(sideFolder);

			for (int i = 0; i < sideTileData.Length; i += 400)
			{
				var tile = sideTileData.Skip(i).Take(400).ToArray();
				var image = ImageFormatHelper.GenerateClutImage(palette2, tile, 20, 20);
				image.Save($@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level{level}\SideTiles\{i / 80}.png", ImageFormat.Png);
			}

			startIndex = 0;
			spriteData = data.Skip(0x310).Take(6946).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x11B52).Take(49178).ToArray()).ToArray();
			spriteData = spriteData.Concat(data.Skip(0x1DB6C).Take(0x12750).ToArray()).ToArray();

			for (int i = 0; i < spriteData.Length; i++)
			{
				if (spriteData[i] == 0x4e && spriteData[i + 1] == 0x75)
				{
					var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, startIndex, 0x180);
					var image = ImageFormatHelper.GenerateClutImage(palette, output, 384, 240, true);
					CropImage(image, 192, 128, 0, 1).Save($@"{outputFolder}\{spriteIndex}.png", ImageFormat.Png);
					spriteIndex++;
					startIndex = i + 2;
				}
			}
		}

		public static void ExtractGoGfx()
		{
			var data = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\go_gfx\1.bin");
			var palettData = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\go_gfx\0.bin").Skip(120).Take(0x900).ToArray();
			var palettes = new List<List<Color>>();

			for (int i = 0; i < palettData.Length; i += 0x180)
			{
				var palette = ConvertBytesToRGB(palettData.Skip(i).Take(0x180).ToArray());
				palettes.Add(palette);
			}

			var clutImageData = data.Take(0x14A00).ToArray();
			var clutFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\go_gfx\clut";
			Directory.CreateDirectory(clutFolder);

			var image = ImageFormatHelper.GenerateClutImage(palettes[1], clutImageData, 384, 220);
			image.Save($@"{clutFolder}\0.png", ImageFormat.Png);

			data = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\go_gfx\2.bin");

			var spriteData = data.Skip(1280).Take(0x32d34).ToArray();
			var startIndex = 0;
			var spriteIndex = 0;
			var outputFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\go_gfx\sprites";
			Directory.CreateDirectory(outputFolder);

			for (int i = 0; i < spriteData.Length; i++)
			{
				if (spriteData[i] == 0x4e && spriteData[i + 1] == 0x75)
				{
					var pIndex = 0;
					var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, startIndex, 0x180);
					image = ImageFormatHelper.GenerateClutImage(palettes[pIndex], output, 384, 240, true);
					CropImage(image, 192, 128, 0, 1).Save($@"{outputFolder}\{spriteIndex}.png", ImageFormat.Png);
					spriteIndex++;
					startIndex = i + 2;
				}
			}

			data = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\go_gfx\3.bin");

			spriteData = data.Skip(0x5a0).Take(0x2db1e).ToArray();
			startIndex = 0;
			for (int i = 0; i < spriteData.Length; i++)
			{
				if (spriteData[i] == 0x4e && spriteData[i + 1] == 0x75)
				{
					var pIndex = 0;
					var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, startIndex, 0x180);
					ImageFormatHelper.GenerateClutImage(palettes[pIndex], output, 384, 240, true).Save($@"{outputFolder}\{spriteIndex}.png", ImageFormat.Png);
					spriteIndex++;
					startIndex = i + 2;
				}
			}
		}

		public static void ExtractConGfx()
		{
			var data = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\con_gfx\1.bin");
			var palettData= File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\con_gfx\0.bin").Skip(120).Take(0x900).ToArray();
			var palettes = new List<List<Color>>();

			for (int i = 0; i < palettData.Length; i+=0x180) 
			{
				var palette = ConvertBytesToRGB(palettData.Skip(i).Take(0x180).ToArray());
				palettes.Add(palette);
			}

			var clutImageData = data.Skip(444).Take(70400).ToArray();
			var clutFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\con_gfx\clut";
			Directory.CreateDirectory(clutFolder);
			
			var image = ImageFormatHelper.GenerateClutImage(palettes[1], clutImageData, 320, 220);
			image.Save($@"{clutFolder}\0.png", ImageFormat.Png);

			clutImageData = data.Skip(70844).Take(61120).ToArray();
			image = ImageFormatHelper.GenerateClutImage(palettes[3], clutImageData, 320, 191);
			image.Save($@"{clutFolder}\1.png", ImageFormat.Png);

			var spriteData = data.Skip(131964).Take(0xd036).ToArray();
			var startIndex = 0;
			var spriteIndex = 0;
			var outputFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\con_gfx\sprites";
			Directory.CreateDirectory(outputFolder);

			for (int i = 0; i < spriteData.Length; i++)
			{
				if (spriteData[i] == 0x4e && spriteData[i + 1] == 0x75)
				{
					var pIndex = spriteIndex == 32 ? 2: spriteIndex < 32 ? 3 : 4;
					var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, startIndex, 0x180);
					image = ImageFormatHelper.GenerateClutImage(palettes[pIndex], output, 384, 240, true);
					CropImage(image, 192, 128, 0, 1).Save($@"{outputFolder}\{spriteIndex}.png", ImageFormat.Png);
					spriteIndex++;
					startIndex = i + 2;
				}
			}

			data = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\con_gfx\2.bin");

			clutImageData = data.Skip(0x1C).Take(61120).ToArray();
			image = ImageFormatHelper.GenerateClutImage(palettes[5], clutImageData, 320, 191);
			image.Save($@"{clutFolder}\2.png", ImageFormat.Png);

			spriteData = data.Skip(61584).Take(0x265f6).ToArray();
			startIndex = 0;
			for (int i = 0; i < spriteData.Length; i++)
			{
				if (spriteData[i] == 0x4e && spriteData[i + 1] == 0x75)
				{
					var pIndex = (spriteIndex >43 && spriteIndex < 52)  ? 5 : spriteIndex > 72 ? 0 : spriteIndex > 70 ? 3 : 4;
					var output = CompiledSpriteHelper.DecodeCompiledSprite(spriteData, startIndex, 0x180);
					ImageFormatHelper.GenerateClutImage(palettes[pIndex], output, 384, 240, true).Save($@"{outputFolder}\{spriteIndex}.png", ImageFormat.Png);
					spriteIndex++;
					startIndex = i + 2;
				}
			}
		}

		public static void ExtractMapInfo(string mapfile)
		{
			var data = File.ReadAllBytes(mapfile);

			var bgShorts = new List<ushort>();
			var tileShorts = new List<ushort>();
			var itemShorts = new List<ushort>();

			var mapBgData = data.Take(0x960).ToArray();
			for (int i = 0; i < mapBgData.Length; i += 2)
			{
				var s = BitConverter.ToUInt16(mapBgData.Skip(i).Take(2).Reverse().ToArray(), 0);
				bgShorts.Add(s);
			}

			var tileData = data.Skip(0x960).Take(0x1900).ToArray();
			for (int i = 0; i < tileData.Length; i += 2)
			{
				var s = BitConverter.ToUInt16(tileData.Skip(i).Take(2).Reverse().ToArray(), 0);
				tileShorts.Add(s);
			}

			var itemData = data.Skip(0x2260).Take(0x1900).ToArray();
			for (int i = 0; i < itemData.Length; i += 2)
			{
				var s = BitConverter.ToUInt16(itemData.Skip(i).Take(2).Reverse().ToArray(), 0);
				itemShorts.Add(s);
			}

			var outputName = Path.GetFileNameWithoutExtension(mapfile);
			var outputFolder = Path.Combine(Path.GetDirectoryName(mapfile), outputName, "Output");
			Directory.CreateDirectory(outputFolder);

			// write as comma separated values, 16 values to a line
			var sb = new StringBuilder();

			for (int i = 0; i < bgShorts.Count; i++)
			{
				var val = bgShorts[i];
				switch (val)
				{
					case 0xa0:
						val = 1;
						break;
					case 0xa1:
						val = 2;
						break;
					case 0xa2:
						val = 3;
						break;
					case 0xa3:
						val = 4;
						break;
					case 0xa4:
						val = 5;
						break;
					case 0xa5:
						val = 6;
						break;
					case 0xb0:
						val = 7;
						break;
					case 0xb1:
						val = 8;
						break;
					case 0xb2:
						val = 9;
						break;
					case 0xb3:
						val = 10;
						break;
					case 0xb4:
						val = 11;
						break;
					case 0xb5:
						val = 12;
						break;
					case 0xc0:
						val = 13;
						break;
					case 0xc1:
						val = 14;
						break;
					case 0xc2:
						val = 15;
						break;
					case 0xc3:
						val = 16;
						break;
					case 0xc4:
						val = 17;
						break;
					case 0xc5:
						val = 18;
						break;
					default:
						val = 0;
						break;
				}
				if (val != 0) val += 324;
				sb.Append($"{val},");
				if ((i + 1) % 6 == 0)
				{
					sb.AppendLine();
				}
			}

			File.WriteAllText(Path.Combine(outputFolder, "bg.txt"), sb.ToString());

			sb.Clear();


			for (int i = 0; i < tileShorts.Count; i++)
			{
				sb.Append($"{tileShorts[i] + 1},");
				if ((i + 1) % 16 == 0)
				{
					sb.AppendLine();
				}
			}

			File.WriteAllText(Path.Combine(outputFolder, "tiles.txt"), sb.ToString());

			sb.Clear();

			for (int i = 0; i < itemShorts.Count; i++)
			{
				sb.Append($"{itemShorts[i] + 1},");
				if ((i + 1) % 16 == 0)
				{
					sb.AppendLine();
				}
			}

			File.WriteAllText(Path.Combine(outputFolder, "items.txt"), sb.ToString());

		}
	}

	public class ApprenticeSubFileOffsets
	{
		public int SubFileIndex { get; set; }
		public List<ApprenticeOffset> SpriteOffsets { get; set; }
		public List<ApprenticeOffset> ClutOffsets { get; set; }
	}

	public class ApprenticeOffset
	{
		public int Offset { get; set; }
		public int Length { get; set; }
	}

	public class VisionFactoryFile
	{
		public string FilePath { get; set; }
		public int SubFileCount { get; set; }
		public List<int> SubFileOffsets { get; set; }
		public List<byte[]> SubFiles { get; set; }
		public byte[] FileData { get; set; }

		public VisionFactoryFile(string filePath, byte[]? fileData = null)
		{
			FilePath = filePath;
			SubFileOffsets = new List<int>();
			SubFiles = new List<byte[]>();
			FileData = fileData ?? File.ReadAllBytes(filePath);
			SubFileCount = FileData.Skip(1).Take(1).ToArray()[0];

			for (int i = 0; i < SubFileCount; i++)
			{
				var offset = 0x2 + (i * 4);
				var length = BitConverter.ToInt32(FileData.Skip(offset).Take(4).Reverse().ToArray(), 0);
				length = (length % 0x800) == 0 ? 0x800 * (length / 0x800) : 0x800 * ((length / 0x800) + 1);
				SubFileOffsets.Add(length);
			}

			var data = FileData.Skip(0x800).ToArray();

			for (int i = 0; i < SubFileCount; i++)
			{
				var blob = data.Skip(0).Take(SubFileOffsets[i]).ToArray();
				SubFiles.Add(blob);
				data = data.Skip(SubFileOffsets[i]).ToArray();
			}
		}
	}

	public class VisionFactorySubFile
	{
		public string FilePath { get; set; }
		public int SubFileCount { get; set; }
		public List<int> SubFileOffsets { get; set; }
		public List<byte[]> SubFiles { get; set; }
		public byte[] FileData { get; set; }

		public VisionFactorySubFile(string filePath)
		{
			FilePath = filePath;
			SubFileOffsets = new List<int>();
			SubFiles = new List<byte[]>();
			FileData = File.ReadAllBytes(filePath);
			SubFileCount = FileData.Skip(1).Take(1).ToArray()[0];

			for (int i = 0; i < SubFileCount; i++)
			{
				var offset = 0x2 + (i * 4);
				var length = BitConverter.ToInt32(FileData.Skip(offset).Take(4).Reverse().ToArray(), 0);
				SubFileOffsets.Add(length+2);
			}

			for (int i = 1; i < SubFileCount; i++)
			{
				var bytesToTake = i == SubFileCount - 1 ? FileData.Length - SubFileOffsets[i] : i >= 1 ? SubFileOffsets[i] - SubFileOffsets[i-1] : SubFileOffsets[i];
				var blob = FileData.Skip(i == SubFileCount - 1 ? SubFileOffsets[i] : SubFileOffsets[i-1]).Take(bytesToTake).ToArray();
				SubFiles.Add(blob);		
			}
		}
	}
}

// var palettData = blobs[0].Skip(0x78).Take(0x300).ToArray();
// var palFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level1\Palettes";
// Directory.CreateDirectory(palFolder);
// var palettes = new List<List<Color>>();
// for (int i = 0; i < palettData.Length; i += 0x180)
// {
//     var bytes = palettData.Skip(i).Take(0x180).ToArray();
//     //File.WriteAllBytes($@"{palFolder}\{i}.bin", bytes);
//     var palette = ConvertBytesToRGB(bytes);
//     //CreateLabelledPalette(palette).Save($@"{palFolder}\{i}.png", ImageFormat.Png);
//     palettes.Add(palette);
// }


// var outputFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\level1\Output";
// var spriteBlobs = new List<byte[]>(); //FileHelpers.ExtractSpriteByteSequences(null, blobs[2], [0x48, 0xe7], [0x4e, 0x75]);
// var spriteOffsets = new List<int>();
// for (int i = 2; i < blobs[2].Length; i += 4)
// {
//     if (blobs[2][i] == 0xFF && blobs[2][i + 1] == 0xFF)
//     {
//         break;
//     }
//     var offset = BitConverter.ToInt32(blobs[2].Skip(i).Take(4).Reverse().ToArray(), 0) + 2;
//     spriteOffsets.Add(offset);
// }

// var blobOutputFolder = Path.Combine(outputFolder, "Blobs");
// Directory.CreateDirectory(blobOutputFolder);

// for (int i = 0; i < spriteOffsets.Count; i++)
// {
//     var bytesToTake = i == spriteOffsets.Count - 1 ? blobs[2].Length - spriteOffsets[i] : spriteOffsets[i + 1] - spriteOffsets[i];
//     var blob = blobs[2].Skip(spriteOffsets[i]).Take(bytesToTake).ToArray();
//     spriteBlobs.Add(blob);
//     //File.WriteAllBytes($@"{blobOutputFolder}\{i}.bin", blob);
// }

// Directory.CreateDirectory(outputFolder);
// var decodedBlobFolder = Path.Combine(outputFolder, "Decoded");
// Directory.CreateDirectory(decodedBlobFolder);
// var images = new List<Image>();
// foreach (var (blob, index) in spriteBlobs.WithIndex())
// {
//     // foreach (var (palette, pIndex) in palettes.WithIndex())
//     // {
//     var decodedBlob = CompiledSpriteHelper.DecodeCompiledSprite(blob, 0, 0x180);
//     //File.WriteAllBytes($@"{decodedBlobFolder}\{index}.bin", decodedBlob);
//     var image = ImageFormatHelper.GenerateClutImage(palettes[1], decodedBlob, 384, 240, true);
//     image = (Bitmap)CropImage(image, 20, 5, 0, 1);
//     images.Add(image);
//     var outputName = Path.Combine(outputFolder, $"{index / 4}.png");
//     if (OperatingSystem.IsWindowsVersionAtLeast(6, 1) && images.Count == 4)
//     {
//         var finalImage = CombineImages(images, 20, 5, 20);
//         finalImage.Save(outputName, ImageFormat.Png);
//         images.Clear();
//         //image.Save(outputName, ImageFormat.Png);
//     }
//     // }
// }


// map data parsing
// var data = File.ReadAllBytes(@"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\map2_1\0.bin");

// var mapBgData = data.Take(0x960).ToArray();
// var bgShorts = new List<ushort>();
// var tileShorts = new List<ushort>();
// var itemShorts = new List<ushort>();
// for (int i = 0; i < mapBgData.Length; i += 2)
// {
//     var s = BitConverter.ToUInt16(mapBgData.Skip(i).Take(2).Reverse().ToArray(), 0);
//     bgShorts.Add(s);
// }


// var tileData = data.Skip(0x960).Take(0x1900).ToArray();
// for (int i = 0; i < tileData.Length; i += 2)
// {
//     var s = BitConverter.ToUInt16(tileData.Skip(i).Take(2).Reverse().ToArray(), 0);
//     tileShorts.Add(s);
// }
// var itemData = data.Skip(0x2260).Take(0x1900).ToArray();
// for (int i = 0; i < itemData.Length; i += 2)
// {
//     var s = BitConverter.ToUInt16(itemData.Skip(i).Take(2).Reverse().ToArray(), 0);
//     itemShorts.Add(s);
// }

// var outputFolder = @"C:\Dev\Projects\Gaming\CD-i\Disc Images\Extracted\The Apprentice - Release\Analysis\map2_1\Output";
// Directory.CreateDirectory(outputFolder);

// // write as comma separated values, 16 values to a line
// var sb = new StringBuilder();

// for (int i = 0; i < bgShorts.Count; i++)
// {
//     sb.Append($"{bgShorts[i] + 1},");
//     if ((i + 1) % 16 == 0)
//     {
//         sb.AppendLine();
//     }
// }

// File.WriteAllText(Path.Combine(outputFolder, "bg.txt"), sb.ToString());

// sb.Clear();


// for (int i = 0; i < tileShorts.Count; i++)
// {
//     sb.Append($"{tileShorts[i] + 1},");
//     if ((i + 1) % 16 == 0)
//     {
//         sb.AppendLine();
//     }
// }

// File.WriteAllText(Path.Combine(outputFolder, "tiles.txt"), sb.ToString());

// sb.Clear();

// for (int i = 0; i < itemShorts.Count; i++)
// {
//     sb.Append($"{itemShorts[i] + 1},");
//     if ((i + 1) % 16 == 0)
//     {
//         sb.AppendLine();
//     }
// }

// File.WriteAllText(Path.Combine(outputFolder, "items.txt"), sb.ToString());

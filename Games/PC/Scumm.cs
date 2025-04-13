using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using ExtractCLUT.Helpers;
using ExtractCLUT.Model.Scumm;
using static ExtractCLUT.Helpers.ColorHelper;

namespace ExtractCLUT.Games.PC
{
	public static class ScummDecompressor
	{

	}

	public class ScummDataFile
	{
		public ScummIndexFile Index { get; set; }
		public RoomOffsetTable Table { get; set; }
		public byte[] Data { get; set; }

		public ScummDataFile(ScummIndexFile index, string path)
		{
			Index = index;
			Table = new RoomOffsetTable();
			ParseDataFile(path);
		}

		private void ParseDataFile(string path)
		{
			using (var reader = new BinaryReader(File.OpenRead(path)))
			{
				// Error if first 4 bytes are not LECF
				var magicBytes = reader.ReadBytes(4);
				if (Encoding.ASCII.GetString(magicBytes) != "LECF")
				{
					throw new Exception("Invalid data file");
				}
				// Get the Length of the LECF block, this should equal the file size
				var length = reader.ReadBigEndianUInt32();
				if (length != reader.BaseStream.Length)
				{
					throw new Exception("Invalid data file");
				}
				// Skip the next 8 bytes
				reader.ReadBytes(8);
				var count = reader.ReadByte();
				for (int i = 0; i < count; i++)
				{
					var roomOffset = new Room()
					{
						RoomNumber = reader.ReadByte(),
						Offset = reader.ReadUInt32()
					};
					Table.RoomOffsets.Add(roomOffset);
				}
				// Return to the start of the data and read into Data
				reader.BaseStream.Seek(0, SeekOrigin.Begin);
				Data = reader.ReadBytes((int)length);
				// verify that the data is the correct length
				if (Data.Length != length) throw new Exception("Invalid data file");
			}
		}

		public void DumpRoomData(int roomNumber, string path)
		{
			var room = Table.RoomOffsets.Where(x => x.RoomNumber == roomNumber).FirstOrDefault();
			if (room == null) throw new Exception("Room not found");

			// use room.Offset to find the start of the room data
			// read the 4 byte block header, then the 4 byte block length
			var data = Data.Skip((int)room.Offset).ToArray();
			using (var reader = new BinaryReader(new MemoryStream(data)))
			{
				var blockType = Encoding.ASCII.GetString(reader.ReadBytes(4));
				var blockLength = reader.ReadBigEndianUInt32();
				// return to beginning of block
				reader.BaseStream.Seek(0, SeekOrigin.Begin);
				// read the block data
				var blockData = reader.ReadBytes((int)blockLength);
				// write the block data to path
				File.WriteAllBytes(path, blockData);
			}
		}

		public RoomFile ParseRoomData(int roomNumber)
		{
			var roomFile = new RoomFile();
			var room = Index.Rooms.Where(x => x.RoomNumber == roomNumber).FirstOrDefault();
			var roomOffset = Table.RoomOffsets.Where(x => x.RoomNumber == roomNumber).FirstOrDefault()?.Offset;
			if (room == null || roomOffset == null) throw new Exception("Room and/or offset not found");
			roomFile.Room = room;
			// use room.Offset to find the start of the room data
			// read the 4 byte block header, then the 4 byte block length
			var data = Data.ToArray();
			using (var reader = new BinaryReader(new MemoryStream(data)))
			{
				reader.BaseStream.Seek((long)roomOffset - 8, SeekOrigin.Begin);
				var blockOffset = reader.BaseStream.Position;
				var blockType = Encoding.ASCII.GetString(reader.ReadBytes(4));
				var blockLength = reader.ReadBigEndianUInt32();
				var end = reader.BaseStream.Position + blockLength;
				while (reader.BaseStream.Position < end - 8)
				{
					blockOffset = reader.BaseStream.Position;
					blockType = Encoding.ASCII.GetString(reader.ReadBytes(4));
					blockLength = reader.ReadBigEndianUInt32();
					switch (blockType)
					{
						case "ROOM":
							break;
						case "RMHD":
						reader.ReadBytes(4);
							var roomHeader = new RoomHeader(reader.ReadBytes(6));
							roomFile.Header = roomHeader;
							break;
						case "CYCL":
							// TODO Parse CYCL block
							reader.ReadBytes((int)blockLength - 8);
							break;
						case "TRNS":
							roomFile.TransparencyIndex = reader.ReadByte();
							reader.ReadByte(); // skip the trailing byte
							break;
						case "EPAL":
							// TODO Parse EPAL block
							reader.ReadBytes((int)blockLength - 8);
							break;
						case "WRAP":
							// TODO Parse EPAL block
							break;
						case "PALS":
							// TODO Parse EPAL block
							break;
						default:
							// TODO Parse BOXD block
							Console.WriteLine($"Unknown block type: {blockType}");
							reader.ReadBytes((int)blockLength - 8);
							break;
						case "APAL":
							var paletteBytes = reader.ReadBytes((int)blockLength - 8);
							File.WriteAllBytes($@"C:\Dev\Gaming\PC_DOS\Extractions\DIG\output\Room_Backgrounds\PALs\{roomFile.Room.RoomNumber}_{roomFile.Room.RoomName}.bin", paletteBytes);
							roomFile.Palette = ConvertBytesToRGB(paletteBytes);
							break;
						case "RMIM":
							ParseRMIMBlock(reader, blockLength - 8, ref roomFile);
							break;
						case "OBIM":
							if (roomFile.ObjectImages == null) roomFile.ObjectImages = new List<ObjectImage>();
							ParseOBIMBlock(reader, blockLength - 8, ref roomFile);
							break;
						case "COST":
							if (roomFile.Costumes == null) roomFile.Costumes = new List<Costume>();

							var cEnd = reader.BaseStream.Position + blockLength - 8;
							while (reader.BaseStream.Position < cEnd)
							{
								var costume = new Costume();
								costume.NumAnim = reader.ReadByte();
								costume.Format = reader.ReadByte();
								costume.PaletteSize = Utils.CheckBitState(costume.Format, 0) ? 32 : 16;
								costume.Palette = new List<byte>();
								for (int i = 0; i < costume.PaletteSize; i++)
								{
									costume.Palette.Add(reader.ReadByte());
								}

								costume.AnimCommandsOffset = reader.ReadUInt16();

								costume.LimbsOffsets = new List<ushort>();
								for (int i = 0; i < 16; i++)
								{
									costume.LimbsOffsets.Add(reader.ReadUInt16());
								}

								costume.AnimOffsets = new List<ushort>();
								for (int i = 0; i < costume.NumAnim + 1; i++)
								{
									costume.AnimOffsets.Add(reader.ReadUInt16());
								}

								costume.Animations = new List<Animation>();
								for (int i = 0; i < costume.AnimOffsets.Count; i++)
								{
									//Por alguma razão, o NumAnimations não é o numero real de animações. Parece que tem um array "reservando" posições para animações 
									//não utilizadas.
									//O que eu quero dizer é que tem o indice de animações, mas esse indice as vezes aponta para um offset de animação 0, ou seja,
									//o indice existe e diz que não aponta para nenhuma animação, pelo que entendi. Então só vou continuar lendo do binary stream
									//quando for o indice apontara para a próxima animação.
									if (costume.AnimOffsets[i] > 0)
									{
										Animation existingAnimation = costume?.Animations?.SingleOrDefault(x => x.Offset == costume?.AnimOffsets[i]);
										if (existingAnimation == null)
										{
											//Pra isso aqui funciona, a posição do binaryReader deve ser a mesma informada no offset. 
											//Se não for, então para no debug, porque tem alguma coisa errada com o meu código e 
											//preciso verificar o que é.#
											var lookback = DebugGetCurrentRelativePosition(reader, blockOffset);
											if (costume?.AnimOffsets[i] != lookback) Debugger.Break();

											var currentAnimation = new Animation();
											currentAnimation.Offset = costume.AnimOffsets[i];
											currentAnimation.LimbMask = reader.ReadUInt16();

											for (int j = 0; j < currentAnimation.NumLimbs; j++)
											{
												var currentDefinition = new AnimationDefinition();
												currentDefinition.Start = reader.ReadUInt16();
												if (!currentDefinition.Disabled)
												{
													currentDefinition.NoLoopAndEndOffset = reader.ReadByte();
												}
												currentAnimation.AnimDefinitions.Add(currentDefinition);
											}
											costume.Animations.Add(currentAnimation);
										}
									}
								}

								int cmdArraySize = (int)(costume.LimbsOffsets.First() - DebugGetCurrentRelativePosition(reader, blockOffset));

								costume.Commands = reader.ReadBytes(cmdArraySize).ToList();

								costume.Limbs = new List<Limb>();
								List<ushort> differentLimbsOnly = costume.LimbsOffsets.Distinct().ToList();
								for (int i = 0; i < differentLimbsOnly.Count - 1; i++)
								{
									Limb currentLimb = new Limb();
									currentLimb.OffSet = differentLimbsOnly[i];
									currentLimb.Size = (ushort)(differentLimbsOnly[i + 1] - differentLimbsOnly[i]);

									costume.Limbs.Add(currentLimb);
								}
								Limb lastLimb = new Limb();
								lastLimb.OffSet = differentLimbsOnly[differentLimbsOnly.Count - 1];

								var nextValue = reader.PeekUInt16();
								if (nextValue != 0)
								{
									lastLimb.Size = (ushort)(nextValue - lastLimb.OffSet);
								}

								if (lastLimb.Size > 0) costume.Limbs.Add(lastLimb);

								foreach (var limb in costume.Limbs)
								{
									if ((limb.Size % 2) != 0) Debugger.Break();
									if (limb.OffSet != DebugGetCurrentRelativePosition(reader, blockOffset)) Debugger.Break();

									//Como cada indice tem 2 bytes (ushort), então o total de entradas é o tamanho do limb dividido por 2
									for (int i = 0; i < (limb.Size / 2); i++)
									{
										ushort currentImageOffset = reader.ReadUInt16();
										limb.ImageOffsets.Add(currentImageOffset);
									}
								}

								costume.Pictures = new List<CostumeImageData>();
								var pictureHeaderSize = (costume.Format & 0x7E) == 0x60 ? 14 : 12;

								CostumeImageData currentPicture = null;
								foreach (var limb in costume.Limbs)
								{
									if (limb.ImageOffsets.Count > 0)
									{
										if (currentPicture != null)
										{
											ushort firstWithImageOffSet = limb.ImageOffsets.Where(x => x > 0).First();
											currentPicture.ImageDataSize = (ushort)(firstWithImageOffSet - currentPicture.ImageStartOffSet - pictureHeaderSize);
											costume.Pictures.Add(currentPicture);
										}

										for (int i = 0; i < limb.ImageOffsets.Count - 1; i++)
										{
											if (limb.ImageOffsets[i] > 0)
											{
												ushort nextWithImageOffset = limb.ImageOffsets.Skip(i + 1).Where(x => x > 0).First();

												CostumeImageData currentCostumeImageData = new CostumeImageData();
												currentCostumeImageData.ImageStartOffSet = limb.ImageOffsets[i];
												currentCostumeImageData.ImageDataSize = (ushort)(nextWithImageOffset - currentCostumeImageData.ImageStartOffSet - pictureHeaderSize);

												costume.Pictures.Add(currentCostumeImageData);
											}
										}
										currentPicture = new CostumeImageData();
										ushort lastWithImageOffset = limb.ImageOffsets.Where(x => x > 0).Last();
										currentPicture.ImageStartOffSet = lastWithImageOffset;
									}
								}
								if (currentPicture != null)
								{
									uint sizeVerify = blockLength - 2;
									currentPicture.ImageDataSize = (ushort)((sizeVerify - currentPicture.ImageStartOffSet) - pictureHeaderSize);
									costume.Pictures.Add(currentPicture);
								}
								foreach (CostumeImageData picture in costume.Pictures)
								{
									/*
									width            : 16le
									height           : 16le
									rel_x            : s16le
									rel_y            : s16le
									move_x           : s16le
									move_y           : s16le
									redir_limb       : 8 only present if((format & 0x7E) == 0x60)
									redir_pict       : 8 only present if((format & 0x7E) == 0x60)
									rle data
									 */
									picture.Width = reader.ReadUInt16();
									picture.Height = reader.ReadUInt16();
									picture.RelX = reader.ReadInt16();
									picture.RelY = reader.ReadInt16();
									picture.MoveX = reader.ReadInt16();
									picture.MoveY = reader.ReadInt16();
									if (pictureHeaderSize == 14)
									{
										//Mexendo, a impressão que parece é que só tem informações de REDIR_LIMB e REDIR_PICT quando
										//o size == 0. Não sei porque, mas é isso que ta parecendo.
										//Vou fazer mais uns testes.
										picture.HasRedirInfo = true;
										picture.RedirLimb = reader.ReadByte();
										picture.RedirPict = reader.ReadByte();
									}
									picture.ImageData = reader.ReadBytes(picture.ImageDataSize);
								}
								roomFile.Costumes.Add(costume);
							}
							foreach (var (costume, cIndex) in roomFile.Costumes.WithIndex())
							{
								foreach (var (picture, pIndex) in costume.Pictures.WithIndex())
								{
									if (picture.Width == 0 || picture.Height == 0) continue;
									var decoded = DecodeCOSTImage(picture, costume, picture.Width, picture.Height, roomFile.Palette);
									var image = ImageFormatHelper.GenerateClutImage(roomFile.Palette, decoded, picture.Width, picture.Height, true, 0);
									var outputFolder = $@"C:\Dev\Gaming\PC_DOS\Extractions\Dig\output\Room_Costumes\{roomFile.Room.RoomNumber}_{roomFile.Room.RoomName}";
									if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);
									image.Save($@"{outputFolder}\{cIndex}_{pIndex}.png");
									outputFolder = $@"C:\Dev\Gaming\PC_DOS\Extractions\Dig\output\Room_Costumes\bins\{roomFile.Room.RoomNumber}_{roomFile.Room.RoomName}";
									if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);
									File.WriteAllBytes($@"{outputFolder}\{cIndex}_{pIndex}.bin", decoded);
								}
							}
							break;
					}
				}
			}
			return roomFile;
		}
		private long DebugGetCurrentRelativePosition(BinaryReader binaryReader, long blockOffSet)
		{
			return binaryReader.BaseStream.Position - (blockOffSet + 2);
		}
		private void ParseOBIMBlock(BinaryReader reader, uint v, ref RoomFile roomFile)
		{
			var objectImageHeader = new ObjectImageHeader();
			var end = reader.BaseStream.Position + v;
			var imageCount = 0;
			while (reader.BaseStream.Position < end)
			{
				var blockType = Encoding.ASCII.GetString(reader.ReadBytes(4));
				var blockLength = reader.ReadBigEndianUInt32();
				switch (blockType)
				{
					case "IMHD":
						objectImageHeader = new ObjectImageHeader()
						{
							Id = reader.ReadUInt16(),
							ImageCount = reader.ReadUInt16(),
							ZPlaneCount = reader.ReadUInt16(),
							Unknown = reader.ReadUInt16(),
							X = reader.ReadUInt16(),
							Y = reader.ReadUInt16(),
							Width = reader.ReadUInt16(),
							Height = reader.ReadUInt16()
						};
						break;
					case "SMAP":
						var width = objectImageHeader.Width;
						var height = objectImageHeader.Height;
						var stripCount = objectImageHeader.Width / 8;
						var smapImageBytes = ParseSMAPBlock(reader, blockLength, blockLength - 8, stripCount, width, height);
						var image = ImageFormatHelper.GenerateClutImage(roomFile.Palette, smapImageBytes, width, height, true, roomFile.TransparencyIndex, false);
						var outputFolder = $@"C:\Dev\Gaming\PC_DOS\Extractions\DIG\output\Room_Objects\{roomFile.Room.RoomNumber}_{roomFile.Room.RoomName}";
						if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);
						image.Save($@"{outputFolder}\{objectImageHeader.Id}_X{objectImageHeader.X}_Y{objectImageHeader.Y}_{imageCount}.png");
						imageCount++;
						break;
					case "IM01":
						break;
					default:
						// TODO Parse BOXD block
						reader.ReadBytes((int)blockLength - 8);
						break;
				}
			}
		}

		private void ParseRMIMBlock(BinaryReader reader, uint v, ref RoomFile roomFile)
		{
			var end = reader.BaseStream.Position + v;
			while (reader.BaseStream.Position < end)
			{
				var blockType = Encoding.ASCII.GetString(reader.ReadBytes(4));
				var blockLength = reader.ReadBigEndianUInt32();
				switch (blockType)
				{
					case "RMIH":
						roomFile.ZBufferCount = reader.ReadByte();
						reader.ReadByte(); // skip the trailing byte
						break;
					case "IM00":
						break;
					case "SMAP":
						var width = roomFile.Header.Width;
						var height = roomFile.Header.Height;
						var stripCount = roomFile.Header.Width / 8;
						var smapImageBytes = ParseSMAPBlock(reader, blockLength, blockLength - 8, stripCount, width, height);
						var image = ImageFormatHelper.GenerateClutImage(roomFile.Palette, smapImageBytes, width, height);
						var outputFolder = $@"C:\Dev\Gaming\PC_DOS\Extractions\DIG\output\Room_Backgrounds";
						if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);
						image.Save($@"{outputFolder}\{roomFile.Room.RoomNumber}_{roomFile.Room.RoomName}.png");
						break;
					default:
						reader.ReadBytes((int)blockLength - 8);
						break;
				}
			}
		}

		private byte[] ParseSMAPBlock(BinaryReader reader, uint blockLength, uint v, int stripCount, int width, int height)
		{
			var end = reader.BaseStream.Position + v;
			var strips = new List<ImageStrip>();
			for (int i = 0; i < stripCount; i++)
			{
				var strip = new ImageStrip()
				{
					Offset = reader.ReadUInt32(),
				};
				strips.Add(strip);
			}
			for (int i = 0; i < stripCount; i++)
			{
				var stripLength = i == stripCount - 1 ? blockLength - strips[i].Offset : strips[i + 1].Offset - strips[i].Offset;
				strips[i].CompressionId = reader.ReadByte();
				strips[i].Data = reader.ReadBytes((int)stripLength - 1);
			}
			reader.BaseStream.Seek(end, SeekOrigin.Begin);
			return DecodeSMAPImage(strips, width, height);
		}

		private byte[] DecodeSMAPImage(List<ImageStrip> strips, int width, int height)
		{
			// we need a list of byte arrays to hold the lines of the image
			var imageData = new List<byte[]>();
			for (int i = 0; i < height; i++)
			{
				imageData.Add(new byte[width]);
			}

			var currentOffset = 0;
			foreach (var (strip, index) in strips.WithIndex())
			{
				// if (roomFile.Room.RoomNumber == 10) {
				// 	Debugger.Break();
				// }
				var currentLine = 0;
				var currentColumn = 0;
				var paletteIndex = 0;
				currentOffset = index * 8;
				var renderingDirection = strip.RenderingDirection;

				var numBitPerPaletteEntry = strip.CompressionId - strip.ParamSubtraction;
				var subtractionVariable = 1;

				if (strip.CompressionType != CompressionType.Uncompressed)
				{
					var bitStreamManager = new BitStreamManager(strip.Data);
					bool finishDecode = false;
					paletteIndex = bitStreamManager.ReadByte();

					imageData[currentLine][currentOffset + currentColumn] = (byte)paletteIndex;
					while (!finishDecode)
					{
						var isActionChange = bitStreamManager.ReadBit();

						if (isActionChange)
						{
							if (strip.CompressionType == CompressionType.Method1)
							{
								var nextBitCode = bitStreamManager.ReadBit();
								if (!nextBitCode) // next bit is false (0). 
								{
									//previous bit was false (0). So now we have a code 10 (1 previous bit and 0 from this one).
									//10: Read a new palette index from the bit stream, i.e., read the number of bits that the parameter 
									//    specifies as a value (see the Tiny Bits of Decompression chapter).
									//    Set the subtraction variable to 1.
									//    Draw the next pixel.
									paletteIndex = bitStreamManager.ReadValue(numBitPerPaletteEntry);
									subtractionVariable = 1;
								}
								else
								{
									//previous bit was true (1). So now we have a code 11 (1 previous bit and 1 from this one).
									//11 alone is not enough. We will need the next bit to know what to do.
									nextBitCode = bitStreamManager.ReadBit();
									if (!nextBitCode)
									{
										//previous bit was false. Now we have a code 110 (1 from first bit, 1 from second and 0 from the last read.)
										//110: Subtract the subtraction variable from the palette index.
										//     Draw the next pixel.
										paletteIndex -= subtractionVariable;
									}
									else
									{
										//previous bit was true. Now we have a code 111 (1 from first bit, 1 from second and 1 from the last read.)
										//111: Negate the subtraction variable (i.e., if it's 1, change it to -1, if it's -1, change it to 1). 
										//     Subtract it from the palette index.
										//     Draw the next pixel.
										subtractionVariable = subtractionVariable * -1;
										paletteIndex -= subtractionVariable;
									}
								}
								if (renderingDirection == RenderingDirection.Horizontal)
								{
									currentColumn++;
									if (currentColumn == 8)
									{
										currentColumn = 0;
										currentLine++;
									}
								}
								else
								{
									currentLine++;
									if (currentLine == height)
									{
										currentLine = 0;
										currentColumn++;
									}
								}
								if (currentLine < height && currentOffset + currentColumn < width)
								{
									imageData[currentLine][currentOffset + currentColumn] = (byte)paletteIndex;
								}
							}
							else
							{
								// Decoder specific actions. If we are here, is because the previous read bit by DecodeCompressed was True (1).

								/*
								10: Read a new palette index from the bitstream (i.e., the number of bits specified by the parameter), and draw the next pixel.
								11: Read the next 3 bit value, and perform an action, depending on the value:
											 000 (0): Decrease current palette index by 4.
											 001 (1): Decrease current palette index by 3.
											 010 (2): Decrease current palette index by 2.
											 011 (3): Decrease current palette index by 1.
											 100 (4): Read next 8 bits. Draw the number of pixels specified by these 8 bits with the current palette index (somewhat similar to RLE).
											 101 (5): Increase current palette index by 1.
											 110 (6): Increase current palette index by 2.
											 111 (7): Increase current palette index by 3. 
								*/

								var nextBitCode = bitStreamManager.ReadBit();
								if (!nextBitCode) // last bit is false (0). 
								{
									//last read bit is false (0). So now we have a code 10 (1 previous bit and 0 from this one).
									//10: Read a new palette index from the bit stream, i.e., read the number of bits that the parameter 
									//    specifies as a value (see the Tiny Bits of Decompression chapter).
									//    Set the subtraction variable to 1.
									//    Draw the next pixel.
									paletteIndex = bitStreamManager.ReadValue(numBitPerPaletteEntry);

								}
								else // last bit is true (1). 
								{
									//11: Read the next 3 bit value, and perform an action, depending on the value:
									byte nextValue = bitStreamManager.ReadValue(3);
									switch (nextValue)
									{
										case 0: //000: Decrease current palette index by 4.
											paletteIndex -= 4;
											break;
										case 1: //001: Decrease current palette index by 3.
											paletteIndex -= 3;
											break;
										case 2: //010: Decrease current palette index by 2.
											paletteIndex -= 2;
											break;
										case 3: //011: Decrease current palette index by 1.
											paletteIndex -= 1;
											break;
										case 4: //100: Read next 8 bits. 
														//Draw the number of pixels specified by these 8 bits with the current palette index (somewhat similar to RLE).
											var numPixels = bitStreamManager.ReadByte();
											for (int i = 0; i < numPixels; i++)
											{
												//if (!((_currentColumn == 7 && _currentLine == (_height - 1))))
												{
													if (renderingDirection == RenderingDirection.Horizontal)
													{
														currentColumn++;
														if (currentColumn == 8)
														{
															currentColumn = 0;
															currentLine++;
														}
													}
													else
													{
														currentLine++;
														if (currentLine == height)
														{
															currentLine = 0;
															currentColumn++;
														}
													}
													if (currentLine < height && currentOffset + currentColumn < width)
													{
														imageData[currentLine][currentOffset + currentColumn] = (byte)paletteIndex;
													}
												}
											}
											break;
										case 5: //101: Increase current palette index by 1.
											paletteIndex += 1;
											break;
										case 6: //110: Increase current palette index by 2.
											paletteIndex += 2;
											break;
										case 7: //111: Increase current palette index by 3. 
											paletteIndex += 3;
											break;
										default:
											Debugger.Break();
											break;
									}
								}
								if (renderingDirection == RenderingDirection.Horizontal)
								{
									currentColumn++;
									if (currentColumn == 8)
									{
										currentColumn = 0;
										currentLine++;
									}
								}
								else
								{
									currentLine++;
									if (currentLine == height)
									{
										currentLine = 0;
										currentColumn++;
									}
								}
								if (currentLine < height && currentOffset + currentColumn < width)
								{
									imageData[currentLine][currentOffset + currentColumn] = (byte)paletteIndex;
								}
							}
						}
						else
						{
							if (renderingDirection == RenderingDirection.Horizontal)
							{
								currentColumn++;
								if (currentColumn == 8)
								{
									currentColumn = 0;
									currentLine++;
								}
							}
							else
							{
								currentLine++;
								if (currentLine == height)
								{
									currentLine = 0;
									currentColumn++;
								}
							}
							if (currentLine < height && currentOffset + currentColumn < width)
							{
								imageData[currentLine][currentOffset + currentColumn] = (byte)paletteIndex;
							}
						}

						if (currentColumn == 7 && currentLine == height - 1 || bitStreamManager.EndOfStream) finishDecode = true;
					}
				}
				else
				{
					var bitStreamManager = new BitStreamManager(strip.Data);
					bool finishDecode = false;
					paletteIndex = bitStreamManager.ReadByte();
					imageData[0][currentOffset] = (byte)paletteIndex;
					while (!finishDecode)
					{
						paletteIndex = bitStreamManager.ReadByte();
						if (renderingDirection == RenderingDirection.Horizontal)
						{
							currentColumn++;
							if (currentColumn == 8)
							{
								currentColumn = 0;
								currentLine++;
							}
						}
						else
						{
							currentLine++;
							if (currentLine == height)
							{
								currentLine = 0;
								currentColumn++;
							}
						}
						imageData[currentLine][currentOffset + currentColumn] = (byte)paletteIndex;
						if (currentColumn == 7 && currentLine == height - 1 || bitStreamManager.EndOfStream) finishDecode = true;
					}
				}
			}
			return imageData.SelectMany(x => x).ToArray();
			//File.WriteAllBytes($@"C:\Dev\Gaming\PC_DOS\Extractions\IndyFOA\output\Room_Backgrounds\{roomFile.Room.RoomNumber}_{roomFile.Room.RoomName}.bin", imageData.SelectMany(x => x).ToArray());
		}

		private byte[] DecodeCOSTImage(CostumeImageData pictureData, Costume costume, int width, int height, List<Color> roomPalette)
		{
			if (pictureData.ImageData == null || pictureData.ImageData.Length <= 1) return Array.Empty<byte>();
			var bitStreamManager = new BitStreamManager(pictureData.ImageData);
			var colorSize = costume.PaletteSize == 16 ? 4 : 5;
			var repetitionCountSize = costume.PaletteSize == 16 ? 4 : 3;
			var imageData = new byte[width * height];

			bool finishDecode = false;
			var currentLine = 0;
			var currentColumn = 0;
			while (!finishDecode)
			{
				var repetitionCount = bitStreamManager.ReadValue(repetitionCountSize);
				var colorIndex = bitStreamManager.ReadValue(colorSize);

				if (repetitionCount == 0 && bitStreamManager.Position != bitStreamManager.Length)
				{
					repetitionCount = bitStreamManager.ReadByte();
				}

				for (int i = 0; i < repetitionCount; i++)
				{
					imageData[currentLine * width + currentColumn] = (byte)((colorIndex == 0) ? 0 : costume.Palette[colorIndex]);
					currentLine++;
					if (currentLine == height)
					{
						currentLine = 0;
						currentColumn++;
					}
				}
				if ((currentColumn == width && currentLine == 0) || bitStreamManager.EndOfStream) finishDecode = true;
			}
			return imageData;
		}
	}

	public class ObjectImage
	{

	}

	public class ObjectImageHeader
	{
		public ushort Id { get; set; }
		public ushort ImageCount { get; set; }
		public ushort ZPlaneCount { get; set; }
		public ushort Unknown { get; set; }
		public ushort X { get; set; }
		public ushort Y { get; set; }
		public ushort Width { get; set; }
		public ushort Height { get; set; }
	}

	public class ImageStrip
	{
		public UInt32 Offset { get; set; }
		private byte _compressionId { get; set; }
		public byte CompressionId
		{
			get { return _compressionId; }
			set
			{
				_compressionId = value;
				SetCompressionInformation();
			}
		}

		public byte[] Data { get; set; }
		public CompressionType CompressionType { get; private set; }
		public RenderingDirection RenderingDirection { get; private set; }
		public bool Transparent { get; private set; }
		public int ParamSubtraction { get; private set; }

		private void SetCompressionInformation()
		{
			if (CompressionId == 0x01)
			{
				CompressionType = CompressionType.Uncompressed;
				RenderingDirection = RenderingDirection.Horizontal;
				Transparent = false;
				ParamSubtraction = 0;
			}
			else if (CompressionId >= 0x0E && CompressionId <= 0x12)
			{
				CompressionType = CompressionType.Method1;
				RenderingDirection = RenderingDirection.Vertical;
				Transparent = false;
				ParamSubtraction = 0x0A;
			}
			else if (CompressionId >= 0x18 && CompressionId <= 0x1C)
			{
				CompressionType = CompressionType.Method1;
				RenderingDirection = RenderingDirection.Horizontal;
				Transparent = false;
				ParamSubtraction = 0x14;
			}
			else if (CompressionId >= 0x22 && CompressionId <= 0x26)
			{
				CompressionType = CompressionType.Method1;
				RenderingDirection = RenderingDirection.Vertical;
				Transparent = true;
				ParamSubtraction = 0x1E;
			}
			else if (CompressionId >= 0x2C && CompressionId <= 0x30)
			{
				CompressionType = CompressionType.Method1;
				RenderingDirection = RenderingDirection.Horizontal;
				Transparent = true;
				ParamSubtraction = 0x28;
			}
			else if (CompressionId >= 0x40 && CompressionId <= 0x44)
			{
				//Debugger.Break();
				CompressionType = CompressionType.Method2;
				RenderingDirection = RenderingDirection.Horizontal;
				Transparent = false;
				ParamSubtraction = 0x3C;
			}
			else if (CompressionId >= 0x54 && CompressionId <= 0x58)
			{
				//Debugger.Break();
				CompressionType = CompressionType.Method2;
				RenderingDirection = RenderingDirection.Horizontal;
				Transparent = true;
				ParamSubtraction = 0x50;
			}
			else if (CompressionId >= 0x68 && CompressionId <= 0x6C)
			{
				CompressionType = CompressionType.Method2;
				RenderingDirection = RenderingDirection.Horizontal;
				Transparent = true;
				ParamSubtraction = 0x64;
			}
			else if (CompressionId >= 0x7C && CompressionId <= 0x80)
			{
				CompressionType = CompressionType.Method2;
				RenderingDirection = RenderingDirection.Horizontal;
				Transparent = false;
				ParamSubtraction = 0x78;
			}
			else
			{
				CompressionType = CompressionType.Unknown;
				RenderingDirection = RenderingDirection.Unknown;
				Transparent = false;
				ParamSubtraction = -2;
			}
		}
	}

	public enum CompressionType
	{
		Uncompressed = 0,
		Method1 = 1,
		Method2 = 2,
		Unknown = 3
	}

	public enum RenderingDirection
	{
		Horizontal = 0,
		Vertical = 1,
		Unknown = 3
	}
	public class ScummIndexFile
	{
		public List<Room> Rooms { get; set; }
		public List<CostumeOffset> Costumes { get; set; }

		public ScummIndexFile(string path)
		{
			Rooms = new List<Room>();
			Costumes = new List<CostumeOffset>();
			ParseIndexFile(path);
		}

		private void ParseIndexFile(string path)
		{
			using (var reader = new BinaryReader(File.OpenRead(path)))
			{
				// Error if first 4 bytes are not RNAM
				var magicBytes = reader.ReadBytes(4);
				if (Encoding.ASCII.GetString(magicBytes) != "RNAM")
				{
					throw new Exception("Invalid index file");
				}
				// Get the Length of the RNAM block
				var length = reader.ReadBigEndianUInt32() - 9;
				// each room entry is 10 bytes, 1 for the Room number, 9 for the name
				var numRooms = length / 10;

				for (int i = 0; i < numRooms; i++)
				{
					var room = new Room();
					room.RoomNumber = reader.ReadByte();
					var roomNameData = reader.ReadBytes(9).Select(x => (byte)(x ^ 0xFF)).ToArray();
					room.RoomName = Encoding.ASCII.GetString(roomNameData).TrimEnd('\0');
					Rooms.Add(room);
				}
				reader.ReadByte(); // skip the last byte

				// TODO Parse intervening BLOCKS
				// for now, skip to 0xECC
				reader.BaseStream.Seek(0x1441, SeekOrigin.Begin);
				var count = reader.ReadUInt16();
				for (int i = 0; i < count; i++)
				{
					var costume = new CostumeOffset
					{
						RoomNumber = reader.ReadByte()
					};
					Costumes.Add(costume);
				}
				for (int i = 0; i < count; i++)
				{
					Costumes[i].Offset = reader.ReadUInt32();
				}
			}
		}
	}

	public class RoomHeader
	{

		public ushort Width { get; set; }
		public ushort Height { get; set; }
		public ushort NumObjects { get; set; }

		public RoomHeader(byte[] data)
		{
			Width = BitConverter.ToUInt16(data, 0);
			Height = BitConverter.ToUInt16(data, 2);
			NumObjects = BitConverter.ToUInt16(data, 4);
		}
	}

	public class RoomFile
	{
		public RoomHeader Header { get; set; }
		public Room Room { get; set; }
		public byte TransparencyIndex { get; set; }
		public List<Color> Palette { get; set; }
		public byte ZBufferCount { get; set; }
		public List<ObjectImage> ObjectImages { get; set; }
		public List<Costume> Costumes { get; set; }
	}

	public class RoomOffsetTable
	{
		public List<Room> RoomOffsets { get; set; }
		public uint NumOfRooms => (uint)RoomOffsets.Count;

		public RoomOffsetTable()
		{
			RoomOffsets = new List<Room>();
		}
	}

	public class Room
	{
		public byte RoomNumber { get; set; }
		public string RoomName { get; set; }
		public uint Offset { get; set; }
	}

	public class CostumeOffset
	{
		public byte RoomNumber { get; set; }
		public uint Offset { get; set; }
	}

	public enum BlockType
	{
		LECF,
		LOFF,
		LFLF,
		ROOM,
		RMHD,
		CYCL,
		TRNS,
		EPAL,
		BOXD,
		BOXM,
		CLUT,
		SCAL,
		RMIM,
		RMIH,
		SMAP,
		OBIM,
		IMHD,
		SOUN,
		COST,
		OBCD,
		CDHD,
		VERB,
		OBNA,
		LSCR
	}
}

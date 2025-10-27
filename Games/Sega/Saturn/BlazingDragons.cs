using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;

namespace ExtractCLUT.Games.Sega.Saturn
{
    public static class BlazingDragons
    {
        public static List<Color> ReadPaletteFile(string filePath)
        {

            var fileBytes = File.ReadAllBytes(filePath);
            return ColorHelper.ReadRgb15Palette(fileBytes);
        }

        public static List<Image> ParseActorFile(string filePath)
        {
            var images = new List<Image>();
            using var aReader = new BinaryReader(File.OpenRead(filePath));
            // Read the actor file and extract images
            aReader.BaseStream.Seek(8, SeekOrigin.Begin);
            var frameListOffset = aReader.ReadBigEndianUInt16();
            var paletteOffset = aReader.ReadBigEndianUInt16();
            aReader.BaseStream.Seek(frameListOffset, SeekOrigin.Begin);
            var frameOffset = aReader.ReadBigEndianUInt16();
            aReader.BaseStream.Seek(frameOffset, SeekOrigin.Begin);
            var frames = new List<ActorFrame>();
            while (aReader.BaseStream.Position < paletteOffset)
            {
                var frame = new ActorFrame
                {
                    xOffset = aReader.ReadBigEndianInt16(),
                    yOffset = aReader.ReadBigEndianInt16(),
                    height = aReader.ReadByte(),
                    width = (ushort)(aReader.ReadByte() * 2),
                    dataOffset = aReader.ReadBigEndianUInt32(),
                    flags = aReader.ReadBigEndianUInt16(),
                    unk = aReader.ReadBigEndianUInt16()
                };
                frames.Add(frame);
                if (frame.unk == 0x1) aReader.ReadBytes(4);
            }

            var canvasWidth = 320;
            var canvasHeight = 200;

            var palLength = frames[0].dataOffset - paletteOffset - 4;
            aReader.BaseStream.Seek(paletteOffset, SeekOrigin.Begin);
            var palData = aReader.ReadBytes((int)palLength);
            var palette = ColorHelper.ReadRgb15Palette(palData);
            foreach (var (frame, index) in frames.WithIndex())
            {
                try
                {

                    aReader.BaseStream.Seek(frame.dataOffset, SeekOrigin.Begin);
                    // var nextOffset = (index < frames.Count - 1) ? frames[index + 1].dataOffset : aReader.BaseStream.Length;
                    // var imageLength = nextOffset - frame.dataOffset;
                    var imageData = aReader.ReadBytes((int)frame.width * frame.height);
                    frame.imageData = imageData;
                    var spriteImage = ImageFormatHelper.DragonActorImage(imageData, frame.width, frame.height, palette);

                    // // Create a new bitmap for the full frame and draw the sprite on it
                    // var frameImage = new Bitmap(canvasWidth, canvasHeight);
                    // using (var g = Graphics.FromImage(frameImage))
                    // {
                    //     // Ensure the background is transparent
                    //     g.Clear(Color.Transparent);
                    //     // Draw the sprite at its correct offset
                    //     g.DrawImage(spriteImage, new Point(160 + frame.xOffset, 100 - frame.yOffset));
                    // }
                    // spriteImage.Dispose(); // Dispose the original sprite image
                    images.Add(spriteImage);
                }
                catch (Exception ex)
                {
                    //Debug.WriteLine($"Error processing frame {index} in actor file {filePath}: {ex.Message}");
                }
            }

            return images;
        }
    }

    public class ActorFrame
    {
        public short xOffset;
        public short yOffset;
        public ushort width;
        public ushort height;
        public uint dataOffset;
        public byte[]? imageData;
        public ushort flags;
        public ushort unk;
    }
}

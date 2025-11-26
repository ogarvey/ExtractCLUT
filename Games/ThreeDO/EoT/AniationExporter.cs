using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;

namespace ExtractCLUT.Games.ThreeDO.EoT
{
    /// <summary>
    /// Handles loading and exporting animations from Eye of Typhoon ACT/IDX files.
    /// </summary>
    public class AnimationExporter
    {
        /// <summary>
        /// Represents a single animation frame with positioning data.
        /// </summary>
        public class AnimationFrame
        {
            /// <summary>Sprite index in CHA file</summary>
            public ushort SpriteIndex { get; set; }

            /// <summary>Frame delay in ticks</summary>
            public ushort Delay { get; set; }

            /// <summary>X position offset (pixels)</summary>
            public short XOffset { get; set; }

            /// <summary>Y position offset (pixels)</summary>
            public short YOffset { get; set; }

            /// <summary>The actual sprite data for this frame</summary>
            public SpriteData? Sprite { get; set; }
        }

        /// <summary>
        /// Represents a complete animation sequence.
        /// </summary>
        public class Animation
        {
            /// <summary>Animation index (0-255)</summary>
            public int Index { get; set; }

            /// <summary>Array of frames in this animation</summary>
            public AnimationFrame[] Frames { get; set; } = [];

            /// <summary>
            /// Gets the bounding box that contains all frames of this animation.
            /// </summary>
            public (int minX, int minY, int maxX, int maxY) GetBoundingBox()
            {
                if (Frames == null || Frames.Length == 0)
                    return (0, 0, 0, 0);

                int minX = int.MaxValue, minY = int.MaxValue;
                int maxX = int.MinValue, maxY = int.MinValue;

                foreach (var frame in Frames)
                {
                    if (frame.Sprite == null) continue;

                    int frameMinX = frame.XOffset;
                    int frameMinY = frame.YOffset;
                    int frameMaxX = frame.XOffset + frame.Sprite.Height;
                    int frameMaxY = frame.YOffset + frame.Sprite.Width;

                    minX = Math.Min(minX, frameMinX);
                    minY = Math.Min(minY, frameMinY);
                    maxX = Math.Max(maxX, frameMaxX);
                    maxY = Math.Max(maxY, frameMaxY);
                }

                return (minX, minY, maxX, maxY);
            }
        }

        /// <summary>
        /// Loads an animation from IDX and ACT files.
        /// </summary>
        /// <param name="animationIndex">Animation index (0-255)</param>
        /// <param name="idxData">Complete IDX file data</param>
        /// <param name="actData">Complete ACT file data</param>
        /// <param name="sprites">Array of loaded sprites from CHA file</param>
        /// <returns>Animation with all frames</returns>
        public static Animation LoadAnimation(int animationIndex, byte[] idxData, byte[] actData, SpriteData[] sprites)
        {
            if (animationIndex < 0 || animationIndex >= 256)
                throw new ArgumentOutOfRangeException(nameof(animationIndex), "Animation index must be 0-255");

            if (idxData == null || actData == null || sprites == null)
                throw new ArgumentNullException("File data cannot be null");

            // Read IDX entry (5 bytes per entry)
            int idxOffset = animationIndex * 5;
            if (idxOffset + 5 > idxData.Length)
                throw new ArgumentException("IDX file too small");

            using (var reader = new BinaryReader(new MemoryStream(idxData, idxOffset, 5)))
            {
                uint actOffset = reader.ReadUInt32();  // Offset into ACT file
                byte frameCount = reader.ReadByte();    // Number of frames

                if (frameCount == 0)
                    return new Animation { Index = animationIndex, Frames = new AnimationFrame[0] };

                // Read frames from ACT file
                var frames = new AnimationFrame[frameCount];

                for (int i = 0; i < frameCount; i++)
                {
                    int frameOffset = (int)(actOffset + (i * 40));  // Each frame is 40 bytes

                    if (frameOffset + 40 > actData.Length)
                        break;

                    frames[i] = ReadFrame(actData, frameOffset, sprites);
                }

                return new Animation
                {
                    Index = animationIndex,
                    Frames = frames
                };
            }
        }

        /// <summary>
        /// Reads a single animation frame from ACT data.
        /// </summary>
        private static AnimationFrame ReadFrame(byte[] actData, int offset, SpriteData[] sprites)
        {
            using (var reader = new BinaryReader(new MemoryStream(actData, offset, 40)))
            {
                ushort spriteIndex = reader.ReadUInt16();   // Offset 0x00: Sprite index
                ushort delay = reader.ReadUInt16();              // Offset 0x02: Frame delay

                short yOffset = reader.ReadInt16();       // Offset 0x04: X offset (fixed-point)
                short xOffset = reader.ReadInt16();       // Offset 0x08: Y offset (fixed-point)

                // Fixed-point to pixels: high word (upper 16 bits) is the pixel offset

                // Get the sprite data
                SpriteData? sprite = null;
                if (spriteIndex < sprites.Length)
                    sprite = sprites[spriteIndex];

                return new AnimationFrame
                {
                    SpriteIndex = spriteIndex,
                    Delay = delay,
                    XOffset = xOffset,
                    YOffset = yOffset,
                    Sprite = sprite
                };
            }
        }

        /// <summary>
        /// Exports an animation as a sprite sheet with all frames aligned properly.
        /// </summary>
        /// <param name="animation">The animation to export</param>
        /// <param name="outputPath">Output file path (raw 8-bit indexed)</param>
        /// <param name="transparentIndex">Palette index to use for transparent pixels (default 0)</param>
        /// <returns>Tuple of (width, height) of the exported sprite sheet</returns>
        public static (int width, int height) ExportAnimationSpriteSheet(Animation animation, string outputPath, byte transparentIndex = 0)
        {
            if (animation == null || animation.Frames == null || animation.Frames.Length == 0)
                throw new ArgumentException("Animation has no frames");

            // Get bounding box to determine canvas size for each frame
            var (minX, minY, maxX, maxY) = animation.GetBoundingBox();
            int frameWidth = maxX - minX;
            int frameHeight = maxY - minY;

            if (frameWidth <= 0 || frameHeight <= 0)
                throw new ArgumentException("Invalid frame dimensions");

            // Create sprite sheet: frames arranged horizontally
            int sheetWidth = frameWidth * animation.Frames.Length;
            int sheetHeight = frameHeight;
            byte[] spriteSheet = new byte[sheetWidth * sheetHeight];

            // Fill with transparent color
            for (int i = 0; i < spriteSheet.Length; i++)
                spriteSheet[i] = transparentIndex;

            // Render each frame
            for (int frameIdx = 0; frameIdx < animation.Frames.Length; frameIdx++)
            {
                var frame = animation.Frames[frameIdx];
                if (frame.Sprite == null) continue;

                // Calculate position in sprite sheet
                int sheetX = frameIdx * frameWidth;

                // Calculate sprite position relative to frame bounding box
                int spriteX = frame.XOffset - minX;
                int spriteY = frame.YOffset - minY;

                // Copy sprite pixels to sprite sheet
                for (int y = 0; y < frame.Sprite.Height; y++)
                {
                    for (int x = 0; x < frame.Sprite.Width; x++)
                    {
                        int srcIdx = y * frame.Sprite.Width + x;
                        int dstX = sheetX + spriteX + x;
                        int dstY = spriteY + y;

                        if (dstX >= 0 && dstX < sheetWidth && dstY >= 0 && dstY < sheetHeight)
                        {
                            int dstIdx = dstY * sheetWidth + dstX;
                            byte pixel = frame.Sprite.Pixels[srcIdx];

                            // Only draw non-transparent pixels
                            if (pixel != 0)
                                spriteSheet[dstIdx] = pixel;
                        }
                    }
                }
            }

            // Save the sprite sheet
            File.WriteAllBytes(outputPath, spriteSheet);

            return (sheetWidth, sheetHeight);
        }

        /// <summary>
        /// Exports an animation as individual frames, each properly positioned.
        /// </summary>
        /// <param name="animation">The animation to export</param>
        /// <param name="outputDir">Output directory for frames</param>
        /// <param name="filePrefix">Prefix for output files</param>
        /// <param name="transparentIndex">Palette index to use for transparent pixels (default 0)</param>
        /// <returns>Tuple of (frameWidth, frameHeight) - the size each frame was exported as</returns>
        public static (int width, int height) ExportAnimationFrames(Animation animation, string outputDir, string filePrefix, byte transparentIndex = 0, List<Color>? palette = null)
        {
            if (animation == null || animation.Frames == null || animation.Frames.Length == 0)
                throw new ArgumentException("Animation has no frames");

            Directory.CreateDirectory(outputDir);

            // Get bounding box
            var (minX, minY, maxX, maxY) = animation.GetBoundingBox();
            int frameWidth = maxY - minY;
            int frameHeight = maxX - minX;

            if (frameWidth <= 0 || frameHeight <= 0)
                throw new ArgumentException("Invalid frame dimensions");

            // Export each frame
            for (int frameIdx = 0; frameIdx < animation.Frames.Length; frameIdx++)
            {
                var frame = animation.Frames[frameIdx];

                // Create frame canvas
                byte[] frameData = new byte[frameWidth * frameHeight];
                for (int i = 0; i < frameData.Length; i++)
                    frameData[i] = transparentIndex;

                if (frame.Sprite != null)
                {
                    // Calculate sprite position relative to frame bounding box
                    int spriteY = frame.XOffset - minX;
                    int spriteX = frame.YOffset - minY;
                    var spriteData = frame.Sprite.GetPixels();
                    // Copy sprite pixels
                    for (int y = 0; y < frame.Sprite.Width; y++)
                    {
                        for (int x = 0; x < frame.Sprite.Height; x++)
                        {
                            int srcIdx = y * frame.Sprite.Height + x;
                            int dstY = spriteX + x;
                            int dstX = spriteY + y;

                            if (dstX >= 0 && dstX < frameHeight && dstY >= 0 && dstY < frameWidth)
                            {
                                int dstIdx = dstY * frameHeight + dstX;
                                byte pixel = spriteData[srcIdx];

                                if (pixel != 0)
                                    frameData[dstIdx] = pixel;
                            }
                        }
                    }
                }
                // Save frame
                if (palette != null)
                {
                    var image = ImageFormatHelper.GenerateIMClutImage(palette, frameData, frameHeight, frameWidth, true);
                    var outputPath = Path.Combine(outputDir, $"{filePrefix}_frame_{frameIdx:D3}.png");
                    image.Save(outputPath);
                }
                else
                {
                    var outputPath = Path.Combine(outputDir, $"{filePrefix}_frame_{frameIdx:D3}.raw");
                    File.WriteAllBytes(outputPath, frameData);
                }
            }

            return (frameWidth, frameHeight);
        }

        public static Animation[] LoadAllAnimations(byte[] idxData, byte[] actData, SpriteData[] sprites)
        {
            var animations = new Animation[256];

            for (int i = 0; i < 256; i++)
            {
                try
                {
                    animations[i] = LoadAnimation(i, idxData, actData, sprites);
                }
                catch
                {
                    // Skip invalid animations
                    animations[i] = new Animation { Index = i, Frames = new AnimationFrame[0] };
                }
            }

            return animations;
        }
    }
}

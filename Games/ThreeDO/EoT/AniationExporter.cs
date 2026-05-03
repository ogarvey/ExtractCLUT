using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtractCLUT.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

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
            public byte Delay { get; set; }

            /// <summary>
            /// Raw ACT flag byte. Low 2 bits control sprite orientation; bit 0 also affects X anchoring.
            /// </summary>
            public byte Flags { get; set; }

            /// <summary>
            /// Signed X anchor offset from the ACT record.
            /// This is not a standalone absolute screen position.
            /// </summary>
            public int XOffset { get; set; }

            /// <summary>
            /// Signed Y anchor offset from the ACT record.
            /// This is not a standalone absolute screen position.
            /// </summary>
            public int YOffset { get; set; }

            /// <summary>
            /// Unconfirmed ACT tail bytes (offsets 0x08..0x27).
            /// Preserved for inspection even though their semantics are not yet verified.
            /// </summary>
            public byte[] UnknownData { get; set; }

            /// <summary>The actual sprite data for this frame</summary>
            public SpriteData Sprite { get; set; }

            /// <summary>
            /// Computes the frame placement relative to an anchor at (0,0), following the game's draw logic.
            /// </summary>
            /// <param name="facing">
            /// Facing flag used by the game when XOR'ing against the low 2 bits of <see cref="Flags"/>.
            /// Use 0 for the default orientation.
            /// </param>
            public FramePlacement GetPlacement(int facing = 0)
            {
                return GetPlacement(Sprite?.Width ?? 0, Sprite?.Height ?? 0, facing);
            }

            /// <summary>
            /// Computes the frame placement relative to an anchor at (0,0), following the game's draw logic.
            /// </summary>
            /// <param name="facing">
            /// Facing flag used by the game when XOR'ing against the low 2 bits of <see cref="Flags"/>.
            /// Use 0 for the default orientation.
            /// </param>
            public FramePlacement GetPlacement(int sourceWidth, int sourceHeight, int facing = 0)
            {
                int orientation = (Flags & 0x03) ^ (facing & 0x01);
                bool mirrorX = (orientation & 0x01) != 0;
                bool flipY = (orientation & 0x02) != 0;
                bool alternateXAnchor = (Flags & 0x01) != 0;

                int left = (mirrorX == alternateXAnchor)
                    ? -XOffset
                    : XOffset - sourceWidth;

                int top = -YOffset;

                return new FramePlacement
                {
                    Left = left,
                    Top = top,
                    Width = sourceWidth,
                    Height = sourceHeight,
                    MirrorX = mirrorX,
                    FlipY = flipY,
                    AlternateXAnchor = alternateXAnchor
                };
            }
        }

        /// <summary>
        /// Placement/orientation information derived from one ACT frame.
        /// </summary>
        public class FramePlacement
        {
            public int Left { get; set; }
            public int Top { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public bool MirrorX { get; set; }
            public bool FlipY { get; set; }
            public bool AlternateXAnchor { get; set; }

            public int Right => Left + Width;
            public int Bottom => Top + Height;
        }

        /// <summary>
        /// Represents a complete animation sequence.
        /// </summary>
        public class Animation
        {
            /// <summary>Animation index (0-255)</summary>
            public int Index { get; set; }

            /// <summary>Array of frames in this animation</summary>
            public AnimationFrame[] Frames { get; set; }

            /// <summary>
            /// Gets the bounding box that contains all frames of this animation.
            /// </summary>
            public (int minX, int minY, int maxX, int maxY) GetBoundingBox(int facing = 0)
            {
                if (Frames == null || Frames.Length == 0)
                    return (0, 0, 0, 0);

                int minX = int.MaxValue, minY = int.MaxValue;
                int maxX = int.MinValue, maxY = int.MinValue;
                bool hasRenderableFrame = false;

                foreach (var frame in Frames)
                {
                    if (frame?.Sprite == null)
                        continue;

                    hasRenderableFrame = true;

                    FramePlacement placement = frame.GetPlacement(facing);

                    int frameMinX = placement.Left;
                    int frameMinY = placement.Top;
                    int frameMaxX = placement.Right;
                    int frameMaxY = placement.Bottom;

                    minX = Math.Min(minX, frameMinX);
                    minY = Math.Min(minY, frameMinY);
                    maxX = Math.Max(maxX, frameMaxX);
                    maxY = Math.Max(maxY, frameMaxY);
                }

                if (!hasRenderableFrame)
                    return (0, 0, 0, 0);

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
                var frames = new List<AnimationFrame>(frameCount);

                for (int i = 0; i < frameCount; i++)
                {
                    int frameOffset = (int)(actOffset + (i * 40));  // Each frame is 40 bytes

                    if (frameOffset + 40 > actData.Length)
                        break;

                    frames.Add(ReadFrame(actData, frameOffset, sprites));
                }

                return new Animation
                {
                    Index = animationIndex,
                    Frames = frames.ToArray()
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
                byte delay = reader.ReadByte();              // Offset 0x02: Frame delay
                byte flags = reader.ReadByte();              // Offset 0x03: Orientation / anchoring flags

                // Offsets are signed anchor offsets, not standalone absolute positions.
                short xOffset = reader.ReadInt16();          // Offset 0x04: X anchor offset
                short yOffset = reader.ReadInt16();          // Offset 0x06: Y anchor offset
                byte[] unknownData = reader.ReadBytes(0x20); // Offset 0x08..0x27: currently unconfirmed

                // Get the sprite data
                SpriteData sprite = null;
                if (spriteIndex < sprites.Length)
                    sprite = sprites[spriteIndex];

                return new AnimationFrame
                {
                    SpriteIndex = spriteIndex,
                    Delay = delay,
                    Flags = flags,
                    XOffset = xOffset,
                    YOffset = yOffset,
                    UnknownData = unknownData,
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
        public static (int width, int height) ExportAnimationSpriteSheet(Animation animation, string outputPath, byte transparentIndex = 0, int facing = 0)
        {
            if (animation == null || animation.Frames == null || animation.Frames.Length == 0)
                throw new ArgumentException("Animation has no frames");

            // Get bounding box to determine canvas size for each frame
            var (minX, minY, maxX, maxY) = animation.GetBoundingBox(facing);
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

                RenderFrame(ref spriteSheet, sheetWidth, sheetHeight, frame, sheetX - minX, -minY, transparentIndex, facing);
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
        public static (int width, int height) ExportAnimationFrames(Animation animation, string outputDir, string filePrefix, byte transparentIndex = 0, int facing = 0)
        {
            if (animation == null || animation.Frames == null || animation.Frames.Length == 0)
                throw new ArgumentException("Animation has no frames");

            Directory.CreateDirectory(outputDir);

            // Get bounding box
            var (minX, minY, maxX, maxY) = animation.GetBoundingBox(facing);
            int frameWidth = maxX - minX;
            int frameHeight = maxY - minY;

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

                RenderFrame(ref frameData, frameWidth, frameHeight, frame, -minX, -minY, transparentIndex, facing);

                // Save frame
                string filename = Path.Combine(outputDir, $"{filePrefix}_frame_{frameIdx:D3}_delay{frame.Delay}.raw");
                File.WriteAllBytes(filename, frameData);
            }

            return (frameWidth, frameHeight);
        }

        /// <summary>
        /// Loads all animations from IDX and ACT files.
        /// </summary>
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

        /// <summary>
        /// Loads and caches source sprite images for the frames used by an animation.
        /// </summary>
        private static DisposableSpriteImageCache LoadSpriteImageCache(
            Animation animation,
            string spriteImageDir,
            string spriteFileNameFormat)
        {
            if (string.IsNullOrWhiteSpace(spriteImageDir))
                throw new ArgumentException("Sprite image directory is required", nameof(spriteImageDir));

            var cache = new DisposableSpriteImageCache();

            foreach (var frame in animation.Frames)
            {
                if (frame == null || cache.ContainsKey(frame.SpriteIndex))
                    continue;

                string fileName = string.Format(spriteFileNameFormat, frame.SpriteIndex);
                string filePath = Path.Combine(spriteImageDir, fileName);

                if (!File.Exists(filePath))
                    continue;

                cache[frame.SpriteIndex] = Image.Load<Rgba32>(filePath);
            }

            return cache;
        }

        /// <summary>
        /// Calculates an animation bounding box using external sprite images as the size source.
        /// </summary>
        private static (int minX, int minY, int maxX, int maxY) GetBoundingBoxFromImages(
            Animation animation,
            Dictionary<ushort, Image<Rgba32>> spriteImages,
            int facing)
        {
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            bool hasRenderableFrame = false;

            foreach (var frame in animation.Frames)
            {
                if (frame == null || !spriteImages.TryGetValue(frame.SpriteIndex, out var image))
                    continue;

                hasRenderableFrame = true;
                FramePlacement placement = frame.GetPlacement(image.Width, image.Height, facing);

                minX = Math.Min(minX, placement.Left);
                minY = Math.Min(minY, placement.Top);
                maxX = Math.Max(maxX, placement.Right);
                maxY = Math.Max(maxY, placement.Bottom);
            }

            if (!hasRenderableFrame)
                return (0, 0, 0, 0);

            return (minX, minY, maxX, maxY);
        }
        
        public static (int width, int height) ExportAnimationFramesFromImages(
                   Animation animation,
                   string spriteImageDir,
                   string outputDir,
                   string filePrefix,
                   string spriteFileNameFormat = "{0:D4}.png",
                   int facing = 0)
        {
            if (animation == null || animation.Frames == null || animation.Frames.Length == 0)
                throw new ArgumentException("Animation has no frames");

            Directory.CreateDirectory(outputDir);

            using var spriteImages = LoadSpriteImageCache(animation, spriteImageDir, spriteFileNameFormat);
            var (minX, minY, maxX, maxY) = GetBoundingBoxFromImages(animation, spriteImages, facing);

            int frameWidth = maxX - minX;
            int frameHeight = maxY - minY;

            if (frameWidth <= 0 || frameHeight <= 0)
                throw new ArgumentException("Animation has no renderable sprite images");

            for (int frameIdx = 0; frameIdx < animation.Frames.Length; frameIdx++)
            {
                var frame = animation.Frames[frameIdx];

                using var frameImage = new Image<Rgba32>(frameWidth, frameHeight, Color.Transparent);

                if (frame != null && spriteImages.TryGetValue(frame.SpriteIndex, out var sourceImage))
                    RenderFrameImage(frameImage, frame, sourceImage, -minX, -minY, facing);

                string filename = Path.Combine(outputDir, $"{filePrefix}_frame_{frameIdx:D3}_delay{frame?.Delay ?? 0}.png");
                frameImage.Save(filename);
            }

            return (frameWidth, frameHeight);
        }


        /// <summary>
        /// Draws one frame using a pre-rendered sprite image.
        /// </summary>
        private static void RenderFrameImage(
            Image<Rgba32> canvas,
            AnimationFrame frame,
            Image<Rgba32> sourceImage,
            int originX,
            int originY,
            int facing)
        {
            FramePlacement placement = frame.GetPlacement(sourceImage.Width, sourceImage.Height, facing);

            using var frameImage = sourceImage.Clone(ctx =>
            {
                if (placement.MirrorX)
                    ctx.Flip(FlipMode.Horizontal);

                if (placement.FlipY)
                    ctx.Flip(FlipMode.Vertical);
            });

            canvas.Mutate(ctx => ctx.DrawImage(frameImage, new Point(originX + placement.Left, originY + placement.Top), 1f));
        }
        
        /// <summary>
        /// Disposable sprite image cache.
        /// </summary>
        private sealed class DisposableSpriteImageCache : Dictionary<ushort, Image<Rgba32>>, IDisposable
        {
            public void Dispose()
            {
                foreach (var image in Values)
                    image.Dispose();
            }
        }
        /// <summary>
        /// Renders a single ACT frame onto an indexed destination buffer.
        /// </summary>
        private static void RenderFrame(ref byte[] destination, int canvasWidth, int canvasHeight, AnimationFrame frame,
            int originX, int originY, byte transparentIndex, int facing)
        {
            if (frame?.Sprite == null)
                return;

            FramePlacement placement = frame.GetPlacement(facing);
            int width = frame.Sprite.Width;
            int height = frame.Sprite.Height;
            var pixels = frame.Sprite.GetPixels();

            for (int y = 0; y < height; y++)
            {
                int srcY = placement.FlipY ? (height - 1 - y) : y;

                for (int x = 0; x < width; x++)
                {
                    int srcX = placement.MirrorX ? (width - 1 - x) : x;
                    byte pixel = pixels[srcY * width + srcX];

                    if (pixel == transparentIndex)
                        continue;

                    int dstX = originX + placement.Left + x;
                    int dstY = originY + placement.Top + y;

                    if (dstX < 0 || dstX >= canvasWidth || dstY < 0 || dstY >= canvasHeight)
                        continue;

                    destination[dstY * canvasWidth + dstX] = pixel;
                }
            }
        }
    }

    /// <summary>
    /// Handles loading and exporting animations from Eye of Typhoon ACT/IDX files.
    /// </summary>
    public class AnimationExporterOld
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
                    int frameMaxX = frame.XOffset + frame.Sprite.Width;
                    int frameMaxY = frame.YOffset + frame.Sprite.Height;

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
                
                short xOffset = reader.ReadInt16();          // Offset 0x04: X offset (pixels)
                short yOffset = reader.ReadInt16();          // Offset 0x06: Y offset (pixels)

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
            int frameWidth = maxX - minX;
            int frameHeight = maxY - minY;

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
                    int spriteX = frame.XOffset - minX;
                    int spriteY = frame.YOffset - minY;

                    var spriteData = frame.Sprite.GetPixels();
                    // Copy sprite pixels
                    for (int y = 0; y < frame.Sprite.Height; y++)
                    {
                        for (int x = 0; x < frame.Sprite.Width; x++)
                        {
                            int srcIdx = y * frame.Sprite.Width + x;
                            int dstX = spriteX + x;
                            int dstY = spriteY + y;

                            if (dstX >= 0 && dstX < frameWidth && dstY >= 0 && dstY < frameHeight)
                            {
                                int dstIdx = dstY * frameWidth + dstX;
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

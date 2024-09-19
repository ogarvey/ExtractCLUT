using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC
{
    public static class TexturePacker
    {
        public class FrameData
        {
            public Frame frame { get; set; }
            //public bool rotated { get; set; }
            //public bool trimmed { get; set; }
            public SpriteSourceSize spriteSourceSize { get; set; }
            public SourceSize sourceSize { get; set; }
            public Pivot pivot { get; set; }
        }

        public class Frame
        {
            public int x { get; set; }
            public int y { get; set; }
            public int w { get; set; }
            public int h { get; set; }
        }

        public class SpriteSourceSize
        {
            public int x { get; set; }
            public int y { get; set; }
            public int w { get; set; }
            public int h { get; set; }
        }

        public class SourceSize
        {
            public int w { get; set; }
            public int h { get; set; }
        }

        public class Pivot
        {
            public float x { get; set; }
            public float y { get; set; }
        }

        public class Meta
        {
            public string app { get; set; }
            public string version { get; set; }
            public string image { get; set; }
            public string format { get; set; }
            public Size size { get; set; }
            public string scale { get; set; }
            public string smartupdate { get; set; }
        }

        public class TextureData
        {
            public Dictionary<string, FrameData> frames { get; set; }
            public Meta meta { get; set; }
        }

        // public static void ExtractSprites(string jsonFilePath, string outputDirectory)
        // {
        //     // Read the JSON file
        //     string jsonString = File.ReadAllText(jsonFilePath);

        //     // if last char of string after trimming is 0x07, remove it
        //     if (jsonString[jsonString.Length - 1] == 0x07)
        //     {
        //         jsonString = jsonString.Substring(0, jsonString.Length - 1);
        //     }

        //     // Deserialize the JSON data
        //     var textureData = JsonSerializer.Deserialize<TextureData>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        //     // Load the main image
        //     var mainImagePath = Path.Combine(Path.GetDirectoryName(jsonFilePath), textureData.meta.image);
        //     using (var mainImage = new Bitmap(mainImagePath))
        //     {
        //         // Ensure output directory exists
        //         Directory.CreateDirectory(outputDirectory);

        //         foreach (var frameEntry in textureData.frames)
        //         {
        //             string spriteName = frameEntry.Key;
        //             var frame = frameEntry.Value.frame;

        //             // Extract the frame rectangle
        //             Rectangle sourceRect = new Rectangle(frame.x, frame.y, frame.w, frame.h);

        //             // Create a new bitmap for the sprite
        //             using (var spriteImage = new Bitmap(sourceRect.Width, sourceRect.Height))
        //             {
        //                 using (var graphics = Graphics.FromImage(spriteImage))
        //                 {
        //                     // Draw the sprite from the main image
        //                     graphics.DrawImage(mainImage, new Rectangle(0, 0, sourceRect.Width, sourceRect.Height), sourceRect, GraphicsUnit.Pixel);
        //                 }

        //                 // Save the sprite image
        //                 string spritePath = Path.Combine(outputDirectory, $"{spriteName}.png");
        //                 spriteImage.Save(spritePath, System.Drawing.Imaging.ImageFormat.Png);
        //             }
        //         }
        //     }
        // }
        public static void ExtractSprites(string jsonFilePath, string outputDirectory)
        {
            // Read the JSON file
            string jsonString = File.ReadAllText(jsonFilePath);
            // if last char of string after trimming is 0x07, remove it
            if (jsonString[jsonString.Length - 1] == 0x07)
            {
                jsonString = jsonString.Substring(0, jsonString.Length - 1);
            }
            // Deserialize the JSON data
            var textureData = JsonSerializer.Deserialize<TextureData>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (textureData == null || textureData.meta == null || textureData.frames == null)
            {
                return;
            }
            // Load the main image
            var mainImagePath = Path.Combine(Path.GetDirectoryName(jsonFilePath), textureData.meta.image);
            if (!File.Exists(mainImagePath))
            {
                return;
            }
            using (var mainImage = new Bitmap(mainImagePath))
            {
                // Ensure output directory exists
                Directory.CreateDirectory(outputDirectory);

                foreach (var frameEntry in textureData.frames)
                {
                    string spriteName = frameEntry.Key;
                    var frame = frameEntry.Value.frame;
                    var spriteSourceSize = frameEntry.Value.spriteSourceSize;
                    var sourceSize = frameEntry.Value.sourceSize;

                    // Create a new bitmap for the sprite with a transparent background
                    using (var spriteImage = new Bitmap(sourceSize.w, sourceSize.h))
                    {
                        using (var graphics = Graphics.FromImage(spriteImage))
                        {
                            graphics.Clear(Color.Transparent);

                            // Draw the sprite from the main image
                            graphics.DrawImage(
                                mainImage,
                                new Rectangle(spriteSourceSize.x, spriteSourceSize.y, spriteSourceSize.w, spriteSourceSize.h),
                                new Rectangle(frame.x, frame.y, frame.w, frame.h),
                                GraphicsUnit.Pixel);
                        }

                        // Save the sprite image
                        string spritePath = Path.Combine(outputDirectory, $"{spriteName}.png");
                        spriteImage.Save(spritePath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
            }
        }
    }
}

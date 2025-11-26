using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ExtractCLUT.Games.ThreeDO.ShadowWarrior
{
    public static class FrameListParser
    {
        /// <summary>
        /// Parses a frame list file and processes images according to the frame data.
        /// </summary>
        /// <param name="frameListPath">Path to the frame list text file</param>
        /// <param name="imageFolder">Folder containing the input images</param>
        public static void ProcessFrames(string frameListPath, string imageFolder)
        {
            // Parse the frame list file
            var frames = ParseFrameList(frameListPath);
            
            // Create output directory
            string outputFolder = Path.Combine(imageFolder, "aligned_output");
            Directory.CreateDirectory(outputFolder);
            
            // Get all image files from the input folder
            var imageFiles = Directory.GetFiles(imageFolder, "*.png")
                .OrderBy(f => f)
                .ToArray();
            
            // Find the base name pattern (everything before _XXX.png)
            string baseName = "";
            if (imageFiles.Length > 0)
            {
                var fileName = Path.GetFileNameWithoutExtension(imageFiles[0]);
                var lastUnderscore = fileName.LastIndexOf('_');
                if (lastUnderscore >= 0)
                {
                    baseName = fileName.Substring(0, lastUnderscore);
                }
            }
            
            // Process each frame
            foreach (var frame in frames)
            {
                ProcessFrame(frame, imageFolder, outputFolder, baseName);
            }
            
            Console.WriteLine($"Processed {frames.Count} frames. Output saved to: {outputFolder}");
        }

        /// <summary>
        /// Parses a moves file and creates animation sequences for each move.
        /// </summary>
        /// <param name="movesFilePath">Path to the moves text file</param>
        /// <param name="imageFolder">Folder containing the input images</param>
        public static void ProcessMoves(string movesFilePath, string imageFolder)
        {
            // Parse the moves file
            var moves = ParseMovesList(movesFilePath);
            
            // Create output directory
            string outputFolder = Path.Combine(imageFolder, "moves_output");
            Directory.CreateDirectory(outputFolder);
            
            // Get all image files from the input folder
            var imageFiles = Directory.GetFiles(imageFolder, "*.png")
                .OrderBy(f => f)
                .ToArray();
            
            // Find the base name pattern (everything before _XXX.png)
            string baseName = "";
            if (imageFiles.Length > 0)
            {
                var fileName = Path.GetFileNameWithoutExtension(imageFiles[0]);
                var lastUnderscore = fileName.LastIndexOf('_');
                if (lastUnderscore >= 0)
                {
                    baseName = fileName.Substring(0, lastUnderscore);
                }
            }
            
            // Process each move
            foreach (var move in moves)
            {
                ProcessMove(move, imageFolder, outputFolder, baseName);
            }
            
            Console.WriteLine($"Processed {moves.Count} moves. Output saved to: {outputFolder}");
        }

        /// <summary>
        /// Processes moves with frame alignment, combining position data from both FRAMES and MOVES files.
        /// </summary>
        /// <param name="framesFilePath">Path to the frames text file</param>
        /// <param name="movesFilePath">Path to the moves text file</param>
        /// <param name="imageFolder">Folder containing the input images</param>
        public static void ProcessMovesWithAlignment(string framesFilePath, string movesFilePath, string imageFolder)
        {
            // Parse both files
            var frames = ParseFrameList(framesFilePath);
            var moves = ParseMovesList(movesFilePath);
            
            // Create a lookup dictionary for frames by their AnimIndex (FrameCode - 100001 = AnimIndex)
            var frameDict = new Dictionary<int, Frame>();
            foreach (var frame in frames)
            {
                int animIndex = frame.FrameCode - 100001;
                frameDict[animIndex] = frame;
            }
            
            // Create output directory
            string outputFolder = Path.Combine(imageFolder, "moves_aligned_output");
            Directory.CreateDirectory(outputFolder);
            
            // Get all image files from the input folder
            var imageFiles = Directory.GetFiles(imageFolder, "*.png")
                .OrderBy(f => f)
                .ToArray();
            
            // Find the base name pattern (everything before _XXX.png)
            string baseName = "";
            if (imageFiles.Length > 0)
            {
                var fileName = Path.GetFileNameWithoutExtension(imageFiles[0]);
                var lastUnderscore = fileName.LastIndexOf('_');
                if (lastUnderscore >= 0)
                {
                    baseName = fileName.Substring(0, lastUnderscore);
                }
            }
            
            // Process each move with alignment
            foreach (var move in moves)
            {
                ProcessMoveWithAlignment(move, frameDict, imageFolder, outputFolder, baseName);
            }
            
            Console.WriteLine($"Processed {moves.Count} moves with alignment. Output saved to: {outputFolder}");
        }
        
        /// <summary>
        /// Parses the frame list file and extracts frame data.
        /// </summary>
        private static List<Frame> ParseFrameList(string filePath)
        {
            var frames = new List<Frame>();
            var lines = File.ReadAllLines(filePath);
            
            Frame? currentFrame = null;
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                
                if (trimmed == "StartAnimation")
                {
                    currentFrame = new Frame();
                }
                else if (trimmed == "EndAnimation")
                {
                    if (currentFrame != null)
                    {
                        frames.Add(currentFrame);
                        currentFrame = null;
                    }
                }
                else if (currentFrame != null && !trimmed.StartsWith("#"))
                {
                    // Parse frame properties
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    if (parts.Length >= 2)
                    {
                        switch (parts[0])
                        {
                            case "FrameCode":
                                currentFrame.FrameCode = int.Parse(parts[1]);
                                break;
                            case "SizeX":
                                currentFrame.SizeX = int.Parse(parts[1]);
                                break;
                            case "SizeY":
                                currentFrame.SizeY = int.Parse(parts[1]);
                                break;
                            case "MinSizeX":
                                currentFrame.MinSizeX = int.Parse(parts[1]);
                                break;
                            case "MinSizeY":
                                currentFrame.MinSizeY = int.Parse(parts[1]);
                                break;
                            case "MinOffsetX":
                                currentFrame.OffsetX = int.Parse(parts[1]);
                                break;
                            case "MinOffsetY":
                                currentFrame.OffsetY = int.Parse(parts[1]);
                                break;
                            case "Rotated":
                                currentFrame.IsRotated = parts[1] == "1";
                                break;
                        }
                    }
                }
            }
            
            return frames;
        }

        /// <summary>
        /// Parses the moves file and extracts move data.
        /// </summary>
        private static List<Move> ParseMovesList(string filePath)
        {
            var moves = new List<Move>();
            var lines = File.ReadAllLines(filePath);
            
            Move? currentMove = null;
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                
                // Check for Name field
                if (trimmed.StartsWith("Name "))
                {
                    currentMove = new Move();
                    var parts = trimmed.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        currentMove.Name = parts[1];
                    }
                }
                else if (trimmed == "EndMove")
                {
                    if (currentMove != null && !string.IsNullOrEmpty(currentMove.Name))
                    {
                        moves.Add(currentMove);
                    }
                    currentMove = null;
                }
                else if (currentMove != null && trimmed.StartsWith("Fr "))
                {
                    // Parse frame line: Fr  00 Anim 06 SubFr 00 Ang 0000 Rep 04 L 10 XM 5 YM  -2
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    if (parts.Length >= 6)
                    {
                        var moveFrame = new MoveFrame();
                        
                        // Find indices for each field
                        for (int i = 0; i < parts.Length; i++)
                        {
                            if (parts[i] == "Fr" && i + 1 < parts.Length)
                            {
                                if (int.TryParse(parts[i + 1], out int frameIndex))
                                {
                                    moveFrame.FrameIndex = frameIndex;
                                }
                            }
                            else if (parts[i] == "Anim" && i + 1 < parts.Length)
                            {
                                if (int.TryParse(parts[i + 1], out int animIndex))
                                {
                                    moveFrame.AnimIndex = animIndex;
                                }
                            }
                            else if (parts[i] == "Rep" && i + 1 < parts.Length)
                            {
                                if (int.TryParse(parts[i + 1], out int repCount))
                                {
                                    moveFrame.RepeatCount = repCount;
                                }
                            }
                            else if (parts[i] == "XM" && i + 1 < parts.Length)
                            {
                                if (int.TryParse(parts[i + 1], out int xm))
                                {
                                    moveFrame.XM = xm;
                                }
                            }
                            else if (parts[i] == "YM" && i + 1 < parts.Length)
                            {
                                if (int.TryParse(parts[i + 1], out int ym))
                                {
                                    moveFrame.YM = ym;
                                }
                            }
                        }
                        
                        currentMove.Frames.Add(moveFrame);
                    }
                }
            }
            
            return moves;
        }
        
        /// <summary>
        /// Processes a single frame and saves the aligned image.
        /// </summary>
        private static void ProcessFrame(Frame frame, string inputFolder, string outputFolder, string baseName)
        {
            // Calculate the image index from FrameCode (100001 -> 0, 100002 -> 1, etc.)
            int imageIndex = frame.FrameCode - 100001;
            
            // Construct the input file path
            string inputFile = Path.Combine(inputFolder, $"{baseName}_{imageIndex:D3}.png");
            
            if (!File.Exists(inputFile))
            {
                Console.WriteLine($"Warning: Input file not found: {inputFile}");
                return;
            }
            
            // Load the input image
            using (var inputImage = Image.Load<Rgba32>(inputFile))
            {
                Image<Rgba32> processedImage = inputImage;
                
                // Rotate if needed
                if (frame.IsRotated)
                {
                    processedImage = inputImage.Clone(ctx => ctx.Rotate(90));
                }
                
                // Create the output canvas with transparent background
                using (var canvas = new Image<Rgba32>(frame.SizeX, frame.SizeY, new Rgba32(0, 0, 0, 0)))
                {
                    // Resize the image to MinSizeX x MinSizeY if needed
                    if (processedImage.Width != frame.MinSizeX || processedImage.Height != frame.MinSizeY)
                    {
                        processedImage.Mutate(ctx => ctx.Resize(frame.MinSizeX, frame.MinSizeY));
                    }
                    
                    // Draw the processed image onto the canvas at the specified offset
                    canvas.Mutate(ctx => ctx.DrawImage(processedImage, new Point(frame.OffsetX, frame.OffsetY), 1f));
                    
                    // Save the output
                    string outputFile = Path.Combine(outputFolder, $"{baseName}_{imageIndex:D3}.png");
                    canvas.Save(outputFile);
                    
                    Console.WriteLine($"Processed frame {frame.FrameCode} -> {outputFile}");
                }
                
                // Dispose rotated image if it's different from the input
                if (frame.IsRotated && processedImage != inputImage)
                {
                    processedImage.Dispose();
                }
            }
        }
        
        /// <summary>
        /// Processes a single move and creates an animation sequence.
        /// </summary>
        private static void ProcessMove(Move move, string inputFolder, string outputFolder, string baseName)
        {
            // Create a folder for this move
            string moveFolderName = SanitizeFileName(move.Name);
            string moveFolder = Path.Combine(outputFolder, moveFolderName);
            Directory.CreateDirectory(moveFolder);
            
            int outputFrameIndex = 0;
            
            foreach (var moveFrame in move.Frames)
            {
                // Construct the input file path based on AnimIndex
                string inputFile = Path.Combine(inputFolder, $"{baseName}_{moveFrame.AnimIndex:D3}.png");
                
                if (!File.Exists(inputFile))
                {
                    Console.WriteLine($"Warning: Input file not found for move '{move.Name}': {inputFile}");
                    continue;
                }
                
                // Copy/repeat the frame according to RepeatCount
                for (int rep = 0; rep < moveFrame.RepeatCount; rep++)
                {
                    string outputFile = Path.Combine(moveFolder, $"{moveFolderName}_{outputFrameIndex:D3}.png");
                    
                    // Copy the file
                    File.Copy(inputFile, outputFile, overwrite: true);
                    outputFrameIndex++;
                }
            }
            
            Console.WriteLine($"Created animation for move '{move.Name}': {outputFrameIndex} frames");
        }

        /// <summary>
        /// Processes a single move with frame alignment, applying XM/YM offsets to base frame positions.
        /// </summary>
        private static void ProcessMoveWithAlignment(Move move, Dictionary<int, Frame> frameDict, 
            string inputFolder, string outputFolder, string baseName)
        {
            // Create a folder for this move
            string moveFolderName = SanitizeFileName(move.Name);
            string moveFolder = Path.Combine(outputFolder, moveFolderName);
            Directory.CreateDirectory(moveFolder);
            
            int outputFrameIndex = 0;
            
            foreach (var moveFrame in move.Frames)
            {
                // Look up the frame data for this AnimIndex
                if (!frameDict.TryGetValue(moveFrame.AnimIndex, out var frameData))
                {
                    Console.WriteLine($"Warning: No frame data found for AnimIndex {moveFrame.AnimIndex} in move '{move.Name}'");
                    continue;
                }
                
                // Construct the input file path based on AnimIndex
                string inputFile = Path.Combine(inputFolder, $"{baseName}_{moveFrame.AnimIndex:D3}.png");
                
                if (!File.Exists(inputFile))
                {
                    Console.WriteLine($"Warning: Input file not found for move '{move.Name}': {inputFile}");
                    continue;
                }
                
                // Process and repeat the frame according to RepeatCount
                for (int rep = 0; rep < moveFrame.RepeatCount; rep++)
                {
                    string outputFile = Path.Combine(moveFolder, $"{moveFolderName}_{outputFrameIndex:D3}.png");
                    
                    // Load the input image
                    using (var inputImage = Image.Load<Rgba32>(inputFile))
                    {
                        Image<Rgba32> processedImage = inputImage;
                        
                        // Rotate if needed (from frame data)
                        if (frameData.IsRotated)
                        {
                            processedImage = inputImage.Clone(ctx => ctx.Rotate(90));
                        }
                        
                        // Create the output canvas with transparent background
                        using (var canvas = new Image<Rgba32>(frameData.SizeX, frameData.SizeY, new Rgba32(0, 0, 0, 0)))
                        {
                            // Resize the image to MinSizeX x MinSizeY if needed
                            if (processedImage.Width != frameData.MinSizeX || processedImage.Height != frameData.MinSizeY)
                            {
                                processedImage.Mutate(ctx => ctx.Resize(frameData.MinSizeX, frameData.MinSizeY));
                            }
                            
                            // Calculate the final position by combining base offset with move offset
                            int finalOffsetX = frameData.OffsetX + moveFrame.XM;
                            int finalOffsetY = frameData.OffsetY + moveFrame.YM;
                            
                            // Draw the processed image onto the canvas at the calculated offset
                            canvas.Mutate(ctx => ctx.DrawImage(processedImage, new Point(finalOffsetX, finalOffsetY), 1f));
                            
                            // Save the output
                            canvas.Save(outputFile);
                        }
                        
                        // Dispose rotated image if it's different from the input
                        if (frameData.IsRotated && processedImage != inputImage)
                        {
                            processedImage.Dispose();
                        }
                    }
                    
                    outputFrameIndex++;
                }
            }
            
            Console.WriteLine($"Created aligned animation for move '{move.Name}': {outputFrameIndex} frames");
        }
        
        /// <summary>
        /// Sanitizes a file name by removing invalid characters.
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
            return sanitized;
        }
    }

    public class Frame
    {
        public int FrameCode { get; set; }
        public int SizeX { get; set; }
        public int SizeY { get; set; }
        public int MinSizeX { get; set; }
        public int MinSizeY { get; set; }
        public int OffsetX { get; set; }
        public int OffsetY { get; set; }
        public bool IsRotated { get; set; }
    }

    public class Move
    {
        public string Name { get; set; } = "";
        public List<MoveFrame> Frames { get; set; } = new List<MoveFrame>();
    }

    public class MoveFrame
    {
        public int FrameIndex { get; set; }
        public int AnimIndex { get; set; }
        public int RepeatCount { get; set; }
        public int XM { get; set; }
        public int YM { get; set; }
    }
}

using System;

namespace ExtractCLUT.Games.ThreeDO.ShadowWarrior
{
    /// <summary>
    /// Example usage of the Shadow Warrior frame processing utilities.
    /// </summary>
    public static class Example
    {
        public static void ProcessAlvinFrames()
        {
            // Example 1: Process and align individual frames
            string frameListPath = @"C:\path\to\ALVIN.FRAMES.txt";
            string imageFolder = @"C:\path\to\alvin\images";
            
            FrameListParser.ProcessFrames(frameListPath, imageFolder);
            // Output: aligned_output folder with properly positioned/rotated frames
        }

        public static void ProcessAlvinMoves()
        {
            // Example 2: Extract move animations
            string movesFilePath = @"C:\path\to\ALVIN.MOVES.txt";
            string imageFolder = @"C:\path\to\alvin\images";
            
            FrameListParser.ProcessMoves(movesFilePath, imageFolder);
            // Output: moves_output folder with subfolders for each move animation
        }

        public static void ProcessAlvinMovesWithAlignment()
        {
            // Example 3: Process moves with full alignment (RECOMMENDED)
            string framesFilePath = @"C:\path\to\ALVIN.FRAMES.txt";
            string movesFilePath = @"C:\path\to\ALVIN.MOVES.txt";
            string imageFolder = @"C:\path\to\alvin\images";
            
            FrameListParser.ProcessMovesWithAlignment(framesFilePath, movesFilePath, imageFolder);
            // Output: moves_aligned_output folder with fully positioned animation frames
        }

        public static void ProcessBothForCharacter(string characterName, string basePath)
        {
            // Example 4: Process both frames and moves for a character
            string framesFile = $@"{basePath}\{characterName}.FRAMES.txt";
            string movesFile = $@"{basePath}\{characterName}.MOVES.txt";
            string imageFolder = $@"{basePath}\{characterName}_images";

            Console.WriteLine($"Processing frames for {characterName}...");
            FrameListParser.ProcessFrames(framesFile, imageFolder);

            Console.WriteLine($"Processing moves with alignment for {characterName}...");
            FrameListParser.ProcessMovesWithAlignment(framesFile, movesFile, imageFolder);

            Console.WriteLine($"Complete! Check output folders in: {imageFolder}");
        }
    }
}

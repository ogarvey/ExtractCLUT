using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC.AniMagic
{
    /// <summary>
    /// Contains logic to parse and describe a Composer engine animation stream.
    /// </summary>
    public static class ComposerAnimationParser
    {
        /// <summary>
        /// The operation codes for an animation entry.
        /// </summary>
        public enum AnimationOp
        {
            Event = 1,
            PlayWave = 2,
            PlayAnim = 3,
            DrawSprite = 4
        }

        /// <summary>
        /// Represents a single operation within an animation's definition.
        /// </summary>
        private class AnimationEntry
        {
            public AnimationOp Op { get; set; }
            public ushort Priority { get; set; }
            public ushort State { get; set; }

            // Fields for simulation
            public int Counter { get; set; }
            public ushort PrevValue { get; set; }

            public override string ToString()
            {
                return $"Op: {Op}, Priority: {Priority}, State: {State}";
            }
        }

        /// <summary>
        /// Reads the animation data from the byte array and prints a descriptive log to the console.
        /// </summary>
        /// <param name="animData">The raw byte array of the animation resource.</param>
        public static void ReadAnimation(byte[] animData, StringBuilder? logBuilder = null)
        {
            using (var stream = new MemoryStream(animData))
            using (var reader = new BinaryReader(stream, Encoding.Default, false))
            {
                // 1. Read Animation Header
                uint entryCount = reader.ReadUInt32();
                uint duration = reader.ReadUInt32();
                uint totalSize = reader.ReadUInt32(); // The 'unknown' field

                logBuilder?.AppendLine("--- Animation Header ---");
                logBuilder?.AppendLine($"Entry Definitions: {entryCount}");
                logBuilder?.AppendLine($"Duration in Frames: {duration}");
                logBuilder?.AppendLine($"Total Resource Size: {totalSize} bytes");
                logBuilder?.AppendLine("------------------------\n");

                // 2. Read Animation Entry Definitions
                var entries = new List<AnimationEntry>();
                logBuilder?.AppendLine("--- Operation Entries ---");
                for (int i = 0; i < entryCount; i++)
                {
                    var entry = new AnimationEntry
                    {
                        Op = (AnimationOp)reader.ReadUInt16(),
                        Priority = reader.ReadUInt16(),
                        State = reader.ReadUInt16()
                    };
                    entries.Add(entry);
                    logBuilder?.AppendLine($"Entry {i}: {entry}");
                }
                logBuilder?.AppendLine("-------------------------\n");

                long frameDataOffset = reader.BaseStream.Position;

                // 3. Simulate Frame-by-Frame Processing
                for (int frame = 1; frame <= duration; frame++)
                {
                    logBuilder?.AppendLine($"========= FRAME {frame} (Stream Offset: {frameDataOffset}) =========");
                    reader.BaseStream.Position = frameDataOffset;

                    bool foundWait = false;
                    foreach (var entry in entries)
                    {
                        // The C++ code skips initial event ops in the second loop; we simulate that behavior.
                        if (!foundWait && entry.Op == AnimationOp.Event)
                        {
                            // In a real scenario, these might be processed separately.
                            // For this log, we'll just note them.
                        }
                        foundWait = true;

                        if (entry.Counter > 0)
                        {
                            entry.Counter--;
                            logBuilder?.AppendLine($"\t- Op '{entry.Op}': Waiting... ({entry.Counter} frames left)");
                            continue;
                        }

                        if (reader.BaseStream.Position >= reader.BaseStream.Length)
                        {
                            logBuilder?.AppendLine("\t- End of stream reached.");
                            break;
                        }

                        ushort data = reader.ReadUInt16();

                        if (data == 0xFFFF)
                        {
                            entry.Counter = reader.ReadUInt16();
                            logBuilder?.AppendLine($"\t- Op '{entry.Op}': DELAY started for {entry.Counter} frames.");
                            entry.Counter--; // The counter includes the current frame
                        }
                        else
                        {
                            switch (entry.Op)
                            {
                                case AnimationOp.Event:
                                    logBuilder?.AppendLine($"\t- Op Event: Trigger event ID {data}.");
                                    break;
                                case AnimationOp.PlayWave:
                                    logBuilder?.AppendLine($"\t- Op PlayWave: Play wave ID {data} with priority {entry.Priority}.");
                                    break;
                                case AnimationOp.PlayAnim:
                                    logBuilder?.AppendLine($"\t- Op PlayAnim: Play animation ID {data}.");
                                    break;
                                case AnimationOp.DrawSprite:
                                    if (data == 0 || (entry.PrevValue != 0 && data != entry.PrevValue))
                                    {
                                        logBuilder?.AppendLine($"\t- Op DrawSprite: Erase previous sprite ID {entry.PrevValue}.");
                                    }
                                    if (data != 0)
                                    {
                                        short x = reader.ReadInt16();
                                        short y = reader.ReadInt16();
                                        logBuilder?.AppendLine($"\t- Op DrawSprite: Draw sprite ID {data} at relative position ({x}, {y}) with Z-order {entry.Priority}.");
                                    }
                                    break;
                                default:
                                    logBuilder?.AppendLine($"\t- Unknown Op {(int)entry.Op} with data {data}.");
                                    break;
                            }
                            entry.PrevValue = data;
                        }
                    }
                    // The next frame's data starts where this frame's data ended.
                    frameDataOffset = reader.BaseStream.Position;
                    logBuilder?.AppendLine();
                }
                logBuilder?.AppendLine("========= ANIMATION COMPLETE =========");
            }
        }
    }
}

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace ExtractCLUT.Games.PC
{
    public static class FileFormatHelper
    {
        public static async Task<List<string>> ScanForAsciiStringsAsync(string filePath, int minimumLength, bool requireNullTerminated = false)
        {
            // time how long the scan takes
            var watch = Stopwatch.StartNew();
            Console.WriteLine($"Scanning {filePath} for ASCII strings of at least {minimumLength} characters in length...");
            Console.WriteLine("This may take a while depending on the size of the file.");
            Console.WriteLine($"Scanning started at {DateTime.Now}");

            const int bufferSize = 4096; // Adjust the buffer size as needed
            var foundStrings = new ConcurrentBag<string>();
            byte[] buffer = new byte[bufferSize];
            int bytesRead;

            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true))
            {
                StringBuilder potentialString = new StringBuilder();
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    ProcessBuffer(buffer, bytesRead, minimumLength, potentialString, foundStrings, requireNullTerminated);
                    //Console.Write($"\r\n{watch.Elapsed.TotalSeconds}\t Scanning... {stream.Position / (double)stream.Length:P0}");
                }

                // Final check to capture any string at the end of the file
                if (potentialString.Length >= minimumLength)
                    foundStrings.Add(potentialString.ToString());
            }

            watch.Stop();
            Console.WriteLine($"\nScanning completed at {DateTime.Now}");
            Console.WriteLine($"Total time taken: {watch.Elapsed.TotalSeconds} seconds");
            Console.WriteLine($"Found {foundStrings.Count} strings of at least {minimumLength} characters in length.");

            return new List<string>(foundStrings);
        }

        private static void ProcessBuffer(byte[] buffer, int bytesRead, int minimumLength, StringBuilder currentString, ConcurrentBag<string> foundStrings, bool requireNullTerminated)
        {
            for (int i = 0; i < bytesRead; i++)
            {
                char currentChar = (char)buffer[i];
                if (currentChar >= 32 && currentChar <= 126) // Printable ASCII range
                {
                    currentString.Append(currentChar);
                }
                else
                {
                    if (currentString.Length >= minimumLength)
                    {
                        if (!requireNullTerminated || (requireNullTerminated && currentChar == 0x00)) foundStrings.Add(currentString.ToString());
                    }
                    currentString.Clear();
                }
            }
        }
    }
}

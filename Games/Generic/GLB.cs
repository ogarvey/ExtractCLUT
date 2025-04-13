using System;
using System.IO;
using System.Text;


namespace ExtractCLUT.Games.Generic;
public static class GLBExtractor
{
    public static void ExtractGLB(string filePath, string outputDirectory)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"File not found: {filePath}");
            return;
        }

        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        byte[] fileBytes = File.ReadAllBytes(filePath);

        if (fileBytes.Length < 4 || fileBytes[0] != 0x64 || fileBytes[1] != 0x9B || fileBytes[2] != 0xD1 || fileBytes[3] != 0x09)
        {
            Console.WriteLine("Invalid GLB file signature.");
            return;
        }

        byte[] decryptedFatEntry = DecryptFatEntry(fileBytes, 0, "32768GLB");

        int fileCount = BitConverter.ToInt32(decryptedFatEntry, 4);

        for (int i = 0; i < fileCount; i++)
        {
            int fatEntryOffset = (i + 1) * 28;
            byte[] encryptedFatEntry = new byte[28];
            Array.Copy(fileBytes, fatEntryOffset, encryptedFatEntry, 0, 28);

            byte[] decryptedEntry = DecryptFatEntry(fileBytes, fatEntryOffset, "32768GLB");

            // if (i % 2 == 1)
            // {
            //     for (int j = 0; j < 28; j++)
            //     {
            //         decryptedEntry[j] = (byte)(decryptedEntry[j] ^ (new byte[] { 0x75, 0x7B, 0x74, 0x0B })[j % 4]);
            //     }
            // }

            uint flags = BitConverter.ToUInt32(decryptedEntry, 0);
            uint fileOffset = BitConverter.ToUInt32(decryptedEntry, 4);
            uint fileLength = BitConverter.ToUInt32(decryptedEntry, 8);
            string fileName = Encoding.ASCII.GetString(decryptedEntry, 12, 16).Trim('\0');

            byte[] fileData = new byte[fileLength];
            Array.Copy(fileBytes, fileOffset, fileData, 0, fileLength);

            if (flags == 1) // Encrypted file
            {
                fileData = DecryptFileData(fileData, "32768GLB");
            }
            fileName = Path.GetInvalidFileNameChars().Aggregate(fileName, (current, c) => current.Replace(c.ToString(), ""));
            string outputFile = Path.Combine(outputDirectory, fileName == "" ? $"file_{i}.bin" : fileName);
            if (File.Exists(outputFile))
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(outputFile);
                var fileExtension = Path.GetExtension(outputFile);
                var newFileName = $"{fileNameWithoutExtension}_{i}{fileExtension}";
                outputFile = Path.Combine(outputDirectory, newFileName);
            }
            File.WriteAllBytes(outputFile, fileData);

            Console.WriteLine($"Extracted: {(fileName == "" ? $"file_{i}.bin" : fileName)}");
        }
    }

    private static byte[] DecryptFatEntry(byte[] data, int offset, string key)
    {
        byte[] decrypted = new byte[28];
        int keyIndex = 1;
        byte previousByte = (byte)key[1];

        for (int i = 0; i < 28; i++)
        {
            byte currentByte = data[offset + i];
            int keyChar = key[keyIndex];
            byte decryptedByte = (byte)((currentByte - keyChar - previousByte) & 0xFF);
            decrypted[i] = decryptedByte;

            previousByte = currentByte;
            keyIndex = (keyIndex + 1) % key.Length;
        }

        return decrypted;
    }

    private static byte[] DecryptFileData(byte[] data, string key)
    {
        byte[] decrypted = new byte[data.Length];
        int keyIndex = 1;
        byte previousByte = (byte)key[1];

        for (int i = 0; i < data.Length; i++)
        {
            byte currentByte = data[i];
            int keyChar = key[keyIndex];
            byte decryptedByte = (byte)((currentByte - keyChar - previousByte) & 0xFF);
            decrypted[i] = decryptedByte;

            previousByte = currentByte;
            keyIndex = (keyIndex + 1) % key.Length;
        }

        return decrypted;
    }
}

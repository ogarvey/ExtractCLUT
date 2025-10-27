using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PC
{
	public static class Gargoyles
	{

		// The decryption key found during reverse engineering.
		// Original hex: 38 45 37 44 35 35 44 42 32 43 43 39 00 00 00 00
		private static readonly byte[] Key =
		{
						0x38, 0x45, 0x37, 0x44, 0x35, 0x35, 0x44, 0x42,
						0x32, 0x43, 0x43, 0x39, 0x00, 0x00, 0x00, 0x00
				};

		// The Initialization Vector (IV) found during reverse engineering.
		// Original hex: 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
		private static readonly byte[] IV = new byte[16]; // An all-zero IV

		/// <summary>
		/// Decrypts the provided encrypted asset data.
		/// </summary>
		/// <param name="encryptedData">The raw byte array of the encrypted asset.</param>
		/// <returns>A byte array containing the decrypted data.</returns>
		/// <exception cref="ArgumentNullException">Thrown if encryptedData is null.</exception>
		/// <exception cref="CryptographicException">Thrown if the decryption fails, which can indicate incorrect key, IV, or data corruption.</exception>
		public static byte[] DecryptAsset(byte[] encryptedData)
		{
			if (encryptedData == null || encryptedData.Length == 0)
			{
				throw new ArgumentNullException(nameof(encryptedData));
			}

			using (var aes = Aes.Create())
			{
				// The analysis showed the use of AES with Cipher Block Chaining (CBC).
				aes.Mode = CipherMode.CBC;

				// PKCS7 is the most common padding and a likely default.
				// If decryption fails or produces garbage data, this might need to be
				// changed to PaddingMode.Zeros or PaddingMode.None if the data is
				// guaranteed to be a multiple of the block size.
				aes.Padding = PaddingMode.Zeros;

				aes.BlockSize = 128; // AES block size is 128 bits
				aes.KeySize = 128;   // The key is 16 bytes (128 bits)

				aes.Key = Key;
				aes.IV = IV;

				using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
				using (var memoryStream = new MemoryStream())
				{
					using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Write))
					{
						cryptoStream.Write(encryptedData, 0, encryptedData.Length);
						cryptoStream.FlushFinalBlock();
					}
					return memoryStream.ToArray();
				}
			}
		}

	}
}

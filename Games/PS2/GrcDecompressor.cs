using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Games.PS2
{
	public static class GrcDecompressor
	{
		// int GrcEncrypt(char* GrcBug, char* DecBug, int GrcSize)
		//{
		// int i = 0;
		// int k = 0;
		// int len;
		// int dat;
		// int declen;

		// do
		// {
		//   dat = GrcBug[i++] & 0xff;

		//   if((dat & 0xc0) == 0xc0)
		//   {
		//     len = dat & 0x3F;

		//     if(len == 1)
		//     {
		//       DecBug[k++] = GrcBug[i++] & 0xff;
		//     }
		//     else
		//     {
		//           declen = GrcBug[i++] & 0xff;

		//           if(len <= 0) continue;

		//           do
		//           {
		//             DecBug[k++] = DecBug[k - declen];
		//           }
		//       while (--len != 0) ;
		//      }
		//    }
		//    else
		//    {
		//       DecBug[k++] = dat;
		//    }
		//  }
		//  while (i < GrcSize) ;

		//   return k;
		// }

		public static byte[] Decompress(byte[] compressedData)
		{
			using var dReader = new BinaryReader(new MemoryStream(compressedData));
			var outputBuffer = new List<byte>();

			while (dReader.BaseStream.Position < dReader.BaseStream.Length-1)
			{
				var dat = dReader.ReadByte();

				if ((dat & 0xC0) == 0xC0)
				{
					var len = dat & 0x3F;

					if (len == 1)
					{
						outputBuffer.Add(dReader.ReadByte());
					}
					else
					{
						var declen = dReader.ReadByte();

						if (len <= 0) continue;

						for (int i = 0; i < len; i++)
						{
							var value = outputBuffer[outputBuffer.Count - declen];
							outputBuffer.Add(value);
						}
					}
				}
				else
				{
					outputBuffer.Add(dat);
				}
			}
			return outputBuffer.ToArray();
		}

	}
}

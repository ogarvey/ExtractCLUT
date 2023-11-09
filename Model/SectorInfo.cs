using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExtractCLUT.Model
{
	[Flags]
	public enum SubmodeBits : byte
	{
		Cdieof = 0b10000000,
		Cdirt = 0b01000000,
		Cdiform = 0b00100000,
		Cditr = 0b00010000,
		Cdid = 0b00001000,
		Cdia = 0b00000100,
		Cdiv = 0b00000010,
		Cdieor = 0b00000001,
		Cdiany = 0b00001110,
	}
	public class SectorInfo
	{
		public SectorInfo(string cdiFile, byte[] chunk)
		{
			CdiFile = cdiFile;
			Data = chunk;
		}

		public int? OriginalOffset { get; set; }
		public string CdiFile { get; set; }
		public int SectorIndex { get; set; }
		public byte FileNumber { get; set; }
		public byte Channel { get; set; }
		public byte SubMode { get; set; }
		public byte CodingInformation { get; set; }
		public byte[] Data { get; set; }
		public bool IsEmptySector => (SubMode & (byte)SubmodeBits.Cdiany) == 0 && Channel == 0 && CodingInformation == 0;
		public bool IsEOF => (SubMode & (1 << 7)) != 0;
		public bool IsRTF => (SubMode & (1 << 6)) != 0;
		public bool IsForm2 => (SubMode & (1 << 5)) != 0;
		public bool IsTrigger => (SubMode & (1 << 4)) != 0;
		public bool IsData => (SubMode & (1 << 3)) != 0;
		public bool IsAudio => (SubMode & (1 << 2)) != 0;
		public bool IsVideo => (SubMode & (1 << 1)) != 0;
		public bool IsEOR => (SubMode & 1) != 0;
		public bool IsASCF => (CodingInformation & (1 << 7)) != 0;
		public bool HasEvenLines => (CodingInformation & (1 << 6)) != 1;
		public int Resolution => (CodingInformation >> 4) & 0x3;
		public int Coding => CodingInformation & 0xF;
		public bool Emphasis => (CodingInformation & (1 << 6)) != 0;
		public int BitsPerSample => (CodingInformation >> 4) & 0x3;
		public int SamplingFrequency => (CodingInformation >> 2) & 0x3;
		public int MonoStereo => CodingInformation & 0x3;
		public string ResolutionString => Resolution switch
		{
			0 => "Normal",
			1 => "Double",
			2 => "Reserved",
			3 => "High",
			_ => "Reserved"
		};
		public string VideoString => Coding switch
		{
			0x0 => "CLUT4",
			0x1 => "CLUT7",
			0x2 => "CLUT8",
			0x3 => "RL3",
			0x4 => "RL7",
			0x5 => "DYUV",
			0x6 => "RGB555 (lower)",
			0x7 => "RGB555 (upper)",
			0x8 => "QHY",
			_ => "Reserved"
		};
		public bool IsMono => MonoStereo == 0;
		public string SamplingFrequencyString => SamplingFrequency switch
		{
			0 => "37.8 kHz",
			1 => "18.9 kHz",
			_ => "Reserved"
		};

		public int SamplingFrequencyValue => SamplingFrequency switch
		{
			0 => 37800,
			1 => 18900,
			_ => 0
		};

		public string BitsPerSampleString => BitsPerSample switch
		{
			0 => "4 bits",
			1 => "8 bits",
			_ => "Reserved"
		};

        public override string ToString()
        {
            return $"Sector: {SectorIndex} of {CdiFile}, d: {IsData}, v: {IsVideo}, a: {IsAudio}";
        }
    }

}

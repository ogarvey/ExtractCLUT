using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtractCLUT.Helpers
{
	public static class XmiHelper
	{
		public static void ConvertXmiToMidi(string xmiPath, string midiPath)
		{
			// Will collect (absoluteTick, rawMidiBytes) pairs
			var midiEvents = new List<(long tick, byte[] data)>();

			// 1) Open and parse IFF XDIR header
			using var fs = File.OpenRead(xmiPath);
			using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: true);

			// FORM XDIR
			if (ReadFourCC(br) != "FORM") throw new InvalidDataException("Not an IFF file");
			uint xdirSize = ReadUInt32BE(br);
			if (ReadFourCC(br) != "XDIR") throw new InvalidDataException("Not an XMI XDIR file");
			long xdirEnd = br.BaseStream.Position + xdirSize - 4;

			// inside XDIR, look for INFO (we only need to skip it)
			while (br.BaseStream.Position < xdirEnd)
			{
				string chunk = ReadFourCC(br);
				uint size = ReadUInt32BE(br);
				if (chunk == "INFO")
				{
					// seqCount = br.ReadUInt16();  // unused here
					br.BaseStream.Seek(size, SeekOrigin.Current);
				}
				else
				{
					br.BaseStream.Seek(size, SeekOrigin.Current);
				}
				// IFF chunks are even-aligned
				if ((size & 1) != 0) br.BaseStream.Seek(1, SeekOrigin.Current);
			}

			// 2) Find CAT XMID
			if (ReadFourCC(br) != "CAT ") throw new InvalidDataException("Missing CAT chunk");
			uint catSize = ReadUInt32BE(br);
			if (ReadFourCC(br) != "XMID") throw new InvalidDataException("Not an XMID catalogue");
			long catEnd = br.BaseStream.Position + catSize - 4;

			// 3) Inside CAT XMID, find the first FORM XMID (one song) and parse TIMB + EVNT
			while (br.BaseStream.Position < catEnd)
			{
				if (ReadFourCC(br) != "FORM")
				{
					// skip unknown chunk
					br.BaseStream.Seek(-4, SeekOrigin.Current);
					var skipId = ReadFourCC(br);
					uint skipSize = ReadUInt32BE(br);
					br.BaseStream.Seek(skipSize + (skipSize & 1), SeekOrigin.Current);
					continue;
				}
				uint formSize = ReadUInt32BE(br);
				string formType = ReadFourCC(br);
				long formEnd = br.BaseStream.Position + formSize - 4;
				if (formType != "XMID")
				{
					br.BaseStream.Seek(formSize - 4 + (formSize & 1), SeekOrigin.Current);
					continue;
				}

				// Prepare bank/patch maps (up to 16 channels)
				var bankMap = Enumerable.Repeat<byte>(0, 16).ToArray();
				var patchMap = Enumerable.Repeat<byte>(0, 16).ToArray();

				// Walk subchunks inside this FORM XMID
				while (br.BaseStream.Position < formEnd)
				{
					string subId = ReadFourCC(br);
					uint subSize = ReadUInt32BE(br);
					if (subId == "TIMB")
					{
						// load per-channel (patch, bank)
						int count = br.ReadUInt16();  // little-endian
						for (int i = 0; i < count && i < 16; i++)
						{
							patchMap[i] = br.ReadByte();
							bankMap[i] = br.ReadByte();
						}
						// skip any padding
						long read = 2 + count * 2;
						if (subSize > read)
							br.BaseStream.Seek(subSize - read, SeekOrigin.Current);
					}
					else if (subId == "EVNT")
					{
						// parse all musical events
						var data = br.ReadBytes((int)subSize);
						ParseEvnt(data, bankMap, patchMap, midiEvents);
						break; // only first song
					}
					else
					{
						br.BaseStream.Seek(subSize, SeekOrigin.Current);
					}
					if ((subSize & 1) != 0) br.BaseStream.Seek(1, SeekOrigin.Current);
				}
				break;
			}

			// 4) Write out a format-0 MIDI at 120 Hz (PPQN=60, tempo=500_000)
			WriteMidi(midiEvents, midiPath);
		}

		static void ParseEvnt(byte[] data, byte[] bankMap, byte[] patchMap, List<(long tick, byte[] data)> dest)
		{
			using var ms = new MemoryStream(data);
			using var br = new BinaryReader(ms);
			long tick = 0;
			while (ms.Position < ms.Length)
			{
				byte b = br.ReadByte();
				if (b < 0x80)
				{
					// delay byte(s)
					int delay = b;
					while (b == 0x7F)
					{
						b = br.ReadByte();
						delay += b;
					}
					tick += delay;
				}
				else
				{
					int status = b;
					int ch = status & 0x0F;
					int cmd = status & 0xF0;
					switch (cmd)
					{
						case 0x90: // Note On w/ duration
							{
								byte note = br.ReadByte();
								byte vel = br.ReadByte();
								int duration = ReadVarLen(br);
								// on
								dest.Add((tick, new byte[] { (byte)(0x90 | ch), note, vel }));
								// off
								dest.Add((tick + duration, new byte[] { (byte)(0x80 | ch), note, 0 }));
							}
							break;
						case 0xC0: // Program Change → Bank + Patch
							{
								byte patch = br.ReadByte();
								byte bank = bankMap[ch];
								// Bank Select MSB = CC0
								dest.Add((tick, new byte[] { (byte)(0xB0 | ch), 0x00, bank }));
								// Program Change
								dest.Add((tick, new byte[] { (byte)(0xC0 | ch), patch }));
							}
							break;
						case 0xB0: // Control Change
							{
								byte ctrl = br.ReadByte();
								byte val = br.ReadByte();
								dest.Add((tick, new byte[] { (byte)(0xB0 | ch), ctrl, val }));
							}
							break;
						case 0x80: // Explicit Note Off
							{
								byte note = br.ReadByte();
								byte vel = br.ReadByte();
								dest.Add((tick, new byte[] { (byte)(0x80 | ch), note, vel }));
							}
							break;
						default:
							// skip unsupported voice messages
							if (cmd == 0xE0) // pitch bend
							{
								byte lsb = br.ReadByte();
								byte msb = br.ReadByte();
								dest.Add((tick, new byte[] { (byte)(0xE0 | ch), lsb, msb }));
							}
							break;
					}
				}
			}
		}

		static void WriteMidi(List<(long tick, byte[] data)> events, string path)
		{
			const int PPQN = 60;
			const int TEMPO = 500_000;   // µs per quarter note → 120 ticks/sec

			// insert tempo meta at tick=0
			var all = new List<(long tick, byte[] data)>
						{
								(0, new byte[] {
										0xFF, 0x51, 0x03,
										(byte)((TEMPO >> 16) & 0xFF),
										(byte)((TEMPO >>  8) & 0xFF),
										(byte)((TEMPO      ) & 0xFF)
								})
						};
			all.AddRange(events);

			// sort by absolute tick
			all = all.OrderBy(e => e.tick).ToList();

			// build track by writing var-len delta + event bytes
			using var trackStream = new MemoryStream();
			using var tw = new BinaryWriter(trackStream);
			long prevTick = 0;
			foreach (var (abs, data) in all)
			{
				long delta = abs - prevTick;
				prevTick = abs;
				WriteVarLength(tw, delta);
				tw.Write(data);
			}
			// end-of-track
			WriteVarLength(tw, 0);
			tw.Write(new byte[] { 0xFF, 0x2F, 0x00 });

			var trackBytes = trackStream.ToArray();

			// write MIDI file
			using var fs = File.Create(path);
			using var bw = new BinaryWriter(fs);
			// header
			bw.Write(Encoding.ASCII.GetBytes("MThd"));
			WriteUInt32BE(bw, 6);
			WriteUInt16BE(bw, 0);     // format 0
			WriteUInt16BE(bw, 1);     // one track
			WriteUInt16BE(bw, (ushort)PPQN);
			// track chunk
			bw.Write(Encoding.ASCII.GetBytes("MTrk"));
			WriteUInt32BE(bw, (uint)trackBytes.Length);
			bw.Write(trackBytes);
		}

		// --- helpers ---

		static string ReadFourCC(BinaryReader r) =>
				new string(r.ReadChars(4));

		static uint ReadUInt32BE(BinaryReader r)
		{
			var b = r.ReadBytes(4);
			return (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
		}

		static void WriteUInt32BE(BinaryWriter w, uint v)
		{
			w.Write((byte)((v >> 24) & 0xFF));
			w.Write((byte)((v >> 16) & 0xFF));
			w.Write((byte)((v >> 8) & 0xFF));
			w.Write((byte)(v & 0xFF));
		}

		static void WriteUInt16BE(BinaryWriter w, ushort v)
		{
			w.Write((byte)((v >> 8) & 0xFF));
			w.Write((byte)(v & 0xFF));
		}

		static int ReadVarLen(BinaryReader r)
		{
			int value = 0;
			byte b;
			do
			{
				b = r.ReadByte();
				value = (value << 7) | (b & 0x7F);
			} while ((b & 0x80) != 0);
			return value;
		}

		static void WriteVarLength(BinaryWriter w, long value)
		{
			// build bytes in reverse
			var buffer = new List<byte>();
			buffer.Insert(0, (byte)(value & 0x7F));
			value >>= 7;
			while (value > 0)
			{
				buffer.Insert(0, (byte)((value & 0x7F) | 0x80));
				value >>= 7;
			}
			foreach (var bt in buffer)
				w.Write(bt);
		}
	}
}

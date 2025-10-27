using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;

namespace ExtractCLUT.Helpers
{

	/// <summary>
	/// Represents a generic IFF chunk.
	/// </summary>
	public class IffChunk
	{
		public string Id { get; set; } // 4-character ID
		public uint Length { get; set; }
		public byte[]? Data { get; set; }
		public long FileOffset { get; set; } // Offset of data start in the file

		public IffChunk(string id, uint length, long offset)
		{
			Id = id;
			Length = length;
			FileOffset = offset;
		}
	}

	/// <summary>
	/// Represents a parsed XMI event before full MIDI conversion.
	/// </summary>
	public class ParsedXmiEvent
	{
		public long AbsoluteXmiTick { get; set; }
		public byte Channel { get; set; }
		public byte StatusByte { get; set; } // Original MIDI status byte (e.g., 0x90, 0xB0)
		public byte Data1 { get; set; }
		public byte Data2 { get; set; } // Or 0 if not present
		public long XmiDuration { get; set; } // For note events, in XMI ticks

		// Specific event types for easier handling
		public bool IsNoteEvent => (StatusByte & 0xF0) == 0x90;
		public bool IsControlChangeEvent => (StatusByte & 0xF0) == 0xB0;
		public bool IsProgramChangeEvent => (StatusByte & 0xF0) == 0xC0;
	}

	/// <summary>
	/// Represents an instrument definition, potentially from TIMB or inferred.
	/// For simplicity, we'll mainly rely on Program Change and Bank Select from EVNT.
	/// </summary>
	public class XmiInstrument
	{
		public int Channel { get; set; }
		public int Patch { get; set; }
		public int Bank { get; set; } // Could be a combined bank or MSB/LSB
	}

	// ---------------
	// XMI File Reader
	// ---------------
	public class XmiReader
	{
		private readonly string _filePath;
		public List<IffChunk> RootChunks { get; private set; }
		public List<ParsedXmiEvent> Events { get; private set; }
		public List<XmiInstrument> Instruments { get; private set; } // Placeholder

		// It's crucial to determine or assume the XMI's native tick resolution.
		// This value significantly affects tempo and duration interpretation.
		// The Shikadi wiki doesn't specify a universal XMI TPQN.
		// Common values for older systems were often tied to hardware timers (e.g., 60Hz, 70Hz, 120Hz).
		// Or, it could be a musical TPQN. For this example, we'll assume XMI ticks
		// map directly to MIDI ticks, and the MIDI tempo events will define speed.
		// If XMI has a fixed "ticks per second", that would require a different conversion.
		public const short DEFAULT_MIDI_TPQN = 480;


		public XmiReader(string filePath)
		{
			_filePath = filePath;
			RootChunks = new List<IffChunk>();
			Events = new List<ParsedXmiEvent>();
			Instruments = new List<XmiInstrument>();
		}


		public bool ParseFile()
		{
			if (!File.Exists(_filePath))
			{
				Console.WriteLine($"Error: File not found: {_filePath}");
				return false;
			}

			try
			{
				using (var reader = new BinaryReader(File.Open(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
				{
					bool foundXdir = false;
					int songCountFromXdir = 0;

					while (reader.BaseStream.Position < reader.BaseStream.Length)
					{
						string chunkId = ReadChunkId(reader);
						if (string.IsNullOrEmpty(chunkId) && reader.BaseStream.Position == reader.BaseStream.Length) break; // Likely EOF
						if (string.IsNullOrEmpty(chunkId))
						{
							Console.WriteLine($"Warning: Could not read chunk ID at offset {reader.BaseStream.Position}. Assuming EOF or invalid data.");
							break;
						}

						if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) // Check for length field
						{
							Console.WriteLine($"Warning: EOF after reading chunk ID '{chunkId}' at offset {reader.BaseStream.Position - 4}. Cannot read length.");
							break;
						}
						uint chunkLength = ReadBigEndianUInt32(reader); // Length of the chunk's data
						long chunkDataStartOffset = reader.BaseStream.Position; // Position where chunk data begins

						// Check if the declared chunk data would exceed file bounds
						if (chunkDataStartOffset + chunkLength > reader.BaseStream.Length)
						{
							Console.WriteLine($"Warning: Chunk '{chunkId}' (declared data length {chunkLength} at offset {chunkDataStartOffset}) exceeds file bounds ({reader.BaseStream.Length}). Stopping parse.");
							break;
						}

						if (chunkId == "FORM")
						{
							if (chunkLength < 4) // FORM chunk data must at least contain its type
							{
								Console.WriteLine($"Warning: FORM chunk at {chunkDataStartOffset - 8} has data length {chunkLength}, too short for a FORM type. Skipping.");
								reader.BaseStream.Seek(chunkDataStartOffset + chunkLength, SeekOrigin.Begin); // Skip its declared data
							}
							else
							{
								string formType = ReadChunkId(reader);
								Console.WriteLine($"Found FORM chunk with type: {formType}");

								if (formType == "XDIR")
								{
									foundXdir = true;
									// chunkLength includes the formType, so pass chunkLength - 4 for XDIR content
									songCountFromXdir = ParseXdirForm(reader, chunkLength - 4);
								}
								else if (formType == "XMID") // Standalone XMI file (not inside a CAT)
								{
									// Pass chunkLength - 4 for XMID content
									ParseXmidForm(reader, chunkLength - 4);
								}
								else
								{
									Console.WriteLine($"Skipping FORM of unknown type: {formType}");
									// Seek to the end of this FORM's data section
									reader.BaseStream.Seek(chunkDataStartOffset + chunkLength, SeekOrigin.Begin);
								}
							}
						}
						else if (chunkId == "CAT ") // Note the space in "CAT "
						{
							ParseCatChunk(reader, chunkLength, foundXdir ? songCountFromXdir : -1);
						}
						else
						{
							Console.WriteLine($"Skipping unknown root chunk: {chunkId}");
							reader.BaseStream.Seek(chunkDataStartOffset + chunkLength, SeekOrigin.Begin); // Skip its data
						}

						// Ensure reader is positioned correctly after processing the entire chunk (ID, Length, Data, Pad)
						// The current position should be at chunkDataStartOffset + chunkLength
						// (because parsing methods should consume exactly chunkLength of data from chunkDataStartOffset)
						// Then apply IFF padding for THIS chunk.

						long expectedPositionAfterData = chunkDataStartOffset + chunkLength;
						if (reader.BaseStream.Position < expectedPositionAfterData)
						{
							Console.WriteLine($"Warning: Chunk '{chunkId}' processing did not consume all its data. Current: {reader.BaseStream.Position}, Expected end of data: {expectedPositionAfterData}. Seeking to expected end of data.");
							reader.BaseStream.Seek(expectedPositionAfterData, SeekOrigin.Begin);
						}
						else if (reader.BaseStream.Position > expectedPositionAfterData)
						{
							Console.WriteLine($"CRITICAL Warning: Chunk '{chunkId}' processing overran its data. Current: {reader.BaseStream.Position}, Expected end of data: {expectedPositionAfterData}. File parsing may be corrupted.");
							// Attempt to position for padding based on declared length
							reader.BaseStream.Seek(expectedPositionAfterData, SeekOrigin.Begin);
						}

						// Apply IFF padding: if chunkLength (of data) is odd, there's one pad byte
						if (chunkLength % 2 != 0)
						{
							if (reader.BaseStream.Position < reader.BaseStream.Length)
							{
								reader.ReadByte(); // Consume the pad byte
							}
							else
							{
								Console.WriteLine($"Warning: Expected pad byte for chunk '{chunkId}' but reached EOF.");
								break;
							}
						}
					}
				}
				return Events.Any(); // Success if we parsed some events
			}
			catch (EndOfStreamException eofEx)
			{
				Console.WriteLine($"Error: Reached end of file unexpectedly during parsing. {eofEx.Message}");
				return false;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error parsing XMI file: {ex.Message}");
				return false;
			}
		}

		private int ParseXdirForm(BinaryReader reader, uint xdirContentLength)
		{
			long xdirContentStartOffset = reader.BaseStream.Position;
			long xdirContentEndOffset = xdirContentStartOffset + xdirContentLength;
			int songCount = 0;
			bool infoFound = false;

			while (reader.BaseStream.Position < xdirContentEndOffset)
			{
				string chunkId = ReadChunkId(reader);
				if (string.IsNullOrEmpty(chunkId))
				{
					Console.WriteLine($"Warning: Null/empty sub-chunk ID in XDIR at {reader.BaseStream.Position - 4}.");
					break;
				}

				if (reader.BaseStream.Position + 4 > xdirContentEndOffset)
				{
					Console.WriteLine($"Warning: EOF in XDIR while reading length for sub-chunk '{chunkId}'."); break;
				}
				uint chunkDataLength = ReadBigEndianUInt32(reader); // Length of sub-chunk's data
				long subChunkDataStart = reader.BaseStream.Position;

				if (subChunkDataStart + chunkDataLength > xdirContentEndOffset)
				{
					Console.WriteLine($"Warning: XDIR sub-chunk '{chunkId}' (data length {chunkDataLength}) at {subChunkDataStart} exceeds XDIR FORM bounds ({xdirContentEndOffset}). Skipping XDIR parse.");
					reader.BaseStream.Seek(xdirContentEndOffset, SeekOrigin.Begin); // Go to end of XDIR content
					return songCount;
				}

				if (chunkId == "INFO")
				{
					infoFound = true;
					if (chunkDataLength >= 2) // Minimum for count
					{
						songCount = ReadBigEndianUInt16(reader);
						Console.WriteLine($"XDIR INFO: Found {songCount} songs listed.");
						if (chunkDataLength > 2) // Skip rest of INFO data
							reader.BaseStream.Seek(subChunkDataStart + chunkDataLength, SeekOrigin.Begin);
					}
					else
					{
						Console.WriteLine("Warning: XDIR INFO chunk is too small for song count.");
						reader.BaseStream.Seek(subChunkDataStart + chunkDataLength, SeekOrigin.Begin);
					}
				}
				else
				{
					Console.WriteLine($"Skipping unknown XDIR sub-chunk: {chunkId}");
					reader.BaseStream.Seek(subChunkDataStart + chunkDataLength, SeekOrigin.Begin);
				}

				// IFF Padding for the sub-chunk's data
				if (chunkDataLength % 2 != 0)
				{
					if (reader.BaseStream.Position < xdirContentEndOffset) reader.ReadByte(); // Consume pad
					else if (reader.BaseStream.Position == xdirContentEndOffset) { /* Pad byte was the last byte */ }
					else { Console.WriteLine($"Warning: Expected pad for XDIR sub-chunk {chunkId} but beyond XDIR content end."); break; }
				}
			}
			if (!infoFound) Console.WriteLine("Warning: XDIR FORM did not contain an INFO chunk.");
			// Ensure reader is at the end of XDIR content
			if (reader.BaseStream.Position < xdirContentEndOffset)
				reader.BaseStream.Seek(xdirContentEndOffset, SeekOrigin.Begin);
			return songCount;
		}

		private void ParseCatChunk(BinaryReader reader, uint catContentsLength, int expectedSongCount)
		{
			long catContentsStartOffset = reader.BaseStream.Position;
			long catContentsDeclaredEndOffset = catContentsStartOffset + catContentsLength;
			int songsFoundInCat = 0;

			if (catContentsLength < 4)
			{ // CAT must at least have its content type ID
				Console.WriteLine("Warning: CAT chunk data length is too small for its content type ID. Skipping CAT parse.");
				reader.BaseStream.Seek(catContentsDeclaredEndOffset, SeekOrigin.Begin);
				return;
			}

			string catContentType = ReadChunkId(reader); // Should be "XMID"
			Console.WriteLine($"CAT chunk contains FORMs of type: {catContentType}");
			if (catContentType != "XMID")
			{
				Console.WriteLine($"Warning: CAT chunk content type is '{catContentType}', expected 'XMID'. Processing may be incorrect.");
			}

			while (reader.BaseStream.Position < catContentsDeclaredEndOffset)
			{
				long currentFormStartInFile = reader.BaseStream.Position; // Position where "FORM" ID begins

				string formId = ReadChunkId(reader);
				if (string.IsNullOrEmpty(formId))
				{
					if (reader.BaseStream.Position >= catContentsDeclaredEndOffset) break; // Legitimate end if at or past boundary
					Console.WriteLine($"Warning: Null/empty chunk ID in CAT at {currentFormStartInFile}. Assuming end of CAT or padding.");
					break;
				}

				if (formId != "FORM")
				{
					Console.WriteLine($"Warning: Expected 'FORM' in CAT chunk at {currentFormStartInFile}, got '{formId}'. Stopping CAT parse.");
					reader.BaseStream.Seek(catContentsDeclaredEndOffset, SeekOrigin.Begin); // Go to end of CAT content
					return;
				}

				if (reader.BaseStream.Position + 4 > catContentsDeclaredEndOffset)
				{
					Console.WriteLine("Warning: CAT chunk ended prematurely when reading FORM length.");
					reader.BaseStream.Seek(catContentsDeclaredEndOffset, SeekOrigin.Begin); return;
				}
				uint formDeclaredDataLength = ReadBigEndianUInt32(reader); // Length of FORM's data (Type + XMID Contents)
				long formDataStartInFile = reader.BaseStream.Position;

				// Check if this FORM (ID + Length + Data) fits within CAT
				// Total size of this FORM chunk: 4 (ID) + 4 (LengthField) + formDeclaredDataLength + (padding if formDeclaredDataLength is odd)
				long formOverallDataEnd = formDataStartInFile + formDeclaredDataLength;
				if (formOverallDataEnd > catContentsDeclaredEndOffset)
				{
					Console.WriteLine($"Warning: FORM chunk (ID: {formId}, DeclaredDataLength: {formDeclaredDataLength}) in CAT at {currentFormStartInFile} data section exceeds CAT bounds ({catContentsDeclaredEndOffset}). Stopping CAT parse.");
					reader.BaseStream.Seek(catContentsDeclaredEndOffset, SeekOrigin.Begin); return;
				}


				if (formDeclaredDataLength < 4) // FORM data must be at least 4 for its type
				{
					Console.WriteLine($"Warning: FORM in CAT at {currentFormStartInFile} has data length {formDeclaredDataLength}, too short for a FORM type. Skipping this FORM.");
					reader.BaseStream.Seek(formDataStartInFile + formDeclaredDataLength, SeekOrigin.Begin); // Skip its declared data
				}
				else
				{
					string formType = ReadChunkId(reader); // Should be XMID
					if (formType == "XMID")
					{
						Console.WriteLine($"Parsing FORM XMID {songsFoundInCat + 1} from CAT chunk (Content length: {formDeclaredDataLength - 4}).");
						// Pass length of XMID's *contents* (after its type "XMID")
						ParseXmidForm(reader, formDeclaredDataLength - 4);
						songsFoundInCat++;
					}
					else
					{
						Console.WriteLine($"Warning: Expected 'XMID' FORM type in CAT at {currentFormStartInFile}, got '{formType}'. Skipping this FORM's content.");
						// Seek to the end of this FORM's data part
						reader.BaseStream.Seek(formDataStartInFile + formDeclaredDataLength, SeekOrigin.Begin);
					}
				}

				// After ParseXmidForm or skipping, reader is at the end of the FORM's data.
				// Now apply IFF padding for this FORM chunk's data.
				if (formDeclaredDataLength % 2 != 0)
				{
					if (reader.BaseStream.Position < catContentsDeclaredEndOffset) reader.ReadByte(); // Consume pad
					else if (reader.BaseStream.Position == catContentsDeclaredEndOffset) { /* Pad was last byte */ }
					else { Console.WriteLine($"Warning: Expected pad for FORM in CAT but beyond CAT content end."); break; }
				}
			}

			if (expectedSongCount != -1 && songsFoundInCat != expectedSongCount)
			{
				Console.WriteLine($"Warning: XDIR INFO listed {expectedSongCount} songs, but {songsFoundInCat} FORMs found in CAT.");
			}
			// Ensure reader is at the end of CAT content
			if (reader.BaseStream.Position < catContentsDeclaredEndOffset)
				reader.BaseStream.Seek(catContentsDeclaredEndOffset, SeekOrigin.Begin);
		}


		private void ParseXmidForm(BinaryReader reader, uint formLength)
		{
			long formEndOffset = reader.BaseStream.Position + formLength;
			while (reader.BaseStream.Position < formEndOffset)
			{
				string chunkId = ReadChunkId(reader);
				if (string.IsNullOrEmpty(chunkId) && reader.BaseStream.Position == formEndOffset) break;
				if (string.IsNullOrEmpty(chunkId))
				{
					Console.WriteLine($"Warning: Could not read sub-chunk ID within XMID at offset {reader.BaseStream.Position}.");
					break;
				}

				uint chunkLength = ReadBigEndianUInt32(reader);
				long chunkDataStartOffset = reader.BaseStream.Position;

				if (chunkDataStartOffset + chunkLength > formEndOffset)
				{
					Console.WriteLine($"Warning: Sub-chunk '{chunkId}' length {chunkLength} exceeds XMID FORM bounds. Skipping.");
					break;
				}

				switch (chunkId)
				{
					case "EVNT":
						Console.WriteLine($"Parsing EVNT chunk of length {chunkLength}");
						ParseEvntChunk(reader, chunkLength);
						break;
					case "TIMB":
						Console.WriteLine($"Found TIMB chunk of length {chunkLength}. (Parsing logic can be expanded)");
						// Basic TIMB parsing could go here if its structure was clearer from the wiki for patch setup.
						// For now, we primarily rely on Program/Bank changes in EVNT.
						reader.BaseStream.Seek(chunkDataStartOffset + chunkLength, SeekOrigin.Begin); // Skip data
						break;
					case "RBRN":
						Console.WriteLine($"Found RBRN chunk of length {chunkLength}. (Loop/branching - not fully implemented)");
						reader.BaseStream.Seek(chunkDataStartOffset + chunkLength, SeekOrigin.Begin); // Skip data
						break;
					default:
						Console.WriteLine($"Skipping unknown XMID sub-chunk: {chunkId}");
						reader.BaseStream.Seek(chunkDataStartOffset + chunkLength, SeekOrigin.Begin); // Skip data
						break;
				}
				// Ensure we are at the end of the chunk, padded to an even boundary if necessary for IFF
				long expectedEndPos = chunkDataStartOffset + chunkLength;
				if (expectedEndPos % 2 != 0) expectedEndPos++; // IFF chunks are often padded to be an even number of bytes

				if (reader.BaseStream.Position < expectedEndPos && expectedEndPos <= formEndOffset) // check if seeking is safe
				{
					reader.BaseStream.Seek(expectedEndPos, SeekOrigin.Begin);
				}
				else if (reader.BaseStream.Position > expectedEndPos)
				{
					Console.WriteLine($"Warning: Overran chunk {chunkId}. Current pos: {reader.BaseStream.Position}, expected end: {expectedEndPos}");
					// This might indicate a parsing error in the sub-chunk
				}

			}
		}

		private void ParseEvntChunk(BinaryReader reader, uint chunkLength)
		{
			long startOffset = reader.BaseStream.Position;
			long currentXmiTick = 0;

			while (reader.BaseStream.Position < startOffset + chunkLength)
			{
				// 1. Read XMI Delta-Time (sum of 7-bit values)
				long xmiDeltaForThisEvent = 0;
				byte delayByte;
				do
				{
					if (reader.BaseStream.Position >= startOffset + chunkLength)
					{
						Console.WriteLine("Warning: Reached end of EVNT chunk while reading delta-time.");
						return;
					}
					delayByte = reader.ReadByte();
					if ((delayByte & 0x80) != 0)
					{
						xmiDeltaForThisEvent += (delayByte & 0x7F);
					}
				} while ((delayByte & 0x80) != 0);

				currentXmiTick += xmiDeltaForThisEvent;

				// The last 'delayByte' read (with MSB=0) is the actual MIDI status byte
				byte statusByte = delayByte;
				byte channel = (byte)(statusByte & 0x0F);
				byte eventType = (byte)(statusByte & 0xF0);

				var xmiEvent = new ParsedXmiEvent
				{
					AbsoluteXmiTick = currentXmiTick,
					Channel = channel,
					StatusByte = statusByte
				};

				// 2. Read MIDI Event Data based on status byte
				switch (eventType)
				{
					case 0x80: // Note Off (Unlikely in XMI if notes have duration, but handle defensively)
					case 0x90: // Note On
						if (reader.BaseStream.Position + 1 >= startOffset + chunkLength) { Console.WriteLine("Warning: EOF in Note event."); return; }
						xmiEvent.Data1 = reader.ReadByte(); // Note Number
						xmiEvent.Data2 = reader.ReadByte(); // Velocity

						if (eventType == 0x90 && xmiEvent.Data2 > 0) // It's a true Note On
						{
							// Read XMI Note Duration (Standard MIDI VLQ)
							xmiEvent.XmiDuration = ReadMidiVlq(reader);
						}
						else // Note On with velocity 0 is a Note Off, or actual Note Off
						{
							xmiEvent.XmiDuration = 0; // No duration for note off
						}
						break;

					case 0xA0: // Polyphonic Key Pressure
					case 0xB0: // Control Change
					case 0xE0: // Pitch Bend Change
						if (reader.BaseStream.Position + 1 >= startOffset + chunkLength) { Console.WriteLine("Warning: EOF in 2-data-byte event."); return; }
						xmiEvent.Data1 = reader.ReadByte();
						xmiEvent.Data2 = reader.ReadByte();
						break;

					case 0xC0: // Program Change
					case 0xD0: // Channel Pressure
						if (reader.BaseStream.Position >= startOffset + chunkLength) { Console.WriteLine("Warning: EOF in 1-data-byte event."); return; }
						xmiEvent.Data1 = reader.ReadByte();
						xmiEvent.Data2 = 0; // Not used
						break;

					case 0xF0: // System Messages
										 // Sysex F0 ... F7
						if (statusByte == 0xF0) // SysEx Start
						{
							// Read until F7 or end of chunk
							var sysexData = new List<byte>();
							sysexData.Add(statusByte);
							byte sxByte;
							do
							{
								if (reader.BaseStream.Position >= startOffset + chunkLength) { Console.WriteLine("Warning: EOF in SysEx."); return; }
								sxByte = reader.ReadByte();
								sysexData.Add(sxByte);
							} while (sxByte != 0xF7 && reader.BaseStream.Position < startOffset + chunkLength);
							// For simplicity, we're not storing full SysEx in ParsedXmiEvent here
							// but you could add a byte[] property for it.
							Console.WriteLine($"Read SysEx event of length {sysexData.Count}");
							// To skip adding this complex event to the simple ParsedXmiEvent list:
							continue;
						}
						else if (statusByte == 0xFF) // Meta Event (not typical in raw XMI EVNT, but possible)
						{
							if (reader.BaseStream.Position + 1 >= startOffset + chunkLength) { Console.WriteLine("Warning: EOF in Meta Event type."); return; }
							byte metaType = reader.ReadByte();
							long metaLength = ReadMidiVlq(reader);
							if (reader.BaseStream.Position + metaLength > startOffset + chunkLength) { Console.WriteLine("Warning: EOF in Meta Event data."); return; }
							reader.ReadBytes((int)metaLength); // Skip meta data for now
							Console.WriteLine($"Read Meta event type {metaType} length {metaLength}");
							continue; // Skip adding to simple event list
						}
						// Other F-status messages (F1, F2, F3, F6, F8, FA, FB, FC, FE) are single bytes or have specific lengths
						// These are less common in XMI EVNT data usually focused on channel messages.
						// For now, we'll assume they are single byte if not F0 or FF.
						Console.WriteLine($"Read System Common/Realtime event: {statusByte:X2}");
						continue; // Skip adding to simple event list

					default:
						Console.WriteLine($"Warning: Unknown event type {eventType:X2} with status {statusByte:X2} at {reader.BaseStream.Position - 1}. Skipping event.");
						// Attempt to recover or stop. For now, we'll try to skip what might be a malformed event.
						// This part is tricky without knowing the exact structure of potential errors.
						continue; // Skip this event
				}
				Events.Add(xmiEvent);
			}
		}

		// Helper to read a 4-char string as chunk ID
		private string ReadChunkId(BinaryReader reader)
		{
			if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) throw new EndOfStreamException("Unexpected end of stream while reading chunk ID.");
			char[] chars = reader.ReadChars(4);
			return new string(chars);
		}

		// Helper to read a Big Endian UInt32
		private uint ReadBigEndianUInt32(BinaryReader reader)
		{
			if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) return 0;
			byte[] bytes = reader.ReadBytes(4);
			Array.Reverse(bytes); // Convert big endian to little endian
			return BitConverter.ToUInt32(bytes, 0);
		}

		private ushort ReadBigEndianUInt16(BinaryReader reader)
		{
			if (reader.BaseStream.Position + 2 > reader.BaseStream.Length) throw new EndOfStreamException("Attempted to read UInt16 beyond stream end.");
			byte[] bytes = reader.ReadBytes(2);
			Array.Reverse(bytes);
			return BitConverter.ToUInt16(bytes, 0);
		}

		// Reads a standard MIDI Variable Length Quantity
		private long ReadMidiVlq(BinaryReader reader)
		{
			long value = 0;
			byte b;
			do
			{
				if (reader.BaseStream.Position >= reader.BaseStream.Length)
					throw new EndOfStreamException("Unexpected end of stream while reading MIDI VLQ.");
				b = reader.ReadByte();
				value = (value << 7) + (b & 0x7F);
			} while ((b & 0x80) != 0);
			return value;
		}
	}

	// ---------------
	// XMI to MIDI Converter
	// ---------------
	public class XmiToMidiConverter
	{
		private readonly XmiReader _xmiReader;
		private MidiFile _midiFile;

		// MIDI Ticks Per Quarter Note for the output MIDI file.
		private readonly short _midiTpQn;
		private readonly double _targetBpm; // Added for adjustable tempo

		public XmiToMidiConverter(XmiReader xmiReader, short outputMidiTpQn = XmiReader.DEFAULT_MIDI_TPQN, double targetBpm = 120.0)
		{
			_xmiReader = xmiReader;
			_midiFile = new MidiFile();
			_midiTpQn = outputMidiTpQn;
			_targetBpm = targetBpm; // Store the target BPM
			_midiFile.TimeDivision = new TicksPerQuarterNoteTimeDivision(_midiTpQn);
		}

		public bool Convert(string outputMidiFilePath)
		{
			if (_xmiReader.Events == null || !_xmiReader.Events.Any())
			{
				Console.WriteLine("No XMI events to convert.");
				return false;
			}

			// Sort events by absolute XMI tick, then by type (e.g., Note Off before Note On at same tick)
			var sortedXmiEvents = _xmiReader.Events
													.OrderBy(e => e.AbsoluteXmiTick)
													.ThenBy(e => (e.StatusByte & 0xF0) == 0x90 && e.Data2 == 0 ? 0 : 1) // Prioritize generated Note Offs
													.ToList();

			// Group events by channel to create MIDI tracks
			// Standard MIDI Format 1: Track 0 for tempo/meta, subsequent tracks for channels
			var eventsByChannel = new Dictionary<int, List<MidiEvent>>();

			// Create Track 0 for tempo and other global meta-events
			var tempoTrack = new TrackChunk();
			tempoTrack.Events.Add(new SetTempoEvent(125000)); // Default: 120 BPM (500,000 microseconds per quarter note)
			tempoTrack.Events.Add(new TimeSignatureEvent(4, 4)); // Default: 4/4
			_midiFile.Chunks.Add(tempoTrack);


			// Process XMI events into MIDI events, mapping XMI ticks to MIDI ticks
			// For simplicity, this example assumes a 1:1 XMI tick to MIDI tick mapping.
			// The actual timing is then controlled by MIDI_TPQN and SetTempoEvents.
			// If XMI has a fixed ticks-per-second, a scaling factor would be needed here.

			var allTimedMidiEvents = new List<(long absoluteMidiTick, byte channel, MidiEvent midiEvent)>();

			foreach (var xmiEvent in sortedXmiEvents)
			{
				long absoluteMidiTick = xmiEvent.AbsoluteXmiTick; // Direct mapping for this example
				byte channel = xmiEvent.Channel;
				MidiEvent? currentMidiEvent = null;

				if (xmiEvent.IsNoteEvent)
				{
					// XMI Note On with duration -> MIDI Note On + MIDI Note Off
					if (xmiEvent.Data2 > 0) // True Note On
					{
						var noteOn = new NoteOnEvent((SevenBitNumber)xmiEvent.Data1, (SevenBitNumber)xmiEvent.Data2)
						{
							Channel = (FourBitNumber)channel
						};
						allTimedMidiEvents.Add((absoluteMidiTick, channel, noteOn));

						long noteOffAbsoluteTick = absoluteMidiTick + xmiEvent.XmiDuration;
						var noteOff = new NoteOffEvent((SevenBitNumber)xmiEvent.Data1, (SevenBitNumber)0) // Velocity 0 for note off
						{
							Channel = (FourBitNumber)channel
						};
						allTimedMidiEvents.Add((noteOffAbsoluteTick, channel, noteOff));
					}
					else // XMI Note On with velocity 0 (effectively a Note Off)
					{
						var noteOff = new NoteOffEvent((SevenBitNumber)xmiEvent.Data1, (SevenBitNumber)0)
						{
							Channel = (FourBitNumber)channel
						};
						allTimedMidiEvents.Add((absoluteMidiTick, channel, noteOff));
					}
				}
				else if (xmiEvent.IsProgramChangeEvent)
				{
					currentMidiEvent = new ProgramChangeEvent((SevenBitNumber)xmiEvent.Data1)
					{
						Channel = (FourBitNumber)channel
					};
				}
				else if (xmiEvent.IsControlChangeEvent)
				{
					// Handle XMI specific CC#114 (AIL_TIMB_BNK) for Bank Select
					if (xmiEvent.Data1 == 114) // AIL_TIMB_BNK
					{
						// Standard MIDI uses CC#0 for Bank Select MSB and CC#32 for LSB.
						// XMI's CC#114 value might be a direct bank number.
						// Assuming it's a single bank number, usually split into MSB/LSB for MIDI.
						// This part might need adjustment based on how bank is stored in XMI CC#114.
						// For simplicity, let's assume CC#114 value is bank MSB, and LSB is 0.
						var bankMsbEvent = new ControlChangeEvent((SevenBitNumber)0, (SevenBitNumber)xmiEvent.Data2)
						{
							Channel = (FourBitNumber)channel
						};
						allTimedMidiEvents.Add((absoluteMidiTick, channel, bankMsbEvent));
						// Optionally add LSB if needed:
						// var bankLsbEvent = new ControlChangeEvent((SevenBitNumber)32, (SevenBitNumber)0) { Channel = (FourBitNumber)channel };
						// allTimedMidiEvents.Add((absoluteMidiTick, channel, bankLsbEvent));
					}
					else
					{
						currentMidiEvent = new ControlChangeEvent((SevenBitNumber)xmiEvent.Data1, (SevenBitNumber)xmiEvent.Data2)
						{
							Channel = (FourBitNumber)channel
						};
					}
				}
				// Add other event type conversions (Pitch Bend, Channel Pressure, etc.)
				else
				{
					// Potentially other event types like pitch bend, channel pressure
					switch (xmiEvent.StatusByte & 0xF0)
					{
						case 0xE0: // Pitch Bend
											 // Data1 is LSB, Data2 is MSB for MIDI pitch bend
											 // Combine Data1 and Data2 from XMI to form the 14-bit MIDI pitch bend value
							ushort pitchBendValue = (ushort)((xmiEvent.Data2 << 7) | xmiEvent.Data1);
							currentMidiEvent = new PitchBendEvent(pitchBendValue)
							{
								Channel = (FourBitNumber)channel
							};
							break;
						case 0xD0: // Channel Pressure
							currentMidiEvent = new ChannelAftertouchEvent((SevenBitNumber)xmiEvent.Data1)
							{
								Channel = (FourBitNumber)channel
							};
							break;
							// Polyphonic aftertouch (0xA0) could be added if needed
					}
				}

				if (currentMidiEvent != null)
				{
					allTimedMidiEvents.Add((absoluteMidiTick, channel, currentMidiEvent));
				}
			}

			// Sort all generated MIDI events by time, then by type (e.g. Note Off before Note On)
			allTimedMidiEvents = allTimedMidiEvents
					.OrderBy(e => e.absoluteMidiTick)
					.ThenBy(e => e.midiEvent is NoteOffEvent ? 0 : (e.midiEvent is ControlChangeEvent || e.midiEvent is ProgramChangeEvent ? 1 : 2)) // Ensure control changes are early
					.ToList();

			// Group by channel and create tracks
			var groupedByChannel = allTimedMidiEvents.GroupBy(e => e.channel);

			foreach (var channelGroup in groupedByChannel)
			{
				var track = new TrackChunk();
				long lastTickInTrack = 0;
				byte currentChannel = channelGroup.Key;

				// Add initial instrument from TIMB if available and desired (not implemented deeply here)
				// Or rely on Program/Bank changes within the event stream.

				foreach (var timedEvent in channelGroup)
				{
					timedEvent.midiEvent.DeltaTime = timedEvent.absoluteMidiTick - lastTickInTrack;
					track.Events.Add(timedEvent.midiEvent);
					lastTickInTrack = timedEvent.absoluteMidiTick;
				}
				_midiFile.Chunks.Add(track);
			}

			try
			{
				_midiFile.Write(outputMidiFilePath, true, MidiFileFormat.MultiTrack); // Format 1
				Console.WriteLine($"Successfully converted XMI to MIDI: {outputMidiFilePath}");
				return true;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error writing MIDI file: {ex.Message}");
				return false;
			}
		}
	}

	// ---------------
	// XMI to MIDI Converter
	// ---------------
	public class XmiToMidiConverter1
	{
		private readonly XmiReader _xmiReader;
		private MidiFile _midiFile;
		private readonly short _midiTpQn;
		private readonly double _targetBpm; // Added for adjustable tempo

		public XmiToMidiConverter1(XmiReader xmiReader, short outputMidiTpQn = XmiReader.DEFAULT_MIDI_TPQN, double targetBpm = 120.0)
		{
			_xmiReader = xmiReader;
			_midiFile = new MidiFile();
			_midiTpQn = outputMidiTpQn;
			_targetBpm = targetBpm; // Store the target BPM
			_midiFile.TimeDivision = new TicksPerQuarterNoteTimeDivision(_midiTpQn);
		}

		public bool Convert(string outputMidiFilePath)
		{
			if (_xmiReader.Events == null || !_xmiReader.Events.Any())
			{
				Console.WriteLine("No XMI events to convert.");
				return false;
			}

			var allTimedMidiEvents = new List<(long absoluteMidiTick, byte channel, MidiEvent midiEvent)>();

			foreach (var xmiEvent in _xmiReader.Events) // Process potentially merged events
			{
				long absoluteMidiTick = xmiEvent.AbsoluteXmiTick;
				byte channel = xmiEvent.Channel;
				MidiEvent? currentMidiEvent = null;

				if (xmiEvent.IsNoteEvent)
				{
					if (xmiEvent.Data2 > 0) // True Note On
					{
						var noteOn = new NoteOnEvent((SevenBitNumber)xmiEvent.Data1, (SevenBitNumber)xmiEvent.Data2)
						{ Channel = (FourBitNumber)channel };
						allTimedMidiEvents.Add((absoluteMidiTick, channel, noteOn));

						long noteOffAbsoluteTick = absoluteMidiTick + xmiEvent.XmiDuration;
						var noteOff = new NoteOffEvent((SevenBitNumber)xmiEvent.Data1, (SevenBitNumber)0)
						{ Channel = (FourBitNumber)channel };
						allTimedMidiEvents.Add((noteOffAbsoluteTick, channel, noteOff));
					}
					else // XMI Note On with velocity 0 (Note Off) or explicit XMI Note Off (0x8n)
					{
						var noteOff = new NoteOffEvent((SevenBitNumber)xmiEvent.Data1, (SevenBitNumber)0)
						{ Channel = (FourBitNumber)channel };
						allTimedMidiEvents.Add((absoluteMidiTick, channel, noteOff));
					}
				}
				else if (xmiEvent.IsProgramChangeEvent)
				{
					currentMidiEvent = new ProgramChangeEvent((SevenBitNumber)xmiEvent.Data1)
					{ Channel = (FourBitNumber)channel };
				}
				else if (xmiEvent.IsControlChangeEvent)
				{
					if (xmiEvent.Data1 == 114) // AIL_TIMB_BNK
					{
						var bankMsbEvent = new ControlChangeEvent((SevenBitNumber)0, (SevenBitNumber)xmiEvent.Data2) // CC#0 Bank Select MSB
						{ Channel = (FourBitNumber)channel };
						allTimedMidiEvents.Add((absoluteMidiTick, channel, bankMsbEvent));
						// Optional: Add LSB if XMI implies it or if a default is needed
						// var bankLsbEvent = new ControlChangeEvent((SevenBitNumber)32, (SevenBitNumber)0) { Channel = (FourBitNumber)channel };
						// allTimedMidiEvents.Add((absoluteMidiTick, channel, bankLsbEvent));
					}
					else
					{
						currentMidiEvent = new ControlChangeEvent((SevenBitNumber)xmiEvent.Data1, (SevenBitNumber)xmiEvent.Data2)
						{ Channel = (FourBitNumber)channel };
					}
				}
				else // Other event types
				{
					switch (xmiEvent.StatusByte & 0xF0)
					{
						case 0xE0: // Pitch Bend
							ushort pitchBendValue = (ushort)((xmiEvent.Data2 << 7) | xmiEvent.Data1);
							currentMidiEvent = new PitchBendEvent(pitchBendValue)
							{ Channel = (FourBitNumber)channel };
							break;
						case 0xD0: // Channel Pressure
							currentMidiEvent = new ChannelAftertouchEvent((SevenBitNumber)xmiEvent.Data1)
							{ Channel = (FourBitNumber)channel };
							break;
						case 0xA0: // Polyphonic Key Pressure
							currentMidiEvent = new NoteAftertouchEvent((SevenBitNumber)xmiEvent.Data1, (SevenBitNumber)xmiEvent.Data2)
							{ Channel = (FourBitNumber)channel };
							break;
					}
				}

				if (currentMidiEvent != null)
				{
					allTimedMidiEvents.Add((absoluteMidiTick, channel, currentMidiEvent));
				}
			}

			allTimedMidiEvents = allTimedMidiEvents
					.OrderBy(e => e.absoluteMidiTick)
					.ThenBy(e => e.midiEvent is NoteOffEvent ? 0 : (e.midiEvent is ControlChangeEvent || e.midiEvent is ProgramChangeEvent ? 1 : 2))
					.ToList();

			var tempoTrack = new TrackChunk();
			// Calculate microseconds per quarter note based on target BPM
			long microsecondsPerQuarterNote = (long)(60000000.0 / _targetBpm);
			tempoTrack.Events.Add(new SetTempoEvent(microsecondsPerQuarterNote));
			Console.WriteLine($"Setting MIDI tempo to {_targetBpm} BPM (Microseconds per quarter note: {microsecondsPerQuarterNote})");


			tempoTrack.Events.Add(new TimeSignatureEvent(4, 4)); // Default: 4/4
			_midiFile.Chunks.Add(tempoTrack);


			var groupedByChannel = allTimedMidiEvents.GroupBy(e => e.channel);

			foreach (var channelGroup in groupedByChannel)
			{
				var track = new TrackChunk();
				long lastTickInTrack = 0;

				foreach (var timedEvent in channelGroup)
				{
					timedEvent.midiEvent.DeltaTime = timedEvent.absoluteMidiTick - lastTickInTrack;
					track.Events.Add(timedEvent.midiEvent);
					lastTickInTrack = timedEvent.absoluteMidiTick;
				}
				_midiFile.Chunks.Add(track);
			}

			// if (!tempoTrack.Events.OfType<EndOfTrackEvent>().Any())
			// {
			// 	long lastTempoTrackTick = 0;
			// 	if (tempoTrack.Events.Any())
			// 	{
			// 		// Get the time of the last event already in the tempo track
			// 		// var eventsCollection = new EventsCollection();
			// 		// eventsCollection.AddRange(tempoTrack.Events);
			// 		// lastTempoTrackTick = eventsCollection.GetTimedEvents().LastOrDefault()?.Time ?? 0;
			// 	}

			// 	long songDurationTicks = 0;
			// 	if (allTimedMidiEvents.Any())
			// 	{
			// 		songDurationTicks = allTimedMidiEvents.Max(e => e.absoluteMidiTick);
			// 	}
			// }


			try
			{
				_midiFile.Write(outputMidiFilePath, true, MidiFileFormat.MultiTrack);
				Console.WriteLine($"Successfully converted XMI to MIDI: {outputMidiFilePath}");
				return true;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error writing MIDI file: {ex.Message}");
				return false;
			}
		}
	}
}

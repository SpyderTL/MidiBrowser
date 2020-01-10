using System;
using System.Collections;
using System.IO;
using System.Linq;

namespace MidiBrowser
{
	internal class MidiFile : IFolder, IProperties
	{
		public string Path;

		public IEnumerable Items
		{
			get
			{
				using (var stream = System.IO.File.OpenRead(Path))
				using (var reader = new System.IO.BinaryReader(stream))
				{
					while (stream.Position < stream.Length)
					{
						// Read Chunk
						var chunkPosition = stream.Position;

						var chunkType = new string(reader.ReadChars(4));
						var chunkLength = BitConverter.ToUInt32(reader.ReadBytes(4).Reverse().ToArray(), 0);

						switch (chunkType)
						{
							case "MThd":
								yield return new MThdChunk
								{
									File = this,
									Position = chunkPosition
								};
								stream.Seek(chunkLength, System.IO.SeekOrigin.Current);
								break;

							case "MTrk":
								yield return new MTrkChunk
								{
									File = this,
									Position = chunkPosition
								};
								stream.Seek(chunkLength, System.IO.SeekOrigin.Current);
								break;

							default:
								stream.Seek(chunkLength, System.IO.SeekOrigin.Current);
								yield return chunkType;
								break;
						}
					}
				}
			}
		}

		public object Properties => new { Path };

		internal class MThdChunk : IProperties
		{
			internal MidiFile File;
			internal long Position;

			public object Properties
			{
				get
				{
					using (var stream = System.IO.File.OpenRead(File.Path))
					using (var reader = new System.IO.BinaryReader(stream))
					{
						stream.Seek(Position + 8, System.IO.SeekOrigin.Begin);

						return new
						{
							Format = BitConverter.ToUInt16(reader.ReadBytes(2).Reverse().ToArray(), 0),
							Tracks = BitConverter.ToUInt16(reader.ReadBytes(2).Reverse().ToArray(), 0),
							Division = BitConverter.ToUInt16(reader.ReadBytes(2).Reverse().ToArray(), 0)
						};
					}
				}
			}

			public override string ToString() => "MThd";
		}

		internal class MTrkChunk : IFolder, IMenu
		{
			internal MidiFile File;
			internal long Position;

			public string[] MenuItems => new[] { "Export" };

			public void Execute(string menuItem)
			{
				switch (menuItem)
				{
					case "Export":
						Export();
						break;
				}
			}

			private void Export()
			{
				using (var stream = System.IO.File.Create("export.xml"))
				using (var writer = new System.IO.StreamWriter(stream))
				{
					var delay = 0UL;

					foreach(var item in Items)
					{
						if (item is NoteOn)
						{
							delay += ((NoteOn)item).Delay;

							writer.WriteLine("<hex>" + delay.ToString("x4") + "</hex>");
							writer.WriteLine("<hex>" + ((NoteOn)item).Channel.ToString("x2") + "</hex>");
							writer.WriteLine("<hex>" + ((NoteOn)item).Key.ToString("x2") + "</hex>");
							writer.WriteLine("<hex>" + ((NoteOn)item).Velocity.ToString("x2") + "</hex>");
							writer.WriteLine();

							delay = 0UL;
						}
						else if (item.ToString() == "End of Track")
						{
							writer.WriteLine("<hex>f2</hex>");
						}
						else if(item is ControlChange)
						{
							delay += ((ControlChange)item).Delay;
						}
					}

					writer.Flush();
				}
			}

			public IEnumerable Items
			{
				get
				{
					using (var stream = System.IO.File.OpenRead(File.Path))
					using (var reader = new System.IO.BinaryReader(stream))
					{
						stream.Seek(Position + 4, System.IO.SeekOrigin.Begin);

						var chunkLength = BitConverter.ToUInt32(reader.ReadBytes(4).Reverse().ToArray(), 0);

						var lastStatus = (byte)0;

						while (stream.Position < Position + chunkLength + 8)
						{
							var delay = ReadQuantity(reader);
							var status = reader.ReadByte();

							if ((status & 0x80) == 0)
							{
								status = lastStatus;
								stream.Seek(-1, SeekOrigin.Current);
							}

							switch (status)
							{
								case 0xff:
									yield return ReadMetaEvent(reader, delay);
									break;

								case 0xf0:
									yield return ReadSystemExclusiveEvent(reader, delay);
									break;

								default:
									yield return ReadChannelEvent(reader, delay, status);
									break;
							}

							lastStatus = status;
						}
					}
				}
			}

			private object ReadChannelEvent(BinaryReader reader, ulong delay, byte status)
			{
				var messageType = status >> 4;
				var channel = (byte)(status & 0xf);

				switch (messageType)
				{
					case 0x8:
						return new NoteOff { Delay = delay, Channel = channel, Key = reader.ReadByte(), Velocity = reader.ReadByte() };

					case 0x9:
						return new NoteOn { Delay = delay, Channel = channel, Key = reader.ReadByte(), Velocity = reader.ReadByte() };

					case 0xa:
						return new PolyphonicKeyPressure { Delay = delay, Channel = channel, Key = reader.ReadByte(), Velocity = reader.ReadByte() };

					case 0xb:
						var value1 = reader.ReadByte();
						var value2 = reader.ReadByte();

						switch (value1)
						{
							case 0x78:
								return "All Sound Off";

							case 0x79:
								return "Reset All Controllers";

							case 0x7a:
								return "Local Control";

							case 0x7b:
								return "All Notes Off";

							case 0x7c:
								return "Omni Mode Off";

							case 0x7d:
								return "Omni Mode On";

							case 0x7e:
								return "Mono Mode On";

							case 0x7f:
								return "Poly Mode On";

							default:
								return new ControlChange { Delay = delay, Channel = channel, Controller = value1, Value = value2 };
						}

					case 0xc:
						return new ProgramChange { Delay = delay, Channel = channel, Patch = reader.ReadByte(), Unknown = 0 };

					case 0xd:
						return new ChannelPressure { Delay = delay, Channel = channel, Velocity = reader.ReadByte(), Unknown = 0 };

					case 0xe:
						return new PitchBendChange { Delay = delay, Channel = channel, Value = BitConverter.ToUInt16(reader.ReadBytes(2).Reverse().ToArray(), 0) };

					default:
						value1 = reader.ReadByte();
						value2 = reader.ReadByte();
						return "Unknown";
				}
			}

			private object ReadMetaEvent(BinaryReader reader, ulong delay)
			{
				var type = reader.ReadByte();
				var length = (int)ReadQuantity(reader);

				switch (type)
				{
					case 0x00:
						reader.ReadBytes(length);
						return "Sequence Number";

					case 0x01:
						var text = new string(reader.ReadChars(length));
						//reader.ReadBytes(length);
						return "Text Event: " + text;

					case 0x02:
						var copyright = new string(reader.ReadChars(length));
						//reader.ReadBytes(length);
						return "Copyright Notice: " + copyright;

					case 0x03:
						var name = new string(reader.ReadChars(length));
						return "Sequence/Track Name: " + name;

					case 0x04:
						var instrument = new string(reader.ReadChars(length));
						//reader.ReadBytes(length);
						return "Instrument Name: " + instrument;

					case 0x05:
						var lyric = new string(reader.ReadChars(length));
						//reader.ReadBytes(length);
						return "Lyric: " + lyric;

					case 0x06:
						reader.ReadBytes(length);
						return "Marker";

					case 0x07:
						reader.ReadBytes(length);
						return "Cue Point";

					case 0x20:
						reader.ReadBytes(length);
						return "MIDI Channel Prefix";

					case 0x21:
						var port = reader.ReadByte();
						return "MIDI Port Prefix:" + port;

					case 0x2F:
						reader.ReadBytes(length);
						return "End Of Track";

					case 0x51:
						var data = reader.ReadBytes(length);
						var tempo = (data[0] << 16) | (data[1] << 8) | data[2];

						return "Set Tempo: " + tempo;

					case 0x54:
						reader.ReadBytes(length);
						return "SMTPE Offset";

					case 0x58:
						var numerator = reader.ReadByte();
						var denominator = reader.ReadByte();
						var midiClocksPerTick = reader.ReadByte();
						var thirtySecondNotes = reader.ReadByte();
						return "Time Signature: " + numerator + "/" + denominator + " (" + midiClocksPerTick + ") [" + thirtySecondNotes + "]";

					case 0x59:
						reader.ReadBytes(length);
						return "Key Signature";

					case 0x7f:
						reader.ReadBytes(length);
						return "Sequencer Event";
				}

				return "Unknown Meta-Event";
			}

			private object ReadSystemExclusiveEvent(BinaryReader reader, ulong delay)
			{
				var length = (int)ReadQuantity(reader);

				reader.ReadBytes(length);
				return "System Exclusive";
			}

			private ulong ReadQuantity(BinaryReader reader)
			{
				var quantity = 0UL;

				while (true)
				{
					var data = reader.ReadByte();

					var value = (ulong)(data & 0x7f);

					quantity <<= 7;
					quantity |= value;

					if ((data & 0x80) == 0)
						break;
				}

				return quantity;
			}

			public override string ToString() => "MTrk";

			internal class NoteOff : IProperties
			{
				public ulong Delay;
				public byte Channel;
				public byte Key;
				public byte Velocity;

				public object Properties => new { Delay, Channel, Key, Velocity };

				public override string ToString() => $"Note On: Delay {Delay} Channel {Channel} Key {Key} Velocity {Velocity}";
			}

			internal class NoteOn : IProperties
			{
				public ulong Delay;
				public byte Channel;
				public byte Key;
				public byte Velocity;

				public object Properties => new { Delay, Channel, Key, Velocity };

				public override string ToString() => $"Note On: Delay {Delay} Channel {Channel} Key {Key} Velocity {Velocity}";
			}

			internal class PolyphonicKeyPressure : IProperties
			{
				public ulong Delay;
				public byte Channel;
				public byte Key;
				public byte Velocity;

				public object Properties => new { Delay, Channel, Key, Velocity };

				public override string ToString() => $"Polyphonic Key Pressure: Delay {Delay} Channel {Channel} Key {Key} Velocity {Velocity}";
			}

			internal class ControlChange : IProperties
			{
				public ulong Delay;
				public byte Channel;
				public byte Controller;
				public byte Value;

				public object Properties => new { Delay, Channel, Controller, Value };

				public override string ToString() => $"Control Change: Delay {Delay} Channel {Channel} Controller {Controller} Value {Value}";
			}

			internal class ProgramChange : IProperties
			{
				public ulong Delay;
				public byte Channel;
				public byte Patch;
				public byte Unknown;

				public object Properties => new { Delay, Channel, Patch, Unknown };

				public override string ToString() => $"Program Change: Delay {Delay} Channel {Channel} Patch {Patch} Unknown {Unknown}";
			}

			internal class ChannelPressure : IProperties
			{
				public ulong Delay;
				public byte Channel;
				public byte Velocity;
				public byte Unknown;

				public object Properties => new { Delay, Channel, Velocity, Unknown };

				public override string ToString() => $"Channel Pressure: Delay {Delay} Channel {Channel} Velocity {Velocity} Unknown {Unknown}";
			}

			internal class PitchBendChange : IProperties
			{
				public ulong Delay;
				public byte Channel;
				public ushort Value;

				public object Properties => new { Delay, Channel, Value };

				public override string ToString() => $"Pitch Bend Change: Delay {Delay} Channel {Channel} Value {Value}";
			}
		}
	}
}
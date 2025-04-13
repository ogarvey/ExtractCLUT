using System.IO;
using System.Text;

namespace ExtractCLUT.Games.PC
{
    public static class TerraGlyph
    {
        public static void TinyToon(string fname)
        {
            if (!File.Exists(fname)) return;

            byte[] fp = File.ReadAllBytes(fname);
            string dir = fname.Replace('.', '_');

            Directory.CreateDirectory(dir);
            StringBuilder txt = new StringBuilder();

            DirLoop(fp, 0, dir, txt);

            File.WriteAllText(Path.Combine(dir, "tgrlist.txt"), txt.ToString());
        }

        static void DirLoop(byte[] fp, int pos, string dir, StringBuilder txt)
        {
            int cnt = BitConverter.ToInt32(fp, pos);
            pos += 4;

            for (int si = 0; si < cnt; si++)
            {
                int subPos = pos + si * 0x0F;

                byte ty = fp[subPos];
                int st = BitConverter.ToInt32(fp, subPos + 1);
                ushort id = BitConverter.ToUInt16(fp, subPos + 5);
                int ps = BitConverter.ToInt32(fp, subPos + 7);
                int sz = BitConverter.ToInt32(fp, subPos + 11);

                string fn = Path.Combine(dir, $"{si:D4}-{st:x}-{id}");

                string log = $"{ps:x8} , {sz:x8} , {st:x8} , {id:x4} , {fn}.{ty:x}";
                Console.WriteLine(log);
                txt.AppendLine(log);

                byte[] data = new byte[sz];
                Array.Copy(fp, ps, data, 0, sz);

                switch (ty)
                {
                    case 0:
                        DirLoop(fp, ps, fn, txt);
                        break;

                    case 1:
                        for (int pi = 0; pi < sz; pi += 4)
                        {
                            byte r = data[pi + 2];
                            byte g = data[pi + 1];
                            byte b = data[pi + 0];
                            data[pi + 0] = r;
                            data[pi + 1] = g;
                            data[pi + 2] = b;
                            data[pi + 3] = 0xFF;
                        }
                        File.WriteAllBytes(fn + ".pal", data);
                        break;

                    case 2:
                        File.WriteAllBytes(fn + ".img", data);
                        break;

                    case 3:
                        File.WriteAllBytes(fn + ".snd", data);
                        break;

                    default:
                        File.WriteAllBytes(fn + $".{ty}", data);
                        break;
                }
            }
        }
    }
}

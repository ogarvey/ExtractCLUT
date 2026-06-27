using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ExtractCLUT.Games.PSX.Alundra;

class Program
{
    static void Main(string[] args)
    {
        var alundraDataFile = @"C:\Dev\Gaming\Sony\PSX\Games\Alundra\DATA\DATAS.BIN";
        var datas = File.ReadAllBytes(alundraDataFile);
        var segments = AlundraHelper.SplitDatasBin(datas);

        AlundraHelper.DatasSegment seg = null;
        foreach (var s in segments)
        {
            if (AlundraHelper.Classify(s.Data) == AlundraHelper.SegmentKind.Container)
            {
                uint mapId = s.Data.Length >= 0x20 ? BitConverter.ToUInt32(s.Data, 0x1c) : 0;
                if (mapId == 324)
                {
                    seg = s;
                    break;
                }
            }
        }

        var subs = AlundraHelper.SplitContainer(seg.Data);
        var sub3 = subs[3];

        var baseDir = @"C:\Dev\Gaming\Sony\PSX\Games\Alundra\Extracted\";

        for (int i = 0; i < subs.Count; i++)
        {
            byte[] d = AlundraHelper.IsEz(subs[i]) ? AlundraHelper.DecompressEZ(subs[i]) : subs[i];
            Console.WriteLine($"Sub[{i}]: rawSize={subs[i].Length}, decompSize={d.Length}, pages={d.Length / 32768.0:F2}");
        }

        var report = AlundraHelper.VerifySub3Layout(sub3);
        Console.WriteLine(report);

        var reportPath = Path.Combine(baseDir, "alundra_sub3_report.txt");
        File.WriteAllText(reportPath, report);
        Console.WriteLine($"Report written to {reportPath}");

        var spriteOut = Path.Combine(baseDir, "alundra_sprites_map324");
        AlundraHelper.ExtractSprites(sub3, subs[4], spriteOut, dumpPages: true);
    }
}

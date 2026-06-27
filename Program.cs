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
        var containerMaps = new List<(int segmentIdx, uint mapId)>();
        for (int i = 0; i < segments.Count; i++)
        {
            var s = segments[i];
            if (AlundraHelper.Classify(s.Data) == AlundraHelper.SegmentKind.Container)
            {
                uint mapId = s.Data.Length >= 0x20 ? BitConverter.ToUInt32(s.Data, 0x1c) : 0;
                containerMaps.Add((i, mapId));
                if (mapId == 324)
                {
                    seg = s;
                }
            }
        }
        Console.WriteLine("Map Container segments:");
        foreach (var cm in containerMaps.OrderBy(x => x.mapId))
        {
            Console.WriteLine($"  Segment[{cm.segmentIdx}]: mapId={cm.mapId}");
        }

        var baseDir = @"C:\Dev\Gaming\Sony\PSX\Games\Alundra\Extracted\";

        var resDbData = segments[0].Data;
        if (AlundraHelper.IsEz(resDbData)) resDbData = AlundraHelper.DecompressEZ(resDbData);
        var resDbPath = Path.Combine(baseDir, "alundra_resident_db.bin");
        File.WriteAllBytes(resDbPath, resDbData);
        Console.WriteLine($"Resident DB extracted to {resDbPath} ({resDbData.Length} bytes)");

        var resSpriteData = segments[2].Data;
        if (AlundraHelper.IsEz(resSpriteData)) resSpriteData = AlundraHelper.DecompressEZ(resSpriteData);
        var resSpritePath = Path.Combine(baseDir, "alundra_resident_sprites.bin");
        File.WriteAllBytes(resSpritePath, resSpriteData);
        Console.WriteLine($"Resident sprites extracted to {resSpritePath} ({resSpriteData.Length} bytes)");

        var resReport = AlundraHelper.VerifySub3Layout(resDbData, 256, 128, 128);
        var resReportPath = Path.Combine(baseDir, "alundra_resident_report.txt");
        File.WriteAllText(resReportPath, resReport);
        Console.WriteLine($"Resident DB report written to {resReportPath}");

        var targetMaps = new List<uint> { 321, 322, 323, 324, 325 };
        Console.WriteLine("\nAnalyzing Nirude Chase Rooms (321-325):");
        foreach (var m in targetMaps)
        {
            var targetSeg = containerMaps.FirstOrDefault(x => x.mapId == m);
            if (targetSeg.mapId == m)
            {
                var sSubs = AlundraHelper.SplitContainer(segments[targetSeg.segmentIdx].Data);
                Console.WriteLine($"Map {m} (Segment {targetSeg.segmentIdx}):");
                for (int i = 0; i < sSubs.Count; i++)
                {
                    byte[] d = AlundraHelper.IsEz(sSubs[i]) ? AlundraHelper.DecompressEZ(sSubs[i]) : sSubs[i];
                    Console.WriteLine($"  Sub[{i}]: rawSize={sSubs[i].Length}, decompSize={d.Length}, pages={d.Length / 32768.0:F2}");
                }
            }
        }

        var subs = AlundraHelper.SplitContainer(seg.Data);
        var sub3 = subs[3];
        if (AlundraHelper.IsEz(sub3)) sub3 = AlundraHelper.DecompressEZ(sub3);
        uint w0 = BitConverter.ToUInt32(sub3, 0);
        uint w1 = BitConverter.ToUInt32(sub3, 4);
        uint w2 = BitConverter.ToUInt32(sub3, 8);
        uint w3 = BitConverter.ToUInt32(sub3, 12);
        uint w4 = BitConverter.ToUInt32(sub3, 16);
        uint w5 = BitConverter.ToUInt32(sub3, 20);
        Console.WriteLine($"sub3 headers: w0={w0:X4}, w1={w1:X4}, w2={w2:X4}, w3={w3:X4}, w4={w4:X4}, w5={w5:X4}");
        
        Console.WriteLine("w0 Table (20-byte records):");
        for (int i = 0; i < (w1 - w0) / 20; i++)
        {
            byte[] rec = new byte[20];
            Array.Copy(sub3, w0 + i * 20, rec, 0, 20);
            Console.WriteLine($"  [{i}]: {BitConverter.ToString(rec)}");
        }
        Console.WriteLine("w1 Table (12-byte records):");
        for (int i = 0; i < (w2 - w1) / 12; i++)
        {
            byte[] rec = new byte[12];
            Array.Copy(sub3, w1 + i * 12, rec, 0, 12);
            Console.WriteLine($"  [{i}]: {BitConverter.ToString(rec)}");
        }
        Console.WriteLine("w2 Table (8-byte records):");
        for (int i = 0; i < Math.Min(30, (w3 - w2) / 8); i++)
        {
            byte[] rec = new byte[8];
            Array.Copy(sub3, w2 + i * 8, rec, 0, 8);
            Console.WriteLine($"  [{i}]: {BitConverter.ToString(rec)}");
        }

        foreach (var m in targetMaps)
        {
            var targetSeg = containerMaps.FirstOrDefault(x => x.mapId == m);
            if (targetSeg.mapId == m)
            {
                var sSubs = AlundraHelper.SplitContainer(segments[targetSeg.segmentIdx].Data);
                string outPath = Path.Combine(baseDir, $"alundra_sprites_map{m}");
                AlundraHelper.ExtractSprites(sSubs, outPath, dumpPages: true);
                
                var rSub3 = sSubs[3];
                var rReport = AlundraHelper.VerifySub3Layout(rSub3, 256, 128, 128);
                File.WriteAllText(Path.Combine(baseDir, $"alundra_sub3_report_map{m}.txt"), rReport);
                Console.WriteLine($"Extracted sprites and report for Map {m}");
            }
        }
    }
}

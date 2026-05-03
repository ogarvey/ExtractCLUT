using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ExtractCLUT.Games.PSX.Korokke;

internal static class KorokkeExtractor
{
    private const int ScriptTableOffset = 0x10;
    private const int ScriptTableCount = 300;
    private const int FrameTableOffset = 0x4C0;
    private const int FrameDescriptorSize = 10;
    private const int MaxScriptSteps = 8192;
    private const int AncTilesPerPageRow = 31;
    private const int AncTilesPerPage = AncTilesPerPageRow * AncTilesPerPageRow;
    private static readonly Rgba32[] TransparentPalette = new Rgba32[16];

    public static void Extract(
        string assetPath,
        string outputRoot,
        string? paletteTimPath = null,
        int paletteGroupOffset = 0)
    {
        if (!File.Exists(assetPath))
        {
            Console.WriteLine($"Skipping missing file: {assetPath}");
            return;
        }

        byte[] data = File.ReadAllBytes(assetPath);
        AssetFormat format = DetectFormat(assetPath);
        CommonHeader header = ParseCommonHeader(data);
        Dictionary<int, List<SequenceFrameStep>> sequenceSteps = DiscoverSequenceSteps(data, header.TextureOffset);
        Dictionary<int, List<int>> sequenceFrames = sequenceSteps.ToDictionary(
            static entry => entry.Key,
            static entry => entry.Value.Select(static step => step.FrameId).ToList());
        List<int> usedFrameIds = sequenceSteps.Values
            .SelectMany(static steps => steps.Select(static step => step.FrameId))
            .Distinct()
            .OrderBy(static id => id)
            .ToList();

        Directory.CreateDirectory(outputRoot);
        string pagesDirectory = Path.Combine(outputRoot, "pages");
        string framesDirectory = Path.Combine(outputRoot, "frames");
        Directory.CreateDirectory(pagesDirectory);
        Directory.CreateDirectory(framesDirectory);

        List<string> warnings = new();
        List<string> pageOutputs = new();
        List<string> frameOutputs = new();
        List<string> sequenceOutputs = new();
        List<string> debugOutputs = new();
        TimClut? externalPalette = format == AssetFormat.Ann && !string.IsNullOrWhiteSpace(paletteTimPath)
            ? TryLoadTimClut(paletteTimPath, warnings)
            : null;
        string paletteResolutionMode = format switch
        {
            AssetFormat.Anm => "embedded",
            AssetFormat.Anc => "embedded-atlas",
            _ => externalPalette is not null ? "explicit" : "fallback"
        };
        List<string> resolvedPaletteSourcePaths = externalPalette is not null
            ? new() { externalPalette.Value.SourcePath }
            : new();

        switch (format)
        {
            case AssetFormat.Anm:
                if (!string.IsNullOrWhiteSpace(paletteTimPath))
                {
                    warnings.Add("Ignored external TIM palette arguments because ANM files carry their own palette data.");
                }
                ExtractAnm(data, header, usedFrameIds, pagesDirectory, framesDirectory, pageOutputs, frameOutputs, warnings);
                break;
            case AssetFormat.Ann:
                ExtractAnn(data, header, usedFrameIds, pagesDirectory, framesDirectory, pageOutputs, frameOutputs, warnings, externalPalette, paletteGroupOffset);
                break;
            case AssetFormat.Anc:
                if (!string.IsNullOrWhiteSpace(paletteTimPath) || paletteGroupOffset != 0)
                {
                    warnings.Add("Ignored external TIM palette arguments because ANC files now resolve palettes from their embedded atlas upload.");
                }
                if (TryReadAncTextureLayout(data, header, warnings, out AncTextureLayout ancTextureLayout))
                {
                    AncPaletteResolution ancPaletteResolution = BuildAncPaletteResolution(
                        assetPath,
                        data.AsSpan(ancTextureLayout.TextureDataOffset, ancTextureLayout.ExpectedPixelBytes),
                        ancTextureLayout.TexturePageColumns);
                    paletteResolutionMode = ancPaletteResolution.ResolutionMode;
                    resolvedPaletteSourcePaths = ancPaletteResolution.BaseSourcePaths.ToList();
                    ExtractAnc(
                        data,
                        header,
                        usedFrameIds,
                        pagesDirectory,
                        framesDirectory,
                        pageOutputs,
                        frameOutputs,
                        debugOutputs,
                        warnings,
                        ancTextureLayout,
                        ancPaletteResolution);
                }
                break;
            default:
                throw new InvalidOperationException($"Unsupported asset format for {assetPath}.");
        }

        ExportSequenceFolders(sequenceSteps, framesDirectory, outputRoot, sequenceOutputs, warnings);

        WriteManifest(
            assetPath,
            outputRoot,
            format,
            header,
            sequenceFrames,
            usedFrameIds,
            pageOutputs,
            frameOutputs,
            sequenceOutputs,
            debugOutputs,
            warnings,
            externalPalette?.SourcePath,
            paletteGroupOffset,
            paletteResolutionMode,
            resolvedPaletteSourcePaths);

        Console.WriteLine($"Extracted {Path.GetFileName(assetPath)} -> {outputRoot}");
        Console.WriteLine($"  Pages : {pageOutputs.Count}");
        Console.WriteLine($"  Frames: {frameOutputs.Count}");
        Console.WriteLine($"  Sequences: {sequenceOutputs.Count}");
        if (warnings.Count > 0)
        {
            Console.WriteLine($"  Warnings: {warnings.Count}");
        }
    }

    private static void ExtractAnm(
        byte[] data,
        CommonHeader header,
        IReadOnlyList<int> usedFrameIds,
        string pagesDirectory,
        string framesDirectory,
        ICollection<string> pageOutputs,
        ICollection<string> frameOutputs,
        ICollection<string> warnings)
    {
        TexturePage?[] pageSlots = new TexturePage[10];
        int cursor = header.TextureOffset;

        for (int slot = 0; slot < pageSlots.Length; slot++)
        {
            byte pageType = data[4 + slot];
            if (pageType == 0xFF)
            {
                continue;
            }

            if (pageType is not (0 or 1 or 3))
            {
                warnings.Add($"ANM slot {slot} has unsupported page type 0x{pageType:X2}.");
                continue;
            }

            int blockSize = pageType == 1 ? 0x10200 : 0x8200;
            if (cursor + blockSize > data.Length)
            {
                warnings.Add($"ANM slot {slot} page block overruns file bounds.");
                break;
            }

            ReadOnlySpan<byte> block = data.AsSpan(cursor, blockSize);
            pageSlots[slot] = pageType switch
            {
                1 => TexturePage.From8Bpp(
                    block.Slice(0x200, 0x10000),
                    256,
                    256,
                    ReadPsxPalette(block.Slice(0, 0x200), 256),
                    true,
                    $"anm_slot_{slot:D2}"),
                3 => TexturePage.From8Bpp(
                    block.Slice(0x200, 0x8000),
                    128,
                    256,
                    ReadPsxPalette(block.Slice(0, 0x200), 256),
                    true,
                    $"anm_slot_{slot:D2}"),
                _ => TexturePage.From4Bpp(
                    block.Slice(0x200, 0x8000),
                    256,
                    256,
                    ReadPsxPaletteGroups(block.Slice(0, 0x200), 16, 16),
                    true,
                    $"anm_slot_{slot:D2}")
            };

            int paletteIndex = pageSlots[slot]!.PaletteCount == 1 ? 0 : 0;
            string pagePath = Path.Combine(pagesDirectory, $"slot_{slot:D2}_type_{pageType:X2}.png");
            using (Image<Rgba32> preview = pageSlots[slot]!.RenderToImage(paletteIndex))
            {
                preview.SaveAsPng(pagePath);
            }
            pageOutputs.Add(pagePath);
            cursor += blockSize;
        }

        foreach (int frameId in usedFrameIds)
        {
            if (!TryReadFrameDescriptor(data, header.TextureOffset, frameId, out FrameDescriptor descriptor))
            {
                warnings.Add($"ANM frame {frameId} falls outside the descriptor table.");
                continue;
            }

            int slot = descriptor.PageSlot;
            TexturePage? page = slot >= 0 && slot < pageSlots.Length ? pageSlots[slot] : null;
            if (page is null)
            {
                warnings.Add($"ANM frame {frameId} references missing page slot {slot}.");
                continue;
            }

            int sourceX = ResolveDirectQuadSourceX(descriptor.SourceWord0);
            int sourceY = ResolveDirectQuadSourceY(descriptor.SourceWord1);
            if (!descriptor.IsPlausible)
            {
                warnings.Add($"ANM frame {frameId} has implausible bounds and was skipped.");
                continue;
            }

            int paletteIndex = 0;
            string framePath = Path.Combine(framesDirectory, $"frame_{frameId:D4}.png");
            using Image<Rgba32> frameImage = page.Crop(sourceX, sourceY, descriptor.Width, descriptor.Height, paletteIndex);
            frameImage.SaveAsPng(framePath);
            frameOutputs.Add(framePath);
        }
    }

    private static void ExtractAnn(
        byte[] data,
        CommonHeader header,
        IReadOnlyList<int> usedFrameIds,
        string pagesDirectory,
        string framesDirectory,
        ICollection<string> pageOutputs,
        ICollection<string> frameOutputs,
        ICollection<string> warnings,
        TimClut? externalPalette,
        int paletteGroupOffset)
    {
        if (header.TextureOffset + 8 > data.Length)
        {
            warnings.Add("ANN texture header falls outside file bounds.");
            return;
        }

        int widthWords = data[header.TextureOffset + 1] * 64;
        int pixelWidth = widthWords * 4;
        int expectedPixelBytes = widthWords * 2 * 256;
        int availablePixelBytes = Math.Max(0, data.Length - (header.TextureOffset + 8));
        if (pixelWidth <= 0 || availablePixelBytes < expectedPixelBytes)
        {
            warnings.Add("ANN texture payload is incomplete.");
            return;
        }

        PaletteGroupGrid paletteGrid = GetPaletteGridOrFallback(externalPalette, 16, 4, 4, seed: 5);
        TexturePage page = TexturePage.From4Bpp(
            data.AsSpan(header.TextureOffset + 8, expectedPixelBytes),
            pixelWidth,
            256,
            paletteGrid.Groups,
            externalPalette is not null,
            "ann_page_00");

        string pagePath = Path.Combine(pagesDirectory, "page_00_indexed.png");
        int previewPaletteIndex = ResolveAnnPaletteIndex(0, paletteGrid, paletteGroupOffset);
        using (Image<Rgba32> preview = page.RenderToImage(previewPaletteIndex))
        {
            preview.SaveAsPng(pagePath);
        }
        pageOutputs.Add(pagePath);
        if (externalPalette is null)
        {
            warnings.Add("ANN output uses a synthetic palette. The file does not carry its CLUT in the uploaded texture payload, so colors are provisional.");
        }
        else
        {
            warnings.Add($"ANN output uses palette groups extracted from '{Path.GetFileName(externalPalette.Value.SourcePath)}'. Palette selection is still heuristic because the runtime CLUT mapping is scene-driven.");
        }

        foreach (int frameId in usedFrameIds)
        {
            if (!TryReadFrameDescriptor(data, header.TextureOffset, frameId, out FrameDescriptor descriptor))
            {
                warnings.Add($"ANN frame {frameId} falls outside the descriptor table.");
                continue;
            }

            if (!descriptor.IsPlausible)
            {
                warnings.Add($"ANN frame {frameId} has implausible bounds and was skipped.");
                continue;
            }

            int sourceX = ResolveDirectQuadSourceX(descriptor.SourceWord0);
            int sourceY = ResolveDirectQuadSourceY(descriptor.SourceWord1);
            int paletteIndex = ResolveAnnPaletteIndex(descriptor.PageVariantHighByte, paletteGrid, paletteGroupOffset);
            string framePath = Path.Combine(framesDirectory, $"frame_{frameId:D4}.png");
            using Image<Rgba32> frameImage = page.Crop(sourceX, sourceY, descriptor.Width, descriptor.Height, paletteIndex);
            frameImage.SaveAsPng(framePath);
            frameOutputs.Add(framePath);
        }
    }

    private static void ExtractAnc(
        byte[] data,
        CommonHeader header,
        IReadOnlyList<int> usedFrameIds,
        string pagesDirectory,
        string framesDirectory,
        ICollection<string> pageOutputs,
        ICollection<string> frameOutputs,
        ICollection<string> debugOutputs,
        ICollection<string> warnings,
        AncTextureLayout layout,
        AncPaletteResolution paletteResolution)
    {
        TexturePage atlas = TexturePage.From4Bpp(
            data.AsSpan(layout.TextureDataOffset, layout.ExpectedPixelBytes),
            layout.TexturePixelWidth,
            256,
            paletteResolution.BasePaletteGrid.Groups,
            paletteResolution.BaseHasRealPalette,
            "anc_atlas");

        AncComposition composition = new(
            layout.GridWidth,
            layout.GridHeight,
            data.AsSpan(layout.CompositionTableOffset, layout.CompositionTableBytes).ToArray(),
            atlas,
            paletteResolution.BasePaletteGrid,
            paletteResolution.BasePaletteOffset,
            paletteResolution.BaseGroupSourceLabels,
            paletteResolution.EffectPaletteGrid,
            paletteResolution.EffectPaletteOffset,
            paletteResolution.EffectGroupSourceLabels);

        string outputRoot = Path.GetDirectoryName(framesDirectory) ?? Environment.CurrentDirectory;
        string debugDirectory = Path.Combine(outputRoot, "debug");
        Directory.CreateDirectory(debugDirectory);

        string atlasPath = Path.Combine(pagesDirectory, "atlas_indexed.png");
        using (Image<Rgba32> preview = atlas.RenderToImage(paletteResolution.PreviewPaletteIndex))
        {
            preview.SaveAsPng(atlasPath);
        }
        pageOutputs.Add(atlasPath);
        string paletteContextPath = Path.Combine(debugDirectory, "anc_palette_context.json");
        WriteAncPaletteContextDebug(
            paletteContextPath,
            paletteResolution,
            layout.GridWidth,
            layout.GridHeight,
            layout.TexturePageColumns,
            layout.TexturePixelWidth,
            usedFrameIds.Count);
        debugOutputs.Add(paletteContextPath);

        Dictionary<AncPaletteUsageSummaryKey, AncPaletteUsageAccumulator> usageSummary = new();

        foreach (int frameId in usedFrameIds)
        {
            if (!TryReadFrameDescriptor(data, header.TextureOffset, frameId, out FrameDescriptor descriptor))
            {
                warnings.Add($"ANC frame {frameId} falls outside the descriptor table.");
                continue;
            }

            if (!descriptor.IsPlausible)
            {
                warnings.Add($"ANC frame {frameId} has implausible bounds and was skipped.");
                continue;
            }

            AncFrameRenderResult? renderResult = composition.RenderFrame(descriptor);
            if (renderResult is null)
            {
                warnings.Add($"ANC frame {frameId} could not be composed.");
                continue;
            }

            string framePath = Path.Combine(framesDirectory, $"frame_{frameId:D4}.png");
            using (renderResult.Image)
            {
                renderResult.Image.SaveAsPng(framePath);
            }
            frameOutputs.Add(framePath);

            string frameDebugPath = Path.Combine(debugDirectory, $"frame_{frameId:D4}_palette_debug.json");
            string frameAncAttributePath = Path.Combine(debugDirectory, $"frame_{frameId:D4}_anc_attribute_u8.bin");
            string frameAncCellsPath = Path.Combine(debugDirectory, $"frame_{frameId:D4}_anc_cells_u8_triplets.bin");
            WriteAncFrameAttributeBinary(frameAncAttributePath, renderResult);
            WriteAncFrameCellBinary(frameAncCellsPath, renderResult);
            debugOutputs.Add(frameAncAttributePath);
            debugOutputs.Add(frameAncCellsPath);
            WriteAncFramePaletteDebug(frameDebugPath, frameId, descriptor, renderResult, frameAncAttributePath, frameAncCellsPath);
            debugOutputs.Add(frameDebugPath);
            UpdateAncPaletteUsageSummary(usageSummary, frameId, renderResult.TileDebugInfos);
        }

        string paletteSummaryPath = Path.Combine(debugDirectory, "anc_palette_usage_summary.json");
        WriteAncPaletteUsageSummary(paletteSummaryPath, usageSummary);
        debugOutputs.Add(paletteSummaryPath);
    }

    private static AssetFormat DetectFormat(string assetPath)
        => Path.GetExtension(assetPath).ToUpperInvariant() switch
        {
            ".ANM" => AssetFormat.Anm,
            ".ANN" => AssetFormat.Ann,
            ".ANC" => AssetFormat.Anc,
            _ => throw new InvalidOperationException($"Unsupported asset extension for {assetPath}.")
        };

    private static CommonHeader ParseCommonHeader(byte[] data)
    {
        if (data.Length < FrameTableOffset)
        {
            throw new InvalidDataException("Asset file is too small to contain the common Korokke header.");
        }

        int textureOffset = ReadInt32LE(data, 0);
        int[] sequenceOffsets = new int[ScriptTableCount];
        for (int index = 0; index < ScriptTableCount; index++)
        {
            sequenceOffsets[index] = ReadInt32BE(data, ScriptTableOffset + index * 4);
        }

        return new(textureOffset, sequenceOffsets);
    }

    private static Dictionary<int, List<SequenceFrameStep>> DiscoverSequenceSteps(byte[] data, int scriptRegionEnd)
    {
        Dictionary<int, List<SequenceFrameStep>> result = new();

        for (int sequenceIndex = 0; sequenceIndex < ScriptTableCount; sequenceIndex++)
        {
            int relativeOffset = ReadInt32BE(data, ScriptTableOffset + sequenceIndex * 4);
            if (relativeOffset < FrameTableOffset)
            {
                continue;
            }

            int scriptStart = ScriptTableOffset + relativeOffset;
            if (scriptStart < 0 || scriptStart >= scriptRegionEnd || scriptStart >= data.Length)
            {
                continue;
            }

            List<SequenceFrameStep> frameSteps = TraceScriptFrames(data, scriptStart, scriptRegionEnd);
            if (frameSteps.Count > 0)
            {
                result[sequenceIndex] = frameSteps;
            }
        }

        return result;
    }

    private static List<SequenceFrameStep> TraceScriptFrames(byte[] data, int scriptStart, int scriptRegionEnd)
    {
        List<SequenceFrameStep> frames = new();
        Dictionary<int, int> cursorVisits = new();
        SequenceTraceState state = new();

        int cursor = 0;
        int loop84Counter = 0;
        int loop84Target = 0;
        int loop86Counter = 0;
        int loop86Target = 0;

        for (int step = 0; step < MaxScriptSteps; step++)
        {
            int absolute = scriptStart + cursor;
            if (absolute < 0 || absolute >= scriptRegionEnd || absolute >= data.Length)
            {
                break;
            }

            cursorVisits.TryGetValue(cursor, out int visitCount);
            if (visitCount > 15)
            {
                break;
            }
            cursorVisits[cursor] = visitCount + 1;

            byte opcode = data[absolute];
            switch (opcode)
            {
                case 0x80:
                case 0x81:
                    cursor += 1;
                    continue;

                case 0x82:
                    // 0x82 restarts playback from the sequence entry point. For export,
                    // treat that as the end of one logical cycle instead of unrolling
                    // the loop until the visit cap.
                    return frames;

                case 0x84:
                    if (absolute + 1 >= scriptRegionEnd)
                    {
                        return frames;
                    }
                    loop84Counter = data[absolute + 1];
                    if (loop84Counter != 0)
                    {
                        loop84Counter--;
                    }
                    loop84Target = cursor + 2;
                    cursor += 2;
                    continue;

                case 0x85:
                    if (loop84Counter != 0)
                    {
                        loop84Counter--;
                        cursor = loop84Target;
                    }
                    else
                    {
                        cursor += 1;
                    }
                    continue;

                case 0x86:
                    if (absolute + 1 >= scriptRegionEnd)
                    {
                        return frames;
                    }
                    loop86Counter = data[absolute + 1];
                    if (loop86Counter != 0)
                    {
                        loop86Counter--;
                    }
                    loop86Target = cursor + 2;
                    cursor += 2;
                    continue;

                case 0x87:
                    if (loop86Counter != 0)
                    {
                        loop86Counter--;
                        cursor = loop86Target;
                    }
                    else
                    {
                        cursor += 1;
                    }
                    continue;

                case 0xF1:
                    if (absolute + 1 >= scriptRegionEnd || absolute + 1 >= data.Length)
                    {
                        return frames;
                    }

                    byte subOpcode = data[absolute + 1];
                    int commandLength = subOpcode == 0x15 ? 12 : 6;
                    if (absolute + commandLength - 1 >= scriptRegionEnd || absolute + commandLength - 1 >= data.Length)
                    {
                        return frames;
                    }

                    if (subOpcode == 0x02)
                    {
                        state.GlobalOffsetX = (short)(data[absolute + 2] - 0x80);
                        state.GlobalOffsetY = (short)(data[absolute + 3] - 0x80);
                    }

                    cursor += commandLength;
                    continue;

                case 0xF2:
                    if (absolute + 2 >= scriptRegionEnd || absolute + 2 >= data.Length)
                    {
                        return frames;
                    }
                    state.ScaleY = ReadUInt16BE(data, absolute + 1);
                    cursor += 3;
                    continue;

                case 0xF3:
                    if (absolute + 2 >= scriptRegionEnd || absolute + 2 >= data.Length)
                    {
                        return frames;
                    }
                    state.ScaleX = ReadUInt16BE(data, absolute + 1);
                    cursor += 3;
                    continue;

                case 0xF8:
                    if (absolute + 2 >= scriptRegionEnd || absolute + 2 >= data.Length)
                    {
                        return frames;
                    }
                    state.Rotation = ReadUInt16BE(data, absolute + 1);
                    cursor += 3;
                    continue;

                case 0xFA:
                    if (absolute + 2 >= scriptRegionEnd || absolute + 2 >= data.Length)
                    {
                        return frames;
                    }
                    cursor += 3;
                    continue;

                case 0xFB:
                    if (absolute + 2 >= scriptRegionEnd || absolute + 2 >= data.Length)
                    {
                        return frames;
                    }
                    state.OffsetY = ReadInt16BE(data, absolute + 1);
                    cursor += 3;
                    continue;

                case 0xFC:
                    if (absolute + 2 >= scriptRegionEnd || absolute + 2 >= data.Length)
                    {
                        return frames;
                    }
                    state.OffsetX = ReadInt16BE(data, absolute + 1);
                    cursor += 3;
                    continue;

                case 0xF5:
                case 0xF6:
                case 0xF7:
                case 0xF9:
                    if (absolute + 1 >= scriptRegionEnd || absolute + 1 >= data.Length)
                    {
                        return frames;
                    }
                    state.TransformFlags = data[absolute + 1];
                    cursor += 2;
                    continue;

                case 0xFD:
                    if (absolute + 1 >= scriptRegionEnd || absolute + 1 >= data.Length)
                    {
                        return frames;
                    }
                    state.RenderFlags = data[absolute + 1];
                    cursor += 2;
                    continue;

                case 0xFE:
                    cursor += 2;
                    continue;

                case 0xFF:
                    return frames;
            }

            if (opcode < 0x41)
            {
                if (absolute + 1 >= scriptRegionEnd)
                {
                    return frames;
                }

                int frameId = (opcode << 8) | data[absolute + 1];
                frames.Add(new(
                    frames.Count,
                    frameId,
                    state.OffsetX,
                    state.OffsetY,
                    state.GlobalOffsetX,
                    state.GlobalOffsetY,
                    state.ScaleX,
                    state.ScaleY,
                    state.Rotation,
                    state.RenderFlags,
                    state.TransformFlags));

                cursor += 2;
                continue;
            }

            return frames;
        }

        return frames;
    }

    private static bool TryReadFrameDescriptor(byte[] data, int descriptorRegionEnd, int frameId, out FrameDescriptor descriptor)
    {
        long offset = FrameTableOffset + (long)frameId * FrameDescriptorSize;
        if (offset < FrameTableOffset || offset + FrameDescriptorSize > descriptorRegionEnd || offset + FrameDescriptorSize > data.Length)
        {
            descriptor = default;
            return false;
        }

        int baseOffset = (int)offset;
        descriptor = new(
            frameId,
            ReadUInt16LE(data, baseOffset),
            ReadUInt16LE(data, baseOffset + 2),
            ReadUInt16LE(data, baseOffset + 4),
            ReadUInt16LE(data, baseOffset + 6),
            ReadUInt16LE(data, baseOffset + 8));
        return true;
    }

    private static void WriteManifest(
        string assetPath,
        string outputRoot,
        AssetFormat format,
        CommonHeader header,
        IReadOnlyDictionary<int, List<int>> sequenceFrames,
        IReadOnlyList<int> usedFrameIds,
        IReadOnlyCollection<string> pageOutputs,
        IReadOnlyCollection<string> frameOutputs,
        IReadOnlyCollection<string> sequenceOutputs,
        IReadOnlyCollection<string> debugOutputs,
        IReadOnlyCollection<string> warnings,
        string? paletteSourcePath,
        int paletteGroupOffset,
        string paletteResolutionMode,
        IReadOnlyCollection<string> resolvedPaletteSourcePaths)
    {
        var manifest = new
        {
            assetPath,
            format = format.ToString(),
            paletteMappingMode = format switch
            {
                AssetFormat.Anc => "runtime-anc-clut-grid",
                AssetFormat.Ann => "flat-palette-groups",
                AssetFormat.Anm => "embedded",
                _ => "unknown"
            },
            paletteResolutionMode,
            paletteSourcePath,
            resolvedPaletteSourcePaths,
            paletteGroupOffset,
            textureOffset = $"0x{header.TextureOffset:X}",
            pageOutputs = pageOutputs.Select(Path.GetFileName).OrderBy(static name => name).ToArray(),
            frameOutputs = frameOutputs.Select(Path.GetFileName).OrderBy(static name => name).ToArray(),
            sequenceOutputs = sequenceOutputs.Select(Path.GetFileName).OrderBy(static name => name).ToArray(),
            debugOutputs = debugOutputs.Select(Path.GetFileName).OrderBy(static name => name).ToArray(),
            usedFrameIds,
            sequenceFrames,
            warnings
        };

        string manifestPath = Path.Combine(outputRoot, "manifest.json");
        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void ExportSequenceFolders(
        IReadOnlyDictionary<int, List<SequenceFrameStep>> sequenceFrames,
        string framesDirectory,
        string outputRoot,
        ICollection<string> sequenceOutputs,
        ICollection<string> warnings)
    {
        if (sequenceFrames.Count == 0)
        {
            return;
        }

        string sequencesDirectory = Path.Combine(outputRoot, "sequences");
        if (Directory.Exists(sequencesDirectory))
        {
            Directory.Delete(sequencesDirectory, recursive: true);
        }

        foreach ((int sequenceIndex, List<SequenceFrameStep> frameSteps) in sequenceFrames.OrderBy(static entry => entry.Key))
        {
            string sequenceDirectory = Path.Combine(sequencesDirectory, $"sequence_{sequenceIndex:D3}");
            int copiedFrameCount = 0;
            List<SequenceAlignedFramePlan> alignmentPlans = new();
            HashSet<string> sequenceWarnings = new(StringComparer.Ordinal);
            int minDrawX = int.MaxValue;
            int minDrawY = int.MaxValue;
            int maxDrawRight = int.MinValue;
            int maxDrawBottom = int.MinValue;

            for (int stepIndex = 0; stepIndex < frameSteps.Count; stepIndex++)
            {
                SequenceFrameStep frameStep = frameSteps[stepIndex];
                int frameId = frameStep.FrameId;
                string sourceFramePath = Path.Combine(framesDirectory, $"frame_{frameId:D4}.png");
                if (!File.Exists(sourceFramePath))
                {
                    continue;
                }

                if (copiedFrameCount == 0)
                {
                    Directory.CreateDirectory(sequenceDirectory);
                }

                string destinationPath = Path.Combine(sequenceDirectory, $"{stepIndex:D4}_frame_{frameId:D4}.png");
                File.Copy(sourceFramePath, destinationPath, overwrite: true);
                copiedFrameCount++;

                using Image<Rgba32> sourceImage = Image.Load<Rgba32>(sourceFramePath);
                int scaledWidth = GetScaledFrameDimension(sourceImage.Width, frameStep.ScaleX);
                int scaledHeight = GetScaledFrameDimension(sourceImage.Height, frameStep.ScaleY);
                minDrawX = Math.Min(minDrawX, frameStep.DrawX);
                minDrawY = Math.Min(minDrawY, frameStep.DrawY);
                maxDrawRight = Math.Max(maxDrawRight, frameStep.DrawX + scaledWidth);
                maxDrawBottom = Math.Max(maxDrawBottom, frameStep.DrawY + scaledHeight);

                if (frameStep.Rotation != 0)
                {
                    sequenceWarnings.Add($"Step {stepIndex:D4} frame {frameId:D4} uses rotation 0x{frameStep.Rotation:X4}; aligned exports keep the unrotated crop.");
                }

                alignmentPlans.Add(new(
                    frameStep.StepIndex,
                    frameId,
                    sourceFramePath,
                    sourceImage.Width,
                    sourceImage.Height,
                    frameStep.DrawX,
                    frameStep.DrawY,
                    scaledWidth,
                    scaledHeight,
                    frameStep.ScaleX,
                    frameStep.ScaleY,
                    frameStep.Rotation,
                    frameStep.RenderFlags,
                    frameStep.TransformFlags));
            }

            if (copiedFrameCount > 0)
            {
                string alignmentPath = Path.Combine(sequenceDirectory, "alignment.json");
                if (alignmentPlans.Count > 0)
                {
                    int canvasWidth = Math.Max(1, maxDrawRight - minDrawX);
                    int canvasHeight = Math.Max(1, maxDrawBottom - minDrawY);
                    string alignedDirectory = Path.Combine(sequenceDirectory, "aligned");
                    Directory.CreateDirectory(alignedDirectory);

                    foreach (SequenceAlignedFramePlan plan in alignmentPlans)
                    {
                        string alignedFramePath = Path.Combine(alignedDirectory, $"{plan.StepIndex:D4}_frame_{plan.FrameId:D4}.png");
                        using Image<Rgba32> sourceImage = Image.Load<Rgba32>(plan.SourceFramePath);
                        using Image<Rgba32> canvas = new(canvasWidth, canvasHeight, new Rgba32(0, 0, 0, 0));
                        BlitAlignedFrame(
                            sourceImage,
                            canvas,
                            plan.DrawX - minDrawX,
                            plan.DrawY - minDrawY,
                            plan.OutputWidth,
                            plan.OutputHeight,
                            plan.FlipHorizontal,
                            plan.FlipVertical);
                        canvas.SaveAsPng(alignedFramePath);
                    }

                    WriteSequenceAlignmentDebug(
                        alignmentPath,
                        sequenceIndex,
                        canvasWidth,
                        canvasHeight,
                        minDrawX,
                        minDrawY,
                        alignmentPlans,
                        sequenceWarnings);
                }

                foreach (string warning in sequenceWarnings)
                {
                    warnings.Add($"Sequence {sequenceIndex:D3}: {warning}");
                }

                sequenceOutputs.Add(sequenceDirectory);
            }
        }
    }

    private static void WriteSequenceAlignmentDebug(
        string filePath,
        int sequenceIndex,
        int canvasWidth,
        int canvasHeight,
        int normalizedOriginX,
        int normalizedOriginY,
        IReadOnlyList<SequenceAlignedFramePlan> alignmentPlans,
        IReadOnlyCollection<string> warnings)
    {
        var alignment = new
        {
            sequenceIndex,
            alignmentMode = "StepAssetEntryScript FC/FB top-left offsets normalized to the minimum sequence extents.",
            assumptions = new[]
            {
                "FC provides the per-frame horizontal draw offset.",
                "FB provides the per-frame vertical draw offset.",
                "Scene registration offsets are normalized away because they are not stored in the asset.",
                "Aligned exports apply FC/FB placement, nearest-neighbor scaling, and documented flip bits only."
            },
            canvasWidth,
            canvasHeight,
            normalizedOriginX,
            normalizedOriginY,
            warnings = warnings.OrderBy(static warning => warning).ToArray(),
            frames = alignmentPlans.Select(plan => new
            {
                plan.StepIndex,
                plan.FrameId,
                rawFrame = $"{plan.StepIndex:D4}_frame_{plan.FrameId:D4}.png",
                alignedFrame = Path.Combine("aligned", $"{plan.StepIndex:D4}_frame_{plan.FrameId:D4}.png"),
                sourceWidth = plan.SourceWidth,
                sourceHeight = plan.SourceHeight,
                drawX = plan.DrawX,
                drawY = plan.DrawY,
                normalizedX = plan.DrawX - normalizedOriginX,
                normalizedY = plan.DrawY - normalizedOriginY,
                outputWidth = plan.OutputWidth,
                outputHeight = plan.OutputHeight,
                plan.ScaleX,
                plan.ScaleY,
                plan.Rotation,
                renderFlags = $"0x{plan.RenderFlags:X2}",
                transformFlags = $"0x{plan.TransformFlags:X2}",
                plan.FlipHorizontal,
                plan.FlipVertical
            }).ToArray()
        };

        File.WriteAllText(filePath, JsonSerializer.Serialize(alignment, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static int GetScaledFrameDimension(int sourceDimension, ushort scalePercent)
        => Math.Max(1, sourceDimension * Math.Max(1, (int)scalePercent) / 100);

    private static void BlitAlignedFrame(
        Image<Rgba32> sourceImage,
        Image<Rgba32> canvas,
        int destinationX,
        int destinationY,
        int outputWidth,
        int outputHeight,
        bool flipHorizontal,
        bool flipVertical)
    {
        if (TryBlitTileScaledFrame(sourceImage, canvas, destinationX, destinationY, outputWidth, outputHeight, flipHorizontal, flipVertical))
        {
            return;
        }

        int safeOutputWidth = Math.Max(1, outputWidth);
        int safeOutputHeight = Math.Max(1, outputHeight);

        for (int y = 0; y < safeOutputHeight; y++)
        {
            int canvasY = destinationY + y;
            if ((uint)canvasY >= canvas.Height)
            {
                continue;
            }

            int sourceY = y * sourceImage.Height / safeOutputHeight;
            if (flipVertical)
            {
                sourceY = sourceImage.Height - 1 - sourceY;
            }

            for (int x = 0; x < safeOutputWidth; x++)
            {
                int canvasX = destinationX + x;
                if ((uint)canvasX >= canvas.Width)
                {
                    continue;
                }

                int sourceX = x * sourceImage.Width / safeOutputWidth;
                if (flipHorizontal)
                {
                    sourceX = sourceImage.Width - 1 - sourceX;
                }

                Rgba32 pixel = sourceImage[sourceX, sourceY];
                if (pixel.A == 0)
                {
                    continue;
                }

                canvas[canvasX, canvasY] = pixel;
            }
        }
    }

    private static bool TryBlitTileScaledFrame(
        Image<Rgba32> sourceImage,
        Image<Rgba32> canvas,
        int destinationX,
        int destinationY,
        int outputWidth,
        int outputHeight,
        bool flipHorizontal,
        bool flipVertical)
    {
        if (sourceImage.Width <= 0 || sourceImage.Height <= 0 || sourceImage.Width % 8 != 0 || sourceImage.Height % 8 != 0)
        {
            return false;
        }

        int tileColumns = sourceImage.Width / 8;
        int tileRows = sourceImage.Height / 8;
        if (tileColumns <= 0 || tileRows <= 0)
        {
            return false;
        }

        int safeOutputWidth = Math.Max(1, outputWidth);
        int safeOutputHeight = Math.Max(1, outputHeight);
        int[] xEdges = BuildInterpolatedEdges(destinationX, safeOutputWidth, tileColumns);
        int[] yEdges = BuildInterpolatedEdges(destinationY, safeOutputHeight, tileRows);

        for (int tileRow = 0; tileRow < tileRows; tileRow++)
        {
            int targetTop = yEdges[tileRow];
            int targetBottom = yEdges[tileRow + 1];
            int targetTileHeight = Math.Max(1, targetBottom - targetTop);

            for (int tileColumn = 0; tileColumn < tileColumns; tileColumn++)
            {
                int targetLeft = xEdges[tileColumn];
                int targetRight = xEdges[tileColumn + 1];
                int targetTileWidth = Math.Max(1, targetRight - targetLeft);
                int sourceTileX = tileColumn * 8;
                int sourceTileY = tileRow * 8;

                for (int y = 0; y < targetTileHeight; y++)
                {
                    int canvasY = targetTop + y;
                    if ((uint)canvasY >= canvas.Height)
                    {
                        continue;
                    }

                    int sourceY = sourceTileY + y * 8 / targetTileHeight;
                    if (flipVertical)
                    {
                        sourceY = sourceTileY + (7 - (sourceY - sourceTileY));
                    }

                    for (int x = 0; x < targetTileWidth; x++)
                    {
                        int canvasX = targetLeft + x;
                        if ((uint)canvasX >= canvas.Width)
                        {
                            continue;
                        }

                        int sourceX = sourceTileX + x * 8 / targetTileWidth;
                        if (flipHorizontal)
                        {
                            sourceX = sourceTileX + (7 - (sourceX - sourceTileX));
                        }

                        Rgba32 pixel = sourceImage[sourceX, sourceY];
                        if (pixel.A == 0)
                        {
                            continue;
                        }

                        canvas[canvasX, canvasY] = pixel;
                    }
                }
            }
        }

        return true;
    }

    private static int[] BuildInterpolatedEdges(int destinationStart, int span, int segmentCount)
    {
        int safeSegments = Math.Max(1, segmentCount);
        int[] edges = new int[safeSegments + 1];
        for (int index = 0; index <= safeSegments; index++)
        {
            edges[index] = destinationStart + index * span / safeSegments;
        }

        return edges;
    }

    private static Rgba32[] ReadPsxPalette(ReadOnlySpan<byte> bytes, int colorCount)
    {
        Rgba32[] colors = new Rgba32[colorCount];
        for (int index = 0; index < colorCount; index++)
        {
            int offset = index * 2;
            ushort raw = (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
            colors[index] = ConvertPsxColor(raw);
        }
        return colors;
    }

    private static IReadOnlyList<Rgba32[]> ReadPsxPaletteGroups(ReadOnlySpan<byte> bytes, int groupCount, int colorsPerGroup)
    {
        List<Rgba32[]> groups = new(groupCount);
        for (int group = 0; group < groupCount; group++)
        {
            groups.Add(ReadPsxPalette(bytes.Slice(group * colorsPerGroup * 2, colorsPerGroup * 2), colorsPerGroup));
        }
        return groups;
    }

    private static IReadOnlyList<Rgba32[]> GetPaletteGroupsOrFallback(TimClut? externalPalette, int colorsPerGroup, int seed)
    {
        if (externalPalette is null)
        {
            return BuildPseudoPaletteGroups(16, colorsPerGroup, seed);
        }

        IReadOnlyList<Rgba32[]> groups = externalPalette.Value.ToPaletteGroups(colorsPerGroup);
        return groups.Count > 0 ? groups : BuildPseudoPaletteGroups(16, colorsPerGroup, seed);
    }

    private static bool TryReadAncTextureLayout(
        byte[] data,
        CommonHeader header,
        ICollection<string> warnings,
        out AncTextureLayout layout)
    {
        if (header.TextureOffset + 0x10 > data.Length)
        {
            warnings.Add("ANC composition header falls outside file bounds.");
            layout = default;
            return false;
        }

        int gridWidth = ReadUInt16BE(data, header.TextureOffset);
        int gridHeight = ReadUInt16BE(data, header.TextureOffset + 2);
        int texturePageColumns = ReadUInt16BE(data, header.TextureOffset + 4);
        int compositionTableBytes = checked(gridWidth * gridHeight * 3);
        int compositionTableOffset = header.TextureOffset + 0x10;
        int textureDataOffset = compositionTableOffset + compositionTableBytes;
        int texturePixelWidth = texturePageColumns * 256;
        int expectedPixelBytes = texturePageColumns * 128 * 256;

        if (gridWidth <= 0 || gridHeight <= 0 || texturePageColumns <= 0)
        {
            warnings.Add("ANC header values are not plausible.");
            layout = default;
            return false;
        }

        if (textureDataOffset + expectedPixelBytes > data.Length)
        {
            warnings.Add("ANC texture payload is incomplete.");
            layout = default;
            return false;
        }

        layout = new(
            gridWidth,
            gridHeight,
            texturePageColumns,
            compositionTableBytes,
            compositionTableOffset,
            textureDataOffset,
            texturePixelWidth,
            expectedPixelBytes);
        return true;
    }

    private static AncPaletteResolution BuildAncPaletteResolution(
        string assetPath,
        ReadOnlySpan<byte> textureData,
        int texturePageColumns)
    {
        PaletteGroupGrid paletteGrid = BuildAncEmbeddedPaletteGrid(textureData, texturePageColumns);
        IReadOnlyList<string> groupSourceLabels = BuildAncEmbeddedGroupSourceLabels(assetPath, paletteGrid);
        IReadOnlyList<string> sourcePaths = new[] { Path.GetFullPath(assetPath) };

        return new(
            paletteGrid,
            0,
            ResolveAncPreviewPaletteIndex(paletteGrid, 0),
            true,
            $"ANC tiles resolve from palette words embedded in '{Path.GetFileName(assetPath)}'. QueueAncTileGrid samples CLUTs from the uploaded ANC atlas rows.",
            sourcePaths,
            groupSourceLabels,
            paletteGrid,
            0,
            null,
            sourcePaths,
            groupSourceLabels,
            "embedded-atlas");
    }

    private static PaletteGroupGrid BuildAncEmbeddedPaletteGrid(ReadOnlySpan<byte> textureData, int texturePageColumns)
    {
        int groupsPerRow = Math.Max(1, texturePageColumns * 4);
        int rowStrideBytes = groupsPerRow * 16 * 2;
        int rowCount = textureData.Length / rowStrideBytes;
        if (rowCount <= 0)
        {
            return new(Array.Empty<Rgba32[]>(), 0);
        }

        List<Rgba32[]> groups = new(groupsPerRow * rowCount);
        for (int row = 0; row < rowCount; row++)
        {
            int rowOffset = row * rowStrideBytes;
            for (int group = 0; group < groupsPerRow; group++)
            {
                int groupOffset = rowOffset + group * 16 * 2;
                groups.Add(ReadPsxPalette(textureData.Slice(groupOffset, 16 * 2), 16));
            }
        }

        return new(groups, groupsPerRow);
    }

    private static IReadOnlyList<string> BuildAncEmbeddedGroupSourceLabels(string assetPath, PaletteGroupGrid paletteGrid)
    {
        if (paletteGrid.Count <= 0)
        {
            return Array.Empty<string>();
        }

        string assetName = Path.GetFileName(assetPath);
        int groupsPerRow = Math.Max(1, paletteGrid.GroupsPerRow);
        string[] labels = new string[paletteGrid.Count];
        for (int index = 0; index < labels.Length; index++)
        {
            int row = index / groupsPerRow;
            labels[index] = $"{assetName} atlas row {row:D3}";
        }

        return labels;
    }

    private static PaletteGroupGrid GetPaletteGridOrFallback(
        TimClut? externalPalette,
        int colorsPerGroup,
        int fallbackGroupsPerRow,
        int fallbackRowCount,
        int seed)
    {
        if (externalPalette is null)
        {
            return BuildPseudoPaletteGrid(fallbackGroupsPerRow, fallbackRowCount, colorsPerGroup, seed);
        }

        PaletteGroupGrid grid = externalPalette.Value.ToPaletteGrid(colorsPerGroup);
        return grid.Count > 0 ? grid : BuildPseudoPaletteGrid(fallbackGroupsPerRow, fallbackRowCount, colorsPerGroup, seed);
    }

    private static string? TryResolveExistingArchivePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (File.Exists(path))
        {
            return Path.GetFullPath(path);
        }

        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        string extension = Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        string targetStem = NormalizeArchiveStem(Path.GetFileNameWithoutExtension(path));
        foreach (string candidatePath in Directory.EnumerateFiles(directory, $"*{extension}", SearchOption.TopDirectoryOnly))
        {
            if (string.Equals(
                NormalizeArchiveStem(Path.GetFileNameWithoutExtension(candidatePath)),
                targetStem,
                StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(candidatePath);
            }
        }

        return null;
    }

    private static string NormalizeArchiveStem(string stem)
        => stem.TrimEnd();

    private static int GetWrappedPaletteIndex(int paletteIndex, int paletteCount)
    {
        if (paletteCount <= 0)
        {
            return 0;
        }

        int wrapped = paletteIndex % paletteCount;
        return wrapped < 0 ? wrapped + paletteCount : wrapped;
    }

    private static int ResolveDirectQuadSourceX(ushort sourceWord0)
        => sourceWord0;

    private static int ResolveDirectQuadSourceY(ushort sourceWord1)
        => sourceWord1 & 0xFF;

    private static int ResolveAnnPaletteIndex(int pageVariantHighByte, PaletteGroupGrid paletteGrid, int paletteGroupOffset)
    {
        if (paletteGrid.Count <= 0)
        {
            return 0;
        }

        int groupsPerRow = Math.Max(1, paletteGrid.GroupsPerRow);
        int bankOffset = (pageVariantHighByte >> 5) * 4;
        int rowFromBottom = (pageVariantHighByte & 0x1F) >> 2;
        int columnOffset = pageVariantHighByte & 0x03;
        int paletteRow = Math.Max(0, paletteGrid.RowCount - 1 - rowFromBottom);
        int paletteIndex = paletteRow * groupsPerRow + bankOffset + columnOffset + paletteGroupOffset;
        return GetWrappedPaletteIndex(paletteIndex, paletteGrid.Count);
    }

    private static int ResolveAncPreviewPaletteIndex(PaletteGroupGrid paletteGrid, int baseGroupOffset)
    {
        if (paletteGrid.Count <= 0)
        {
            return 0;
        }

        int groupsPerRow = Math.Max(1, paletteGrid.GroupsPerRow);
        int runtimeBaseGroupIndex = baseGroupOffset + Math.Max(0, paletteGrid.RowCount - 1) * groupsPerRow;
        return GetWrappedPaletteIndex(runtimeBaseGroupIndex, paletteGrid.Count);
    }

    private static TimClut? TryLoadTimClut(string? timPath, ICollection<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(timPath))
        {
            return null;
        }

        string? resolvedTimPath = TryResolveExistingArchivePath(timPath);
        if (resolvedTimPath is null)
        {
            warnings.Add($"TIM palette source was not found: {timPath}");
            return null;
        }

        byte[] data = File.ReadAllBytes(resolvedTimPath);
        if (data.Length < 20)
        {
            warnings.Add($"TIM palette source '{Path.GetFileName(resolvedTimPath)}' is too small to contain a valid TIM header.");
            return null;
        }

        int magic = ReadInt32LE(data, 0);
        if (magic != 0x10)
        {
            warnings.Add($"TIM palette source '{Path.GetFileName(resolvedTimPath)}' does not have a TIM magic value.");
            return null;
        }

        int flags = ReadInt32LE(data, 4);
        if ((flags & 0x8) == 0)
        {
            warnings.Add($"TIM palette source '{Path.GetFileName(resolvedTimPath)}' does not carry a CLUT block.");
            return null;
        }

        int clutBlockOffset = 8;
        int clutBlockSize = ReadInt32LE(data, clutBlockOffset);
        if (clutBlockSize < 12 || clutBlockOffset + clutBlockSize > data.Length)
        {
            warnings.Add($"TIM palette source '{Path.GetFileName(resolvedTimPath)}' has an invalid CLUT block size.");
            return null;
        }

        int clutWidth = ReadUInt16LE(data, clutBlockOffset + 8);
        int clutHeight = ReadUInt16LE(data, clutBlockOffset + 10);
        int totalColors = clutWidth * clutHeight;
        int paletteDataBytes = clutBlockSize - 12;
        if (clutWidth <= 0 || clutHeight <= 0 || totalColors <= 0 || paletteDataBytes < totalColors * 2)
        {
            warnings.Add($"TIM palette source '{Path.GetFileName(resolvedTimPath)}' has implausible CLUT dimensions.");
            return null;
        }

        Rgba32[] colors = ReadPsxPalette(data.AsSpan(clutBlockOffset + 12, totalColors * 2), totalColors);
        return new TimClut(Path.GetFullPath(resolvedTimPath), flags, clutWidth, clutHeight, colors);
    }

    private static IReadOnlyList<Rgba32[]> BuildPseudoPaletteGroups(int groupCount, int colorsPerGroup, int seed)
    {
        List<Rgba32[]> groups = new(groupCount);
        for (int group = 0; group < groupCount; group++)
        {
            Rgba32[] colors = new Rgba32[colorsPerGroup];
            colors[0] = new Rgba32(0, 0, 0, 0);
            for (int index = 1; index < colorsPerGroup; index++)
            {
                byte tone = (byte)(16 + (index * 239 / Math.Max(1, colorsPerGroup - 1)));
                byte red = (byte)Math.Clamp(tone + ((group * 37 + seed * 11) % 64) - 32, 0, 255);
                byte green = (byte)Math.Clamp(tone + ((group * 23 + seed * 7) % 64) - 32, 0, 255);
                byte blue = (byte)Math.Clamp(tone + ((group * 13 + seed * 5) % 64) - 32, 0, 255);
                colors[index] = new Rgba32(red, green, blue, 255);
            }
            groups.Add(colors);
        }
        return groups;
    }

    private static PaletteGroupGrid BuildPseudoPaletteGrid(int groupsPerRow, int rowCount, int colorsPerGroup, int seed)
    {
        int safeGroupsPerRow = Math.Max(1, groupsPerRow);
        int safeRowCount = Math.Max(1, rowCount);
        return new(BuildPseudoPaletteGroups(safeGroupsPerRow * safeRowCount, colorsPerGroup, seed), safeGroupsPerRow);
    }

    private static IReadOnlyList<string> BuildUniformGroupSourceLabels(PaletteGroupGrid paletteGrid, string label)
    {
        if (paletteGrid.Count <= 0)
        {
            return Array.Empty<string>();
        }

        string[] labels = new string[paletteGrid.Count];
        Array.Fill(labels, label);
        return labels;
    }

    private static void WriteAncPaletteContextDebug(
        string filePath,
        AncPaletteResolution paletteResolution,
        int gridWidth,
        int gridHeight,
        int texturePageColumns,
        int texturePixelWidth,
        int frameCount)
    {
        var context = new
        {
            paletteResolutionMode = paletteResolution.ResolutionMode,
            gridWidth,
            gridHeight,
            texturePageColumns,
            texturePixelWidth,
            frameCount,
            basePalette = new
            {
                paletteOffset = paletteResolution.BasePaletteOffset,
                previewPaletteIndex = paletteResolution.PreviewPaletteIndex,
                hasRealPalette = paletteResolution.BaseHasRealPalette,
                sourcePaths = paletteResolution.BaseSourcePaths,
                groupsPerRow = paletteResolution.BasePaletteGrid.GroupsPerRow,
                rowCount = paletteResolution.BasePaletteGrid.RowCount,
                groupCount = paletteResolution.BasePaletteGrid.Count,
                uniqueGroupSources = paletteResolution.BaseGroupSourceLabels.Distinct().OrderBy(static label => label).ToArray()
            },
            effectPalette = new
            {
                paletteOffset = paletteResolution.EffectPaletteOffset,
                sourcePaths = paletteResolution.EffectSourcePaths,
                groupsPerRow = paletteResolution.EffectPaletteGrid.GroupsPerRow,
                rowCount = paletteResolution.EffectPaletteGrid.RowCount,
                groupCount = paletteResolution.EffectPaletteGrid.Count,
                uniqueGroupSources = paletteResolution.EffectGroupSourceLabels.Distinct().OrderBy(static label => label).ToArray()
            }
        };

        File.WriteAllText(filePath, JsonSerializer.Serialize(context, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteAncFramePaletteDebug(
        string filePath,
        int frameId,
        FrameDescriptor descriptor,
        AncFrameRenderResult renderResult,
        string ancAttributeBinaryPath,
        string ancCellBinaryPath)
    {
        var paletteUsage = renderResult.TileDebugInfos
            .GroupBy(static tile => new
            {
                tile.Attribute,
                tile.PaletteSelection.PaletteRole,
                tile.PaletteSelection.PaletteIndex,
                tile.PaletteSelection.PaletteGridRow,
                tile.PaletteSelection.PaletteGridColumn,
                tile.PaletteSelection.RowFromBottom,
                tile.PaletteSelection.ColumnOffset,
                tile.PaletteSelection.SourceLabel
            })
            .OrderBy(static group => group.Key.Attribute)
            .ThenBy(static group => group.Key.PaletteIndex)
            .Select(static group => new
            {
                attribute = $"0x{group.Key.Attribute:X2}",
                attributeValue = group.Key.Attribute,
                group.Key.PaletteRole,
                group.Key.PaletteIndex,
                group.Key.PaletteGridRow,
                group.Key.PaletteGridColumn,
                group.Key.RowFromBottom,
                group.Key.ColumnOffset,
                group.Key.SourceLabel,
                tileCount = group.Count()
            })
            .ToArray();

        var frameDebug = new
        {
            frameId,
            descriptor = new
            {
                sourceWord0 = $"0x{descriptor.SourceWord0:X4}",
                sourceWord1 = $"0x{descriptor.SourceWord1:X4}",
                sourceWord2 = $"0x{descriptor.SourceWord2:X4}",
                sourceWord3 = $"0x{descriptor.SourceWord3:X4}",
                sourceWord4 = $"0x{descriptor.SourceWord4:X4}",
                descriptor.Width,
                descriptor.Height,
                descriptor.PageSlot,
                descriptor.PageVariantHighByte
            },
            ancAttributeBinary = new
            {
                fileName = Path.GetFileName(ancAttributeBinaryPath),
                width = descriptor.Width,
                height = descriptor.Height,
                bytesPerPixel = 1,
                layout = "row-major",
                valueEncoding = "raw ANC attribute byte repeated across each 8x8 tile"
            },
            ancCellBinary = new
            {
                fileName = Path.GetFileName(ancCellBinaryPath),
                tileColumns = renderResult.TileColumns,
                tileRows = renderResult.TileRows,
                bytesPerCell = 3,
                layout = "row-major by tile",
                fieldOrder = new[] { "tileCodeLo", "tileCodeHi", "attribute" },
                valueEncoding = "raw 3-byte ANC cell records copied from the visible composition window"
            },
            tileWindow = new
            {
                renderResult.StartTileX,
                renderResult.StartTileY,
                renderResult.TileColumns,
                renderResult.TileRows
            },
            paletteUsage,
            tiles = renderResult.TileDebugInfos.Select(static tile => new
            {
                tile.TileRow,
                tile.TileColumn,
                tile.CellX,
                tile.CellY,
                cellIndex = $"0x{tile.CellIndex:X}",
                tileCode = $"0x{tile.TileCode:X4}",
                attribute = $"0x{tile.Attribute:X2}",
                attributeValue = tile.Attribute,
                tile.PageIndex,
                tile.TileWithinPage,
                tile.AtlasX,
                tile.AtlasY,
                tile.PaletteSelection.PaletteRole,
                tile.PaletteSelection.PaletteIndex,
                tile.PaletteSelection.PaletteGridRow,
                tile.PaletteSelection.PaletteGridColumn,
                tile.PaletteSelection.RowFromBottom,
                tile.PaletteSelection.ColumnOffset,
                tile.PaletteSelection.SourceLabel
            }).ToArray()
        };

        File.WriteAllText(filePath, JsonSerializer.Serialize(frameDebug, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteAncFrameAttributeBinary(
        string filePath,
        AncFrameRenderResult renderResult)
        => File.WriteAllBytes(filePath, renderResult.RawAncAttributes);

    private static void WriteAncFrameCellBinary(
        string filePath,
        AncFrameRenderResult renderResult)
        => File.WriteAllBytes(filePath, renderResult.RawAncCells);

    private static void UpdateAncPaletteUsageSummary(
        IDictionary<AncPaletteUsageSummaryKey, AncPaletteUsageAccumulator> usageSummary,
        int frameId,
        IReadOnlyList<AncTilePaletteDebugInfo> tileDebugInfos)
    {
        foreach (AncTilePaletteDebugInfo tile in tileDebugInfos)
        {
            AncPaletteUsageSummaryKey key = new(
                tile.Attribute,
                tile.PaletteSelection.PaletteRole,
                tile.PaletteSelection.PaletteIndex,
                tile.PaletteSelection.PaletteGridRow,
                tile.PaletteSelection.PaletteGridColumn,
                tile.PaletteSelection.SourceLabel);

            if (!usageSummary.TryGetValue(key, out AncPaletteUsageAccumulator? accumulator))
            {
                accumulator = new AncPaletteUsageAccumulator();
                usageSummary[key] = accumulator;
            }

            accumulator.TileCount++;
            accumulator.FrameIds.Add(frameId);
        }
    }

    private static void WriteAncPaletteUsageSummary(
        string filePath,
        IReadOnlyDictionary<AncPaletteUsageSummaryKey, AncPaletteUsageAccumulator> usageSummary)
    {
        var summary = usageSummary
            .OrderBy(static entry => entry.Key.Attribute)
            .ThenBy(static entry => entry.Key.PaletteIndex)
            .Select(static entry => new
            {
                attribute = $"0x{entry.Key.Attribute:X2}",
                attributeValue = entry.Key.Attribute,
                entry.Key.PaletteRole,
                entry.Key.PaletteIndex,
                entry.Key.PaletteGridRow,
                entry.Key.PaletteGridColumn,
                entry.Key.SourceLabel,
                tileCount = entry.Value.TileCount,
                frameIds = entry.Value.FrameIds.OrderBy(static frameId => frameId).ToArray()
            })
            .ToArray();

        File.WriteAllText(filePath, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static Rgba32 ConvertPsxColor(ushort raw)
    {
        if ((raw & 0x7FFF) == 0)
        {
            return new Rgba32(0, 0, 0, 0);
        }

        byte red = Scale5To8(raw & 0x1F);
        byte green = Scale5To8((raw >> 5) & 0x1F);
        byte blue = Scale5To8((raw >> 10) & 0x1F);
        return new Rgba32(red, green, blue, 255);
    }

    private static byte Scale5To8(int value)
        => (byte)((value * 255 + 15) / 31);

    private static int ReadInt32LE(byte[] data, int offset)
        => BitConverter.ToInt32(data, offset);

    private static int ReadInt32BE(byte[] data, int offset)
        => (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];

    private static ushort ReadUInt16LE(byte[] data, int offset)
        => (ushort)(data[offset] | (data[offset + 1] << 8));

    private static ushort ReadUInt16BE(byte[] data, int offset)
        => (ushort)((data[offset] << 8) | data[offset + 1]);

    private static short ReadInt16BE(byte[] data, int offset)
        => unchecked((short)ReadUInt16BE(data, offset));

    private enum AssetFormat
    {
        Anm,
        Ann,
        Anc
    }

    private readonly record struct CommonHeader(int TextureOffset, int[] SequenceOffsets);

    private readonly record struct TimClut(string SourcePath, int Flags, int ClutWidth, int ClutHeight, Rgba32[] Colors)
    {
        public IReadOnlyList<Rgba32[]> ToPaletteGroups(int colorsPerGroup)
        {
            if (colorsPerGroup <= 0 || Colors.Length < colorsPerGroup)
            {
                return Array.Empty<Rgba32[]>();
            }

            int groupCount = Colors.Length / colorsPerGroup;
            List<Rgba32[]> groups = new(groupCount);
            for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
            {
                Rgba32[] group = new Rgba32[colorsPerGroup];
                Array.Copy(Colors, groupIndex * colorsPerGroup, group, 0, colorsPerGroup);
                groups.Add(group);
            }
            return groups;
        }

        public PaletteGroupGrid ToPaletteGrid(int colorsPerGroup)
        {
            IReadOnlyList<Rgba32[]> groups = ToPaletteGroups(colorsPerGroup);
            if (groups.Count == 0)
            {
                return new(Array.Empty<Rgba32[]>(), 0);
            }

            int groupsPerRow = colorsPerGroup > 0 && ClutWidth >= colorsPerGroup && ClutWidth % colorsPerGroup == 0
                ? ClutWidth / colorsPerGroup
                : groups.Count;

            return new(groups, Math.Max(1, groupsPerRow));
        }
    }

    private readonly record struct PaletteGroupGrid(IReadOnlyList<Rgba32[]> Groups, int GroupsPerRow)
    {
        public int Count => Groups.Count;

        public int RowCount => GroupsPerRow <= 0 ? 0 : (Groups.Count + GroupsPerRow - 1) / GroupsPerRow;
    }

    private readonly record struct AncPaletteResolution(
        PaletteGroupGrid BasePaletteGrid,
        int BasePaletteOffset,
        int PreviewPaletteIndex,
        bool BaseHasRealPalette,
        string BaseWarning,
        IReadOnlyList<string> BaseSourcePaths,
        IReadOnlyList<string> BaseGroupSourceLabels,
        PaletteGroupGrid EffectPaletteGrid,
        int EffectPaletteOffset,
        string? EffectWarning,
        IReadOnlyList<string> EffectSourcePaths,
        IReadOnlyList<string> EffectGroupSourceLabels,
        string ResolutionMode);

    private readonly record struct AncTextureLayout(
        int GridWidth,
        int GridHeight,
        int TexturePageColumns,
        int CompositionTableBytes,
        int CompositionTableOffset,
        int TextureDataOffset,
        int TexturePixelWidth,
        int ExpectedPixelBytes);

    private sealed record AncFrameRenderResult(
        Image<Rgba32> Image,
        byte[] RawAncAttributes,
        byte[] RawAncCells,
        IReadOnlyList<AncTilePaletteDebugInfo> TileDebugInfos,
        int StartTileX,
        int StartTileY,
        int TileColumns,
        int TileRows);

    private readonly record struct AncTilePaletteDebugInfo(
        int TileRow,
        int TileColumn,
        int CellX,
        int CellY,
        int CellIndex,
        ushort TileCode,
        int Attribute,
        int PageIndex,
        int TileWithinPage,
        int AtlasX,
        int AtlasY,
        AncPaletteSelection PaletteSelection);

    private readonly record struct AncPaletteSelection(
        string PaletteRole,
        int PaletteIndex,
        int PaletteGridRow,
        int PaletteGridColumn,
        int RowFromBottom,
        int ColumnOffset,
        string SourceLabel);

    private readonly record struct AncPaletteUsageSummaryKey(
        int Attribute,
        string PaletteRole,
        int PaletteIndex,
        int PaletteGridRow,
        int PaletteGridColumn,
        string SourceLabel);

    private readonly record struct SequenceFrameStep(
        int StepIndex,
        int FrameId,
        short OffsetX,
        short OffsetY,
        short GlobalOffsetX,
        short GlobalOffsetY,
        ushort ScaleX,
        ushort ScaleY,
        ushort Rotation,
        byte RenderFlags,
        byte TransformFlags)
    {
        public int DrawX => OffsetX + GlobalOffsetX;

        public int DrawY => OffsetY + GlobalOffsetY;

        public bool FlipVertical => (TransformFlags & 0x01) != 0;

        public bool FlipHorizontal => (TransformFlags & 0x02) != 0;
    }

    private readonly record struct SequenceAlignedFramePlan(
        int StepIndex,
        int FrameId,
        string SourceFramePath,
        int SourceWidth,
        int SourceHeight,
        int DrawX,
        int DrawY,
        int OutputWidth,
        int OutputHeight,
        ushort ScaleX,
        ushort ScaleY,
        ushort Rotation,
        byte RenderFlags,
        byte TransformFlags)
    {
        public bool FlipVertical => (TransformFlags & 0x01) != 0;

        public bool FlipHorizontal => (TransformFlags & 0x02) != 0;
    }

    private sealed class AncPaletteUsageAccumulator
    {
        public int TileCount { get; set; }

        public HashSet<int> FrameIds { get; } = new();
    }

    private sealed class SequenceTraceState
    {
        public short OffsetX { get; set; }

        public short OffsetY { get; set; }

        public short GlobalOffsetX { get; set; }

        public short GlobalOffsetY { get; set; }

        public ushort ScaleX { get; set; } = 100;

        public ushort ScaleY { get; set; } = 100;

        public ushort Rotation { get; set; }

        public byte RenderFlags { get; set; }

        public byte TransformFlags { get; set; }
    }

    private readonly record struct FrameDescriptor(
        int FrameId,
        ushort SourceWord0,
        ushort SourceWord1,
        ushort SourceWord2,
        ushort SourceWord3,
        ushort SourceWord4)
    {
        public int PageSlot => SourceWord4 & 0xFF;

        public int PageVariantHighByte => SourceWord4 >> 8;

        public int Width => GetWrappedDescriptorSpan(SourceWord0, SourceWord2);

        public int Height => GetWrappedDescriptorSpan(SourceWord1, SourceWord3);

        public bool IsPlausible =>
            Width > 0 && Height > 0 && Width <= 512 && Height <= 512 && SourceWord4 != 0xFB40;
    }

    private static int GetWrappedDescriptorSpan(ushort start, ushort end)
    {
        int span = (((end & 0xFF) - (start & 0xFF)) + 1) & 0xFF;
        return span == 0 ? 256 : span;
    }

    private sealed class TexturePage
    {
        private readonly byte[] _indices;
        private readonly IReadOnlyList<Rgba32[]> _palettes;

        private TexturePage(int width, int height, int bitsPerPixel, byte[] indices, IReadOnlyList<Rgba32[]> palettes, bool hasRealPalette, string label)
        {
            Width = width;
            Height = height;
            BitsPerPixel = bitsPerPixel;
            _indices = indices;
            _palettes = palettes;
            HasRealPalette = hasRealPalette;
            Label = label;
        }

        public int Width { get; }

        public int Height { get; }

        public int BitsPerPixel { get; }

        public bool HasRealPalette { get; }

        public int PaletteCount => _palettes.Count;

        public string Label { get; }

        public static TexturePage From4Bpp(ReadOnlySpan<byte> packedPixels, int width, int height, IReadOnlyList<Rgba32[]> palettes, bool hasRealPalette, string label)
        {
            byte[] indices = new byte[width * height];
            int pixelIndex = 0;
            foreach (byte packed in packedPixels)
            {
                if (pixelIndex < indices.Length)
                {
                    indices[pixelIndex++] = (byte)(packed & 0x0F);
                }
                if (pixelIndex < indices.Length)
                {
                    indices[pixelIndex++] = (byte)(packed >> 4);
                }
            }
            return new TexturePage(width, height, 4, indices, palettes, hasRealPalette, label);
        }

        public static TexturePage From8Bpp(ReadOnlySpan<byte> packedPixels, int width, int height, Rgba32[] palette, bool hasRealPalette, string label)
        {
            byte[] indices = packedPixels.Slice(0, Math.Min(packedPixels.Length, width * height)).ToArray();
            if (indices.Length < width * height)
            {
                Array.Resize(ref indices, width * height);
            }
            return new TexturePage(width, height, 8, indices, new[] { palette }, hasRealPalette, label);
        }

        public byte GetIndex(int x, int y)
        {
            if ((uint)x >= Width || (uint)y >= Height)
            {
                return 0;
            }
            return _indices[y * Width + x];
        }

        public Image<Rgba32> RenderToImage(int paletteIndex)
        {
            Image<Rgba32> image = new(Width, Height);
            Rgba32[] palette = _palettes[Math.Clamp(paletteIndex, 0, _palettes.Count - 1)];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    image[x, y] = palette[GetIndex(x, y) % palette.Length];
                }
            }
            return image;
        }

        public Image<Rgba32> Crop(int sourceX, int sourceY, int width, int height, int paletteIndex)
        {
            Image<Rgba32> image = new(Math.Max(1, width), Math.Max(1, height), new Rgba32(0, 0, 0, 0));
            Rgba32[] palette = _palettes[Math.Clamp(paletteIndex, 0, _palettes.Count - 1)];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte index = GetIndex(sourceX + x, sourceY + y);
                    image[x, y] = palette[index % palette.Length];
                }
            }
            return image;
        }
    }

    private sealed class AncComposition
    {
        private readonly byte[] _cells;
        private readonly PaletteGroupGrid _basePaletteGrid;
        private readonly int _basePaletteOffset;
        private readonly IReadOnlyList<string> _baseGroupSourceLabels;
        private readonly PaletteGroupGrid _effectPaletteGrid;
        private readonly int _effectPaletteOffset;
        private readonly IReadOnlyList<string> _effectGroupSourceLabels;

        public AncComposition(
            int gridWidth,
            int gridHeight,
            byte[] cells,
            TexturePage atlas,
            PaletteGroupGrid basePaletteGrid,
            int basePaletteOffset,
            IReadOnlyList<string> baseGroupSourceLabels,
            PaletteGroupGrid effectPaletteGrid,
            int effectPaletteOffset,
            IReadOnlyList<string> effectGroupSourceLabels)
        {
            GridWidth = gridWidth;
            GridHeight = gridHeight;
            _cells = cells;
            Atlas = atlas;
            _basePaletteGrid = basePaletteGrid;
            _basePaletteOffset = basePaletteOffset;
            _baseGroupSourceLabels = baseGroupSourceLabels;
            _effectPaletteGrid = effectPaletteGrid;
            _effectPaletteOffset = effectPaletteOffset;
            _effectGroupSourceLabels = effectGroupSourceLabels;
        }

        public int GridWidth { get; }

        public int GridHeight { get; }

        public TexturePage Atlas { get; }

        public AncFrameRenderResult? RenderFrame(FrameDescriptor descriptor)
        {
            int width = descriptor.Width;
            int height = descriptor.Height;
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            int tileColumns = width / 8;
            int tileRows = height / 8;
            if (tileColumns <= 0 || tileRows <= 0)
            {
                return null;
            }

            int startTileX = descriptor.SourceWord0 >> 3;
            int startTileY = descriptor.SourceWord1 >> 3;
            if (startTileX < 0 || startTileY < 0 || startTileX >= GridWidth || startTileY >= GridHeight)
            {
                return null;
            }

            Image<Rgba32> image = new(width, height, new Rgba32(0, 0, 0, 0));
            byte[] rawAncAttributes = new byte[width * height];
            byte[] rawAncCells = new byte[tileColumns * tileRows * 3];
            List<AncTilePaletteDebugInfo> tileDebugInfos = new(tileColumns * tileRows);

            for (int tileRow = 0; tileRow < tileRows; tileRow++)
            {
                for (int tileColumn = 0; tileColumn < tileColumns; tileColumn++)
                {
                    int cellIndex = ((startTileY + tileRow) * GridWidth + (startTileX + tileColumn)) * 3;
                    if (cellIndex + 2 >= _cells.Length)
                    {
                        continue;
                    }

                    ushort tileCode = (ushort)(_cells[cellIndex] | (_cells[cellIndex + 1] << 8));
                    int attribute = _cells[cellIndex + 2] & 0xFF;
                    int tileCellOffset = (tileRow * tileColumns + tileColumn) * 3;
                    rawAncCells[tileCellOffset] = _cells[cellIndex];
                    rawAncCells[tileCellOffset + 1] = _cells[cellIndex + 1];
                    rawAncCells[tileCellOffset + 2] = _cells[cellIndex + 2];
                    int pageIndex = tileCode / AncTilesPerPage;
                    int tileWithinPage = tileCode % AncTilesPerPage;
                    int atlasX = pageIndex * 256 + (tileWithinPage % AncTilesPerPageRow) * 8;
                    int atlasY = (tileWithinPage / AncTilesPerPageRow) * 8;
                    bool usesBasePalette = attribute == 0 || _effectPaletteGrid.Count == 0;
                    PaletteGroupGrid paletteGrid = usesBasePalette
                        ? _basePaletteGrid
                        : _effectPaletteGrid;
                    int paletteOffset = usesBasePalette
                        ? _basePaletteOffset
                        : _effectPaletteOffset;
                    IReadOnlyList<string> groupSourceLabels = usesBasePalette
                        ? _baseGroupSourceLabels
                        : _effectGroupSourceLabels;
                    string paletteRole = usesBasePalette ? "base" : "effect";
                    AncPaletteSelection paletteSelection = ResolveAncPaletteSelection(paletteGrid, paletteOffset, attribute, paletteRole, groupSourceLabels);
                    Rgba32[] palette = paletteGrid.Count <= 0
                        ? TransparentPalette
                        : paletteGrid.Groups[paletteSelection.PaletteIndex];

                    tileDebugInfos.Add(new(
                        tileRow,
                        tileColumn,
                        startTileX + tileColumn,
                        startTileY + tileRow,
                        cellIndex,
                        tileCode,
                        attribute,
                        pageIndex,
                        tileWithinPage,
                        atlasX,
                        atlasY,
                        paletteSelection));

                    for (int y = 0; y < 8; y++)
                    {
                        for (int x = 0; x < 8; x++)
                        {
                            byte index = Atlas.GetIndex(atlasX + x, atlasY + y);
                            int pixelX = tileColumn * 8 + x;
                            int pixelY = tileRow * 8 + y;
                            int pixelOffset = pixelY * width + pixelX;
                            rawAncAttributes[pixelOffset] = (byte)attribute;
                            image[pixelX, pixelY] = palette[index % palette.Length];
                        }
                    }
                }
            }

            return new(image, rawAncAttributes, rawAncCells, tileDebugInfos, startTileX, startTileY, tileColumns, tileRows);
        }

        private static AncPaletteSelection ResolveAncPaletteSelection(
            PaletteGroupGrid paletteGrid,
            int baseGroupOffset,
            int attribute,
            string paletteRole,
            IReadOnlyList<string> groupSourceLabels)
        {
            if (paletteGrid.Count <= 0)
            {
                return new(paletteRole, 0, 0, 0, attribute >> 2, attribute < 0x20 ? (attribute & 0x03) : (((attribute - 0x20) & 0x03) + 4), "transparent-fallback");
            }

            int groupsPerRow = Math.Max(1, paletteGrid.GroupsPerRow);
            int rowFromBottom = attribute >> 2;
            int columnOffset = attribute < 0x20
                ? (attribute & 0x03)
                : (((attribute - 0x20) & 0x03) + 4);
            int runtimeBaseGroupIndex = baseGroupOffset + Math.Max(0, paletteGrid.RowCount - 1) * groupsPerRow;
            int paletteIndex = runtimeBaseGroupIndex - rowFromBottom * groupsPerRow + columnOffset;
            int wrappedPaletteIndex = GetWrappedPaletteIndex(paletteIndex, paletteGrid.Count);
            int paletteGridRow = groupsPerRow <= 0 ? 0 : wrappedPaletteIndex / groupsPerRow;
            int paletteGridColumn = groupsPerRow <= 0 ? 0 : wrappedPaletteIndex % groupsPerRow;
            string sourceLabel = wrappedPaletteIndex < groupSourceLabels.Count
                ? groupSourceLabels[wrappedPaletteIndex]
                : "unknown";
            return new(paletteRole, wrappedPaletteIndex, paletteGridRow, paletteGridColumn, rowFromBottom, columnOffset, sourceLabel);
        }
    }
}

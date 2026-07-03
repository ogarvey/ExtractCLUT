namespace BladeRunnerSliceExporter;

// Mirrors BladeRunnerEngine::getResourceStream (bladerunner.cpp):
//   1) if a loose extracted file exists on disk, use it directly
//   2) otherwise search the opened MIX archives in order
// (Enhanced Edition .kpf/zip support could be added as a third branch.)
public sealed class ResourceResolver : IDisposable
{
    private readonly string _gameDir;
    private readonly List<MixArchive> _archives = new();

    public ResourceResolver(string gameDir) => _gameDir = gameDir;

    // Open any of the given MIX files that exist. Order matters: earlier archives win,
    // matching the engine's archive search order. A.MIX holds INDEX.DAT / PALETTES.DAT.
    public void OpenArchives(IEnumerable<string> mixFileNames)
    {
        foreach (var fileName in mixFileNames)
        {
            string path = Path.Combine(_gameDir, fileName);
            if (MixArchive.Exists(path))
            {
                try
                {
                    _archives.Add(MixArchive.Open(path));
                    Console.WriteLine($"Opened archive {fileName}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to open {fileName}: {ex.Message}");
                }
            }
        }
    }

    // Returns the resource bytes or null if not found anywhere.
    public byte[]? GetResource(string name)
    {
        // 1) Loose file on disk (already-extracted resource).
        string loose = Path.Combine(_gameDir, name);
        if (File.Exists(loose))
            return File.ReadAllBytes(loose);

        // 2) Search MIX archives.
        foreach (var mix in _archives)
        {
            var data = mix.ReadMember(name);
            if (data != null)
                return data;
        }
        return null;
    }

    public byte[] GetResourceRequired(string name) =>
        GetResource(name) ?? throw new FileNotFoundException(
            $"Resource '{name}' not found on disk or in any opened MIX archive.");

    public void Dispose()
    {
        foreach (var a in _archives) a.Dispose();
        _archives.Clear();
    }
}

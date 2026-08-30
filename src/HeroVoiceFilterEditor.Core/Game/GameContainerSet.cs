using UAssetTool.ZenPackage;

namespace HeroVoiceFilterEditor.Core.Game;

/// Owns the loaded IoStore containers and the package index built from them.
public sealed class GameContainerSet : IDisposable
{
    private readonly FZenPackageContext _context;
    private readonly List<string> _containerFiles = new();

    private GameContainerSet(FZenPackageContext context, string paksDirectory, string build)
    {
        _context = context;
        PaksDirectory = paksDirectory;
        Build = build;
    }

    public FZenPackageContext Context => _context;

    public string PaksDirectory { get; }

    public string Build { get; }

    public int ContainerCount => _containerFiles.Count;

    public int PackageCount => _context.PackageCount;

    public static GameContainerSet Open(string paksDirectory, string? aesKeyHex = null, IProgress<string>? log = null)
    {
        if (!GameLocator.IsPaksDirectory(paksDirectory))
            throw new DirectoryNotFoundException($"Not a Marvel Rivals Paks directory (no {GameDefaults.GlobalContainerName}): {paksDirectory}");

        string[] containers = Directory.GetFiles(paksDirectory, "*.utoc", SearchOption.TopDirectoryOnly);
        var context = new FZenPackageContext();
        var set = new GameContainerSet(context, paksDirectory, ContainerOrdering.DescribeBuild(containers));

        try
        {
            context.SetAesKey(GameDefaults.NormalizeAesKey(aesKeyHex));

            string globalPath = Path.Combine(paksDirectory, GameDefaults.GlobalContainerName);
            log?.Report($"Loading {GameDefaults.GlobalContainerName}");
            context.LoadContainer(globalPath);
            set._containerFiles.Add(globalPath);
            context.LoadScriptObjectsFromContainer(0);

            foreach (string container in ContainerOrdering.Order(containers))
            {
                try
                {
                    context.LoadContainer(container);
                    set._containerFiles.Add(container);
                }
                catch (Exception ex)
                {
                    log?.Report($"Skipped {Path.GetFileName(container)}: {ex.Message}");
                }
            }

            log?.Report($"Loaded {set.ContainerCount} containers, {set.PackageCount} packages, build {set.Build}");
            return set;
        }
        catch
        {
            context.Dispose();
            throw;
        }
    }

    public ulong? ResolvePackage(string packagePath)
    {
        ulong id = FPackageId.FromName(packagePath);
        if (_context.HasPackage(id))
            return id;

        return _context.FindPackageIdByPath(packagePath);
    }

    /// File name of the container a package was ultimately resolved from, for provenance display.
    public string ContainerNameFor(ulong packageId)
    {
        int index = _context.GetPackageContainerIndex(packageId);
        return index >= 0 && index < _containerFiles.Count
            ? Path.GetFileName(_containerFiles[index])
            : "unknown";
    }

    public IEnumerable<(ulong Id, string PackagePath)> PackagesUnder(string packagePathPrefix)
    {
        string prefix = packagePathPrefix.EndsWith('/') ? packagePathPrefix : packagePathPrefix + "/";

        foreach (ulong id in _context.GetAllPackageIds())
        {
            string? path = _context.GetPackagePath(id);
            if (!string.IsNullOrEmpty(path) && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                yield return (id, path);
        }
    }

    public void Dispose() => _context.Dispose();
}

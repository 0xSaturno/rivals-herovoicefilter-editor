using HeroVoiceFilterEditor.Core.Effects;
using HeroVoiceFilterEditor.Core.Game;
using HeroVoiceFilterEditor.Core.Metadata;
using HeroVoiceFilterEditor.Core.Project;
using HeroVoiceFilterEditor.Core.Table;
using UAssetAPI.Unversioned;

namespace HeroVoiceFilterEditor.Core.Session;

public enum SessionReadiness
{
    /// Everything needed to edit is loaded.
    Ready,

    /// Usmap and hero names are loaded, but no vanilla table has been extracted yet.
    NeedsExtraction,

    /// The game directory is not configured or not valid.
    NeedsGameDirectory,

    /// No usmap could be obtained, so nothing can be parsed.
    NeedsUsmap
}

/// Ties the settings, metadata caches, extracted vanilla table and open document together.
/// Deliberately UI-free so it can be driven from tests as well as the editor.
public sealed class EditorSession
{
    public AppSettings Settings { get; private set; } = new();

    public HeroSkinCatalog Heroes { get; private set; } = HeroSkinCatalog.Parse(string.Empty);

    public EffectCatalog? Effects { get; private set; }

    public VanillaSnapshot? Snapshot { get; private set; }

    public VoiceDataDocument? Document { get; private set; }

    public Usmap? Mappings { get; private set; }

    public string? UsmapName { get; private set; }

    public string Workspace => Settings.EffectiveWorkspace;

    public SessionReadiness Readiness
    {
        get
        {
            if (Mappings is null) return SessionReadiness.NeedsUsmap;
            if (Document is not null && Effects is not null) return SessionReadiness.Ready;
            if (!GameLocator.IsPaksDirectory(Settings.PaksDirectory)) return SessionReadiness.NeedsGameDirectory;
            return SessionReadiness.NeedsExtraction;
        }
    }

    /// Build the game is on, read from container file names only, so this stays cheap.
    public string? DetectBuild()
    {
        if (!GameLocator.IsPaksDirectory(Settings.PaksDirectory))
            return null;

        return ContainerOrdering.DescribeBuild(Directory.GetFiles(Settings.PaksDirectory!, "*.utoc", SearchOption.TopDirectoryOnly));
    }

    public async Task InitializeAsync(IProgress<string>? log = null, CancellationToken cancellationToken = default)
    {
        Settings = SettingsService.Load();
        if (Settings.ApplyDefaults())
            SettingsService.Save(Settings);

        log?.Report($"Game: {Settings.PaksDirectory ?? "not configured"}");

        await LoadMetadataAsync(log, cancellationToken);
        TryLoadCachedVanilla(log);
    }

    public async Task LoadMetadataAsync(IProgress<string>? log = null, CancellationToken cancellationToken = default)
    {
        bool checkRemote = Settings.CheckForUpdatesOnLaunch;

        (HeroSkinCatalog heroes, CacheStatus heroStatus, string heroDetail) =
            await HeroSkinCatalog.EnsureCurrentAsync(checkRemote: checkRemote, cancellationToken: cancellationToken);
        Heroes = heroes;
        log?.Report($"Hero names: {heroStatus} — {heroDetail}");

        if (!string.IsNullOrWhiteSpace(Settings.UsmapOverridePath) && File.Exists(Settings.UsmapOverridePath))
        {
            Mappings = UsmapService.Load(Settings.UsmapOverridePath);
            UsmapName = Path.GetFileName(Settings.UsmapOverridePath);
            log?.Report($"Usmap: pinned override {UsmapName}");
            return;
        }

        UsmapResult usmap = await new UsmapService().EnsureCurrentAsync(checkRemote, cancellationToken);
        log?.Report($"Usmap: {usmap.Status} — {usmap.Detail}");

        if (usmap.IsUsable)
        {
            Mappings = UsmapService.Load(usmap.Path!);
            UsmapName = Path.GetFileName(usmap.Path);
        }
    }

    /// Reuses an earlier extraction for the build the game is currently on, avoiding the
    /// multi-second container scan on every launch.
    public bool TryLoadCachedVanilla(IProgress<string>? log = null)
    {
        string? build = DetectBuild();
        if (build is null)
            return false;

        VanillaSnapshot? snapshot = WorkspaceExtractor.LoadExisting(Workspace, build);
        if (snapshot is null || !File.Exists(snapshot.AssetPath))
            return false;

        EffectCatalog? effects = EffectCatalog.Load(snapshot.SnapshotRoot);
        if (effects is null)
            return false;

        Snapshot = snapshot;
        Effects = effects;
        log?.Report($"Workspace: reusing build {build} from {snapshot.SourceContainer}");

        OpenDocument();
        return true;
    }

    public Task RefreshFromGameAsync(IProgress<string>? log = null, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            if (!GameLocator.IsPaksDirectory(Settings.PaksDirectory))
                throw new InvalidOperationException("Set a valid Marvel Rivals Paks directory first.");

            using GameContainerSet containers = GameContainerSet.Open(Settings.PaksDirectory!, Settings.AesKey, log);
            cancellationToken.ThrowIfCancellationRequested();

            Snapshot = WorkspaceExtractor.ExtractVoiceData(containers, Workspace, log);
            Effects = EffectCatalog.Build(containers, log);
            Effects.Save(Snapshot.SnapshotRoot);

            OpenDocument();
        }, cancellationToken);

    public void OpenDocument()
    {
        if (Snapshot is null || Mappings is null)
            return;

        Document = VoiceDataDocument.Load(Snapshot.AssetPath, Mappings);
    }

    public void SaveAsset(string path)
    {
        if (Document is null)
            throw new InvalidOperationException("Nothing is loaded.");

        Document.Save(path);
    }

    /// Default export location, mirroring the game's own mount layout so the result can be
    /// packed as-is by an external tool.
    public string DefaultExportPath() =>
        Path.Combine(Workspace, "export",
            (Snapshot?.RelativeAssetPath ?? VoiceDataPaths.VoiceDataContainerPath).Replace('/', Path.DirectorySeparatorChar));

    public FilterProject CreateProject() =>
        Document is null
            ? throw new InvalidOperationException("Nothing is loaded.")
            : FilterProject.FromDocument(Document, Snapshot?.Build);

    public ReplayReport ApplyProject(FilterProject project, ConflictPolicy policy = ConflictPolicy.Skip)
    {
        if (Document is null || Effects is null)
            throw new InvalidOperationException("Nothing is loaded.");

        return ReplayEngine.Apply(Document, project, Effects, policy);
    }

    /// Reloads vanilla and replays the project onto it, which is the post-patch workflow.
    public ReplayReport ReloadAndApply(FilterProject project, ConflictPolicy policy = ConflictPolicy.Skip)
    {
        OpenDocument();
        return ApplyProject(project, policy);
    }
}

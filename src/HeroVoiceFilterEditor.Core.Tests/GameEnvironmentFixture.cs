using HeroVoiceFilterEditor.Core.Effects;
using HeroVoiceFilterEditor.Core.Game;
using HeroVoiceFilterEditor.Core.Metadata;
using UAssetAPI.Unversioned;
using Xunit;

namespace HeroVoiceFilterEditor.Core.Tests;

/// Extracts the real vanilla table once per test run and shares it across every test that
/// needs live game data. Tests that depend on it call Skip.IfNot(fixture.IsAvailable, ...)
/// rather than failing outright, so the suite still runs somewhere without the game installed.
public sealed class GameEnvironmentFixture : IAsyncLifetime
{
    private static readonly string TestCacheRoot = Path.Combine(Path.GetTempPath(), "HeroVoiceFilterEditorTests");

    public bool IsAvailable { get; private set; }

    public string SkipReason { get; private set; } = string.Empty;

    public VanillaSnapshot Snapshot { get; private set; } = null!;

    public EffectCatalog Effects { get; private set; } = null!;

    public Usmap Mappings { get; private set; } = null!;

    public HeroSkinCatalog Heroes { get; private set; } = null!;

    /// Call at the top of every [SkippableFact] that needs live game data.
    public void RequireAvailable() => Skip.IfNot(IsAvailable, SkipReason);

    public async Task InitializeAsync()
    {
        string? paks = GameLocator.FindCandidates().FirstOrDefault();
        if (paks is null)
        {
            SkipReason = "No Marvel Rivals install found on this machine.";
            return;
        }

        var usmapService = new UsmapService(Path.Combine(TestCacheRoot, "usmap"));
        UsmapResult usmap = await usmapService.EnsureCurrentAsync();
        if (!usmap.IsUsable)
        {
            SkipReason = $"No usmap available: {usmap.Detail}";
            return;
        }

        Mappings = UsmapService.Load(usmap.Path!);

        (HeroSkinCatalog heroes, _, _) = await HeroSkinCatalog.EnsureCurrentAsync(Path.Combine(TestCacheRoot, "metadata"));
        Heroes = heroes;

        string workspace = Path.Combine(TestCacheRoot, "workspace");
        string build = ContainerOrdering.DescribeBuild(Directory.GetFiles(paks, "*.utoc", SearchOption.TopDirectoryOnly));

        VanillaSnapshot? cachedSnapshot = WorkspaceExtractor.LoadExisting(workspace, build);
        EffectCatalog? cachedEffects = cachedSnapshot is null ? null : EffectCatalog.Load(cachedSnapshot.SnapshotRoot);

        if (cachedSnapshot is not null && cachedEffects is not null)
        {
            Snapshot = cachedSnapshot;
            Effects = cachedEffects;
        }
        else
        {
            using GameContainerSet containers = GameContainerSet.Open(paks);
            Snapshot = WorkspaceExtractor.ExtractVoiceData(containers, workspace);
            Effects = EffectCatalog.Build(containers);
            Effects.Save(Snapshot.SnapshotRoot);
        }

        IsAvailable = true;
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition(Name)]
public sealed class GameEnvironmentCollection : ICollectionFixture<GameEnvironmentFixture>
{
    public const string Name = "Game environment";
}

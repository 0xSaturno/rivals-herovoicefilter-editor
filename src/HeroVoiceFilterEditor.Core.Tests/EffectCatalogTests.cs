using HeroVoiceFilterEditor.Core.Effects;
using Xunit;

namespace HeroVoiceFilterEditor.Core.Tests;

[Collection(GameEnvironmentCollection.Name)]
public class EffectCatalogTests
{
    private readonly GameEnvironmentFixture _env;

    public EffectCatalogTests(GameEnvironmentFixture env) => _env = env;

    [SkippableFact]
    public void Effects_AreAllUnderTheEffectVoFolder()
    {
        _env.RequireAvailable();
        Assert.NotEmpty(_env.Effects.Effects);
        Assert.All(_env.Effects.Effects, e =>
            Assert.StartsWith("/Game/Marvel/Wwise/Assets/Effects/effect_vo", e.PackagePath));
    }

    [SkippableFact]
    public void Families_CoverEveryEffect()
    {
        _env.RequireAvailable();
        int total = _env.Effects.Families.Sum(f => f.Members.Count);
        Assert.Equal(_env.Effects.Effects.Count, total);
    }

    [SkippableFact]
    public void SaveThenLoad_RoundTripsTheSameEffectSet()
    {
        _env.RequireAvailable();
        string dir = Path.Combine(Path.GetTempPath(), $"hvfe-catalog-{Guid.NewGuid():N}");
        try
        {
            _env.Effects.Save(dir);
            EffectCatalog? reloaded = EffectCatalog.Load(dir);

            Assert.NotNull(reloaded);
            Assert.Equal(
                _env.Effects.Effects.Select(e => e.PackagePath).OrderBy(p => p),
                reloaded!.Effects.Select(e => e.PackagePath).OrderBy(p => p));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [SkippableFact]
    public void Contains_IsTrueForAKnownEffect_AndFalseForAMadeUpOne()
    {
        _env.RequireAvailable();
        string real = _env.Effects.Effects.First().PackagePath;
        Assert.True(_env.Effects.Contains(real));
        Assert.False(_env.Effects.Contains(real + "_does_not_exist"));
    }
}

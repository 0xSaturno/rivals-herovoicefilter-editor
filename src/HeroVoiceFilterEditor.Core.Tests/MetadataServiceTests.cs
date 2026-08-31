using HeroVoiceFilterEditor.Core.Game;
using HeroVoiceFilterEditor.Core.Metadata;
using Xunit;

namespace HeroVoiceFilterEditor.Core.Tests;

/// Live network round-trip against the real usmap manifest and hero-id markdown. Loose
/// bounds only — both sources grow as the game gets patched, so exact counts would rot.
public class MetadataServiceTests
{
    [SkippableFact]
    public async Task UsmapService_FirstFetchDownloads_SecondFetchHitsCache()
    {
        string cacheDir = Path.Combine(Path.GetTempPath(), $"hvfe-usmap-{Guid.NewGuid():N}");
        try
        {
            var service = new UsmapService(cacheDir);

            UsmapResult empty = await service.EnsureCurrentAsync(checkRemote: false);
            Assert.Equal(CacheStatus.Unavailable, empty.Status);

            UsmapResult first = await service.EnsureCurrentAsync();
            Skip.If(first.Status is CacheStatus.Unavailable or CacheStatus.Offline, "No network access.");
            Assert.Equal(CacheStatus.Downloaded, first.Status);
            Assert.True(first.IsUsable);

            UsmapResult second = await service.EnsureCurrentAsync();
            Assert.Equal(CacheStatus.UpToDate, second.Status);
            Assert.Equal(first.Path, second.Path);
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }

    [SkippableFact]
    public async Task HeroSkinCatalog_FetchesAPlausibleNumberOfHeroesAndSkins()
    {
        string cacheDir = Path.Combine(Path.GetTempPath(), $"hvfe-heroes-{Guid.NewGuid():N}");
        try
        {
            (HeroSkinCatalog catalog, CacheStatus status, _) = await HeroSkinCatalog.EnsureCurrentAsync(cacheDir);
            Skip.If(status == CacheStatus.Unavailable, "No network access.");

            Assert.True(catalog.Heroes.Count > 50, $"expected a lot more than 50 heroes, got {catalog.Heroes.Count}");
            Assert.True(catalog.SkinCount > 200, $"expected a lot more than 200 skins, got {catalog.SkinCount}");

            List<int> heroIds = catalog.Heroes.Select(h => h.HeroId).ToList();
            Assert.Equal(heroIds.Count, heroIds.Distinct().Count());
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }

    [Fact]
    public void AppSettings_ApplyDefaults_NeverLeavesAesKeyBlank()
    {
        var settings = new AppSettings { AesKey = "" };
        settings.ApplyDefaults();
        Assert.False(string.IsNullOrWhiteSpace(settings.AesKey));
    }

    [Fact]
    public void SettingsService_ARoundTrip_PreservesEveryField()
    {
        string path = Path.GetTempFileName();
        try
        {
            var settings = new AppSettings
            {
                PaksDirectory = @"D:\Games\Paks",
                AesKey = "ABCDEF",
                WorkspaceDirectory = @"D:\workspace",
                UsmapOverridePath = @"D:\pin.usmap",
                CheckForUpdatesOnLaunch = false,
                ShowLogPane = false
            };

            SettingsService.Save(settings, path);
            AppSettings loaded = SettingsService.Load(path);

            Assert.Equal(settings.PaksDirectory, loaded.PaksDirectory);
            Assert.Equal(settings.AesKey, loaded.AesKey);
            Assert.Equal(settings.WorkspaceDirectory, loaded.WorkspaceDirectory);
            Assert.Equal(settings.UsmapOverridePath, loaded.UsmapOverridePath);
            Assert.False(loaded.CheckForUpdatesOnLaunch);
            Assert.False(loaded.ShowLogPane);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SettingsService_Load_FallsBackToDefaults_OnCorruptJson()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{ not json");
            AppSettings loaded = SettingsService.Load(path);
            Assert.Equal(GameDefaults.AesKey, loaded.AesKey);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

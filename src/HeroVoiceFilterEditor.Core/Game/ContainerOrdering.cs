using System.Globalization;
using System.Text.RegularExpressions;

namespace HeroVoiceFilterEditor.Core.Game;

/// Load order decides which container wins: FZenPackageContext lets later loads override earlier ones.
public static partial class ContainerOrdering
{
    /// Matches the build in names like Patch_-Windows_1.1.3805839_P.utoc, where a dot precedes it.
    [GeneratedRegex(@"(\d+)_P\.utoc$", RegexOptions.IgnoreCase)]
    private static partial Regex PatchBuildPattern();

    public static bool IsPatchContainer(string path) =>
        Path.GetFileName(path).EndsWith("_P.utoc", StringComparison.OrdinalIgnoreCase);

    public static bool IsGlobalContainer(string path) =>
        string.Equals(Path.GetFileName(path), GameDefaults.GlobalContainerName, StringComparison.OrdinalIgnoreCase);

    public static long? PatchBuild(string path)
    {
        Match match = PatchBuildPattern().Match(Path.GetFileName(path));
        return match.Success && long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out long build)
            ? build
            : null;
    }

    /// Base containers first, then patch containers by ascending build, so the newest patch overrides.
    public static IReadOnlyList<string> Order(IEnumerable<string> containerPaths)
    {
        var all = containerPaths.Where(p => !IsGlobalContainer(p)).ToList();

        var baseContainers = all
            .Where(p => !IsPatchContainer(p))
            .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase);

        var patchContainers = all
            .Where(IsPatchContainer)
            .OrderBy(p => PatchBuild(p) ?? long.MinValue)
            .ThenBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase);

        return baseContainers.Concat(patchContainers).ToList();
    }

    /// Highest patch build present, used to label the extracted snapshot.
    public static string DescribeBuild(IEnumerable<string> containerPaths)
    {
        long? newest = containerPaths.Where(IsPatchContainer).Select(PatchBuild).Where(b => b.HasValue).Max();
        return newest?.ToString(CultureInfo.InvariantCulture) ?? "base";
    }
}

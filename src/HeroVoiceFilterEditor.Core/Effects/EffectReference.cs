using System.Text.RegularExpressions;

namespace HeroVoiceFilterEditor.Core.Effects;

/// One AkEffectShareSet object. In this asset class a package holds exactly one such object,
/// so the package path's leaf segment is also the object name.
public sealed partial record EffectReference(string PackagePath)
{
    [GeneratedRegex(@"^(?<family>.*?)_(?<ordinal>\d+)$")]
    private static partial Regex FamilyPattern();

    public string ObjectName => PackagePath[(PackagePath.LastIndexOf('/') + 1)..];

    public string Family => Split().Family;

    public int Ordinal => Split().Ordinal;

    public string DisplayName => ObjectName;

    private (string Family, int Ordinal) Split()
    {
        Match match = FamilyPattern().Match(ObjectName);
        return match.Success && int.TryParse(match.Groups["ordinal"].Value, out int ordinal)
            ? (match.Groups["family"].Value, ordinal)
            : (ObjectName, 0);
    }
}

public sealed record EffectFamily(string Name, IReadOnlyList<EffectReference> Members);

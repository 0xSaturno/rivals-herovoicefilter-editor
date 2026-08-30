using System.Text.Json;
using System.Text.Json.Serialization;

namespace HeroVoiceFilterEditor.Core.Game;

public sealed record VanillaSnapshot
{
    public const string FileName = "snapshot.json";

    public required string Build { get; init; }

    public required string SourceContainer { get; init; }

    public required string PackagePath { get; init; }

    /// Path of the .uasset relative to the snapshot root, mirroring the game's own mount layout.
    public required string RelativeAssetPath { get; init; }

    public required DateTimeOffset ExtractedUtc { get; init; }

    [JsonIgnore]
    public string SnapshotRoot { get; init; } = string.Empty;

    [JsonIgnore]
    public string AssetPath => Path.Combine(SnapshotRoot, RelativeAssetPath.Replace('/', Path.DirectorySeparatorChar));

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public void Save()
    {
        Directory.CreateDirectory(SnapshotRoot);
        File.WriteAllText(Path.Combine(SnapshotRoot, FileName), JsonSerializer.Serialize(this, SerializerOptions));
    }

    public static VanillaSnapshot? Load(string snapshotRoot)
    {
        string manifest = Path.Combine(snapshotRoot, FileName);
        if (!File.Exists(manifest))
            return null;

        VanillaSnapshot? snapshot = JsonSerializer.Deserialize<VanillaSnapshot>(File.ReadAllText(manifest));
        return snapshot is null ? null : snapshot with { SnapshotRoot = snapshotRoot };
    }
}

using System.Globalization;

namespace HeroVoiceFilterEditor.Core.Metadata;

public sealed record HeroSkin(int SkinId, string SkinName, int HeroId, string HeroName)
{
    public bool HasName => !string.IsNullOrWhiteSpace(SkinName);

    public string Display => HasName ? $"{HeroName} — {SkinName}" : $"{HeroName} — skin {SkinId}";
}

public sealed record Hero(int HeroId, string HeroName, IReadOnlyList<HeroSkin> Skins);

/// Hero and skin names parsed from the community-maintained IDs markdown. Purely cosmetic:
/// an unlisted skin id is still fully editable, just shown without a name.
public sealed class HeroSkinCatalog
{
    public const string CacheFileName = "hero-skin-ids.md";

    private readonly Dictionary<int, HeroSkin> _skinsById;
    private readonly Dictionary<int, Hero> _heroesById;

    private HeroSkinCatalog(IReadOnlyList<Hero> heroes)
    {
        Heroes = heroes;
        _heroesById = heroes.ToDictionary(h => h.HeroId);

        _skinsById = new Dictionary<int, HeroSkin>();
        foreach (HeroSkin skin in heroes.SelectMany(h => h.Skins))
            _skinsById.TryAdd(skin.SkinId, skin);
    }

    public IReadOnlyList<Hero> Heroes { get; }

    public int SkinCount => _skinsById.Count;

    public HeroSkin? FindSkin(int skinId) => _skinsById.GetValueOrDefault(skinId);

    public Hero? FindHero(int heroId) => _heroesById.GetValueOrDefault(heroId);

    /// Skin ids embed their hero id as the leading four digits, which is the fallback
    /// when a skin is too new to appear in the markdown.
    public static int HeroIdOf(int skinId) => skinId >= 1_000_000 ? skinId / 1000 : 0;

    /// The base costume's id — heroId + "001". Never listed in the community markdown
    /// (it is the look every hero starts with, not an alternate skin), so it is synthesized.
    public static int DefaultSkinId(int heroId) => heroId * 1000 + 1;

    public string Describe(int skinId)
    {
        if (FindSkin(skinId) is { } skin)
            return skin.Display;

        Hero? hero = FindHero(HeroIdOf(skinId));
        return hero is null ? $"skin {skinId}" : $"{hero.HeroName} — skin {skinId}";
    }

    /// The markdown reassigns some ids across sections, listing the superseded owner as
    /// "Name (Old)" with no skins. Rows are merged per id, first non-empty name winning.
    public static HeroSkinCatalog Parse(string markdown)
    {
        var names = new Dictionary<int, string>();
        var skins = new Dictionary<int, List<(int SkinId, string SkinName)>>();
        var order = new List<int>();
        int currentHeroId = 0;

        foreach (string rawLine in markdown.Split('\n'))
        {
            string line = rawLine.Trim();
            if (!line.StartsWith('|'))
                continue;

            string[] cells = SplitRow(line);
            if (cells.Length == 0 || IsSeparator(cells))
                continue;

            string heroCell = Cell(cells, 0);

            if (heroCell.Length > 0)
            {
                // A non-numeric id is a placeholder row such as "???? | Upcoming Characters".
                currentHeroId = ParseId(heroCell);
                if (currentHeroId == 0)
                    continue;

                string heroName = TitleCaseWords(Cell(cells, 1));
                if (!names.TryGetValue(currentHeroId, out string? existing))
                {
                    names[currentHeroId] = heroName;
                    skins[currentHeroId] = new List<(int, string)>();
                    order.Add(currentHeroId);
                }
                else if (existing.Length == 0 && heroName.Length > 0)
                {
                    names[currentHeroId] = heroName;
                }
            }

            if (currentHeroId == 0)
                continue;

            int skinId = ParseId(Cell(cells, 2));
            if (skinId != 0)
                skins[currentHeroId].Add((skinId, TitleCaseWords(Cell(cells, 3))));
        }

        var heroes = new List<Hero>(order.Count);
        foreach (int heroId in order)
        {
            string heroName = names[heroId].Length > 0 ? names[heroId] : $"Hero {heroId}";

            // "(Old)" rows are superseded owners of a reassigned id, not real heroes; ids past
            // 1999 are test/placeholder rows in the markdown. Neither belongs in the picker.
            if (heroId > 1999 || heroName.Contains("(Old)", StringComparison.OrdinalIgnoreCase))
                continue;

            List<HeroSkin> heroSkins = skins[heroId].Select(s => new HeroSkin(s.SkinId, s.SkinName, heroId, heroName)).ToList();

            int defaultId = DefaultSkinId(heroId);
            if (heroSkins.All(s => s.SkinId != defaultId))
                heroSkins.Insert(0, new HeroSkin(defaultId, "Default", heroId, heroName));

            heroes.Add(new Hero(heroId, heroName, heroSkins));
        }

        return new HeroSkinCatalog(heroes);
    }

    /// The markdown shouts some names in ALL CAPS while others are already properly cased;
    /// only words that are entirely uppercase get re-cased, so names like "G-Bomb" are untouched.
    private static string TitleCaseWords(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        string[] words = text.Split(' ');
        for (int i = 0; i < words.Length; i++)
            words[i] = TitleCaseIfShouting(words[i]);
        return string.Join(' ', words);
    }

    private static string TitleCaseIfShouting(string word)
    {
        if (word.Length < 2 || word != word.ToUpperInvariant() || !word.Any(char.IsLetter))
            return word;

        char[] chars = word.ToLowerInvariant().ToCharArray();
        bool startOfWord = true;
        for (int i = 0; i < chars.Length; i++)
        {
            if (char.IsLetter(chars[i]))
            {
                chars[i] = startOfWord ? char.ToUpperInvariant(chars[i]) : chars[i];
                startOfWord = false;
            }
            else
            {
                startOfWord = true;
            }
        }
        return new string(chars);
    }

    private static string[] SplitRow(string line)
    {
        string trimmed = line.Trim('|');
        return trimmed.Length == 0 ? [] : trimmed.Split('|').Select(c => c.Trim()).ToArray();
    }

    private static bool IsSeparator(string[] cells) =>
        cells.All(c => c.Length > 0 && c.All(ch => ch is ':' or '-' or ' ')) ||
        string.Equals(cells[0], "ID", StringComparison.OrdinalIgnoreCase);

    private static string Cell(string[] cells, int index) => index < cells.Length ? cells[index] : string.Empty;

    private static int ParseId(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int id) ? id : 0;

    public static async Task<(HeroSkinCatalog Catalog, CacheStatus Status, string Detail)> EnsureCurrentAsync(
        string? cacheDirectory = null,
        bool checkRemote = true,
        CancellationToken cancellationToken = default)
    {
        string directory = cacheDirectory ?? AppPaths.MetadataCacheDirectory;
        string cachePath = Path.Combine(directory, CacheFileName);
        bool haveCache = File.Exists(cachePath);

        if (!checkRemote)
        {
            return haveCache
                ? (Parse(await File.ReadAllTextAsync(cachePath, cancellationToken)), CacheStatus.UpToDate, "using cache without checking remote")
                : (Parse(string.Empty), CacheStatus.Unavailable, "no cached hero ids and remote check disabled");
        }

        try
        {
            string markdown = await RemoteSources.Client.GetStringAsync(RemoteSources.HeroSkinIds, cancellationToken);
            HeroSkinCatalog parsed = Parse(markdown);

            if (parsed.SkinCount == 0)
                throw new InvalidDataException("hero id markdown parsed to zero skins");

            bool changed = !haveCache || await File.ReadAllTextAsync(cachePath, cancellationToken) != markdown;
            AppPaths.Ensure(directory);
            await File.WriteAllTextAsync(cachePath, markdown, cancellationToken);

            return (parsed,
                changed ? CacheStatus.Downloaded : CacheStatus.UpToDate,
                $"{parsed.Heroes.Count} heroes, {parsed.SkinCount} skins");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            if (!haveCache)
                return (Parse(string.Empty), CacheStatus.Unavailable, $"offline and nothing cached: {ex.Message}");

            HeroSkinCatalog cached = Parse(await File.ReadAllTextAsync(cachePath, cancellationToken));
            return (cached, CacheStatus.Offline, $"offline, using cache: {cached.SkinCount} skins");
        }
    }
}

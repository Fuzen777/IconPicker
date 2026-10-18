using System.Text.RegularExpressions;

namespace IconPicker.Core.Candidates;

/// <summary>
/// Orders icon candidates by relevance, extending Select-IconExe from Set Folder Icons.ps1:
/// <list type="number">
/// <item>Icons inside executables and .ico files before pictures and SVGs, unless their largest size is under 48 px: those are too blurry as a folder icon and rank with the pictures.</item>
/// <item>An executable's main icon before its secondary icons, which come after every other main icon of their group and never above their own main icon.</item>
/// <item>Files named like the folder (exactly, then partly, or an executable whose product name is the folder's) or ending with a generic icon word before other names. A size marker at the end of an .ico or picture name doesn't count against it, so tModLoader256.bmp is as well named as tModLoader.bmp.</item>
/// <item>Files closer to the folder before deeper ones, except among files named exactly like the folder, where the best copy wins wherever it sits.</item>
/// <item>Square icons and pictures (sides within 5%) of 256 px or more first, then other square ones, then the rest (a square SVG counts as 256 px or more, since it is sharp at any size); executables before a .dll before .ico files before pictures and SVGs; then sharper before blurrier; then closer, and larger executables before smaller ones.</item>
/// </list>
/// </summary>
public static partial class CandidateRanker
{
    private const int LargeIconSize = 256;

    /// <summary>Below this, an icon is too blurry as a folder icon to rank above a good picture.</summary>
    private const int LowResolutionSize = 48;

    private static readonly HashSet<string> GenericIconNames = new(StringComparer.Ordinal) { "icon", "logo", "appicon", "favicon" };

    public static List<IconCandidate> Rank(IEnumerable<IconCandidate> candidates, IReadOnlyList<string> folderNames)
    {
        var candidateList = candidates.ToList();
        // An executable's main icon is its first readable one (index 0, unless that was a placeholder or damaged). A shortcut's icon is always a main icon: it was picked to stand for the app.
        static string GetModuleKey(IconCandidate candidate) => candidate.ShortcutPath ?? candidate.Source.FilePath;
        var mainIcons = candidateList
            .Where(candidate => candidate.Source.Kind == IconSourceKind.Module)
            .GroupBy(GetModuleKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.MinBy(candidate => candidate.Source.IconIndex)!, StringComparer.OrdinalIgnoreCase);
        bool IsSecondaryIcon(IconCandidate candidate)
        {
            return candidate.Source.Kind == IconSourceKind.Module && candidate.Source.IconIndex > mainIcons[GetModuleKey(candidate)].Source.IconIndex;
        }
        // A secondary icon never ranks above its own executable's main icon: a sharp document or engine icon can't jump ahead of a blurry main icon.
        int GetRankingTier(IconCandidate candidate)
        {
            return IsSecondaryIcon(candidate) ? Math.Max(GetTier(candidate), GetTier(mainIcons[GetModuleKey(candidate)])) : GetTier(candidate);
        }
        // A wide picture's number is rarely an icon size (Celeste_1024.jpg is a 1024 × 576 wallpaper), so its marker only counts when it is square.
        int GetCandidateNameRank(IconCandidate candidate)
        {
            var sourceNameRank = GetNameRank(candidate.Source.FilePath, folderNames, trustSizeMarker: !candidate.Source.IsConverted || candidate.IsNearlySquare, candidate.ProductName);
            // A shortcut is named for what it opens (Hades.lnk), while its icon file often isn't (Steam's games\5f3a….ico).
            return candidate.ShortcutPath is null ? sourceNameRank : Math.Min(sourceNameRank, GetNameRank(candidate.ShortcutPath, folderNames, trustSizeMarker: false));
        }

        return candidateList
            .Select(candidate => (Candidate: candidate, NameRank: GetCandidateNameRank(candidate)))
            .OrderBy(item => GetRankingTier(item.Candidate))
            .ThenBy(item => IsSecondaryIcon(item.Candidate))
            .ThenBy(item => item.NameRank)
            .ThenBy(item => GetNearness(item.NameRank, item.Candidate.Depth))
            .ThenBy(item => GetQualityRank(item.Candidate))
            .ThenBy(item => GetKindOrder(item.Candidate.Source))
            .ThenByDescending(item => item.Candidate.SmallestSide)
            // Equally sharp copies: the closer one (Grim Dawn.exe over compat\Grim Dawn.exe), then the larger executable.
            .ThenBy(item => item.Candidate.Depth)
            .ThenByDescending(item => item.Candidate.Source.Kind == IconSourceKind.Module ? item.Candidate.FileSize : 0)
            .ThenBy(item => item.Candidate.Source.IconIndex)
            .ThenBy(item => item.Candidate.RelativePath, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => item.Candidate)
            .ToList();
    }

    /// <summary>
    /// The order before files are opened, from what the folder listing tells: name, type, depth and file size. Files named exactly like the folder come first whatever their type, so the inspection cut never drops them: whether an icon is too blurry to beat a picture is only known once it is opened. SVGs come after pictures of the same name rank, so an app's thousands of interface SVGs (GIMP, VS Code) don't take the pictures' places.
    /// </summary>
    internal static IEnumerable<T> OrderByListingDetails<T>(IEnumerable<T> files, Func<T, string> getFilePath, Func<T, IconSourceKind> getKind, Func<T, int> getDepth, Func<T, long> getFileSize, IReadOnlyList<string> folderNames)
    {
        return files
            .Select(file => (File: file, NameRank: GetNameRank(getFilePath(file), folderNames)))
            .OrderBy(item => item.NameRank == 0 ? 0 : 1)
            .ThenBy(item => getKind(item.File) is IconSourceKind.Image or IconSourceKind.Vector ? 1 : 0)
            .ThenBy(item => item.NameRank)
            .ThenBy(item => getKind(item.File) == IconSourceKind.Vector ? 1 : 0)
            .ThenBy(item => GetNearness(item.NameRank, getDepth(item.File)))
            .ThenBy(item => GetKindOrder(new IconSource(getFilePath(item.File), getKind(item.File))))
            .ThenByDescending(item => getFileSize(item.File))
            .Select(item => item.File);
    }

    /// <summary>
    /// 0 when the file is named exactly like one of the folders, 1 when one name contains the other, the executable's product name equals a folder name, or the name ends with a generic icon word, 2 otherwise. Names shorter than 3 characters don't count.
    /// </summary>
    /// <param name="filePath">The candidate file.</param>
    /// <param name="folderNames">The names a file named like the folder may carry.</param>
    /// <param name="trustSizeMarker">
    /// Whether an .ico or picture name may also be compared without its trailing size marker (see <see cref="GetNameWithoutSizeMarker"/>). Executable names are always compared as they are: their digits are bitness or part of the title (AutoHotkey64, DOOM64), and their icon sizes are inside them.
    /// </param>
    /// <param name="productName">
    /// An executable's product name. It only counts when it equals a folder name: helpers often carry part of the app's name (Sandboxie's Start.exe is "Sandboxie" in the Sandboxie-Plus folder), while the main app carries all of it (SandMan.exe is "Sandboxie-Plus").
    /// </param>
    internal static int GetNameRank(string filePath, IReadOnlyList<string> folderNames, bool trustSizeMarker = true, string? productName = null)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var canHaveSizeMarker = trustSizeMarker && IconSource.GetKind(filePath) is IconSourceKind.IconFile or IconSourceKind.Image or IconSourceKind.Vector;
        var nameWithoutSizeMarker = canHaveSizeMarker ? GetNameWithoutSizeMarker(fileName) : fileName;
        var fileKey = NormalizeName(fileName);
        var fileKeyWithoutSize = NormalizeName(nameWithoutSizeMarker);
        // A name that is only a size (256x256.png) leaves nothing to compare.
        string[] fileKeys = fileKeyWithoutSize != fileKey && fileKeyWithoutSize.Length >= 3 ? [fileKey, fileKeyWithoutSize] : [fileKey];
        var productKey = productName is null ? "" : NormalizeName(productName);
        var bestRank = 2;
        foreach (var folderName in folderNames)
        {
            var folderKey = NormalizeName(folderName);
            if (folderKey.Length < 3)
            {
                continue;
            }
            if (productKey == folderKey)
            {
                bestRank = 1;
            }
            foreach (var key in fileKeys.Where(key => key.Length >= 3))
            {
                if (key == folderKey)
                {
                    return 0;
                }
                if (folderKey.Contains(key, StringComparison.Ordinal) || key.Contains(folderKey, StringComparison.Ordinal))
                {
                    bestRank = 1;
                }
            }
        }
        if (bestRank == 2 && HasGenericIconName(nameWithoutSizeMarker))
        {
            bestRank = 1;
        }
        return bestRank;
    }

    /// <summary>
    /// The name without its trailing Windows resource qualifiers (StoreLogo.scale-200, Square44x44Logo.altform-unplated_targetsize-256) and one trailing size marker, or the name itself when it has neither. A marker is a whole common icon size (Game256, icon_48, vlc16x16, Game 150px, icon_512x512@2x), a scale (@2x, @1.5x) or a size word (Game_large, GameHD).
    /// Numbers that are only partly a size are kept: zero-padded indexes (Dish.016, LY016), years (Celeste2016), versions (Game-1.16) and the second half of a resolution (1280x1024).
    /// </summary>
    internal static string GetNameWithoutSizeMarker(string fileName)
    {
        var nameWithoutQualifiers = ResourceQualifierPattern().Replace(fileName, "", 1);
        return SizeMarkerPattern().Replace(nameWithoutQualifiers, "", 1);
    }

    /// <summary>
    /// Whether the name's last word is a generic icon word: icon.png, app_icon.png, GameLogo.png, AppIcon.ico. The word inside another one (lexicon, iconbg) or followed by more (IconSet, BuffIcon_01, logo_white) doesn't count.
    /// </summary>
    internal static bool HasGenericIconName(string fileName)
    {
        var words = NameWordPattern().Matches(fileName);
        return words.Count > 0 && GenericIconNames.Contains(words[^1].Value.ToLowerInvariant());
    }

    /// <summary>Lower case, letters and digits only, so "My Game" matches "my_game.exe".</summary>
    internal static string NormalizeName(string name)
    {
        return NonAlphanumericPattern().Replace(name.ToLowerInvariant(), "");
    }

    /// <summary>Executables and .ico files first, unless their icon is too small to look sharp as a folder icon. Pictures and SVGs come after, however sharp.</summary>
    private static int GetTier(IconCandidate candidate)
    {
        return !candidate.Source.IsConverted && candidate.LargestSide >= LowResolutionSize ? 0 : 1;
    }

    /// <summary>Copies named exactly like the folder are judged on quality wherever they sit (a 256 px icons\default\Game.ico over a 48 px shellicons\game.ico); depth only breaks the tie.</summary>
    private static int GetNearness(int nameRank, int depth)
    {
        return nameRank == 0 ? 0 : depth;
    }

    /// <summary>Square and 256 px or more, then square and sharp enough (48 px or more), then square but blurry, then not square.</summary>
    private static int GetQualityRank(IconCandidate candidate)
    {
        if (!candidate.IsNearlySquare)
        {
            return 3;
        }
        if (candidate.LargestSide >= LargeIconSize)
        {
            return 0;
        }
        return candidate.LargestSide >= LowResolutionSize ? 1 : 2;
    }

    /// <summary>
    /// Executables, then a .dll named like the folder, then .ico files, then pictures and SVGs alike. The .dll is only there for apps without an executable of their own: when both exist the executable is the natural pick, and the .dll often sits in a version folder (Vivaldi, Battle.net).
    /// </summary>
    private static int GetKindOrder(IconSource source)
    {
        return source.Kind switch
        {
            IconSourceKind.Module when string.Equals(Path.GetExtension(source.FilePath), ".dll", StringComparison.OrdinalIgnoreCase) => 1,
            IconSourceKind.Module => 0,
            IconSourceKind.IconFile => 2,
            _ => 3,
        };
    }

    [GeneratedRegex(@"[^\p{L}\p{Nd}]")]
    private static partial Regex NonAlphanumericPattern();

    // Words of a file name: capitalized or lowercase words (camelCase splits), runs of capitals (HTMLIcon → HTML, Icon), runs of digits, and runs of letters without case (logo_英文 → logo, 英文).
    [GeneratedRegex(@"\p{Lu}?\p{Ll}+|\p{Lu}+(?!\p{Ll})|\p{Nd}+|[\p{Lo}\p{Lt}\p{Lm}]+")]
    private static partial Regex NameWordPattern();

    // Windows Store resource qualifiers at the end of a name: .scale-200, .targetsize-48, .altform-unplated_targetsize-256, .scale-100_contrast-white.
    [GeneratedRegex(@"\.(?:(?:scale|targetsize|contrast|altform|theme|lang)-[A-Za-z0-9-]+_?)+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ResourceQualifierPattern();

    // 1. A listed size after a separator, after a dot with no digit before it, or glued to a letter that isn't the x of a resolution; optionally NxN with both sides equal, optionally "px", optionally a scale (icon_512x512@2x).
    // 2. A scale: @1x, @1.5x, @2x…  3. A size word after a separator.  4. A size word after a lowercase-to-uppercase boundary.
    [GeneratedRegex(@"(?:(?:[ _\-]|(?<!\p{Nd})\.|(?<=\p{L})(?<!\p{Nd}x))(?<size>16|20|22|24|30|32|36|40|44|48|50|60|64|70|72|80|96|100|120|128|144|150|152|160|180|192|200|256|310|384|512|1024)(?:x\k<size>)?(?:px)?(?:@[1-9](?:\.\d{1,2})?x)?|@[1-9](?:\.\d{1,2})?x|[ _\-](?:small|large|big|hd|hires)|(?-i:(?<=\p{Ll})(?:Small|Large|Big|HD|Hires)))$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SizeMarkerPattern();
}

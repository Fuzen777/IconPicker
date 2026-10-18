using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace IconPicker.Core.Launch;

/// <summary>The restart that moves the app's data after the portable setting changed.</summary>
/// <param name="ToPortable">Into the Data folder next to the app, or back to %LocalAppData%.</param>
/// <param name="PreviousProcessId">The copy that asked for the restart, which has to have exited before its data moves.</param>
public sealed record DataMoveRequest(bool ToPortable, int PreviousProcessId);

/// <summary>What the app was started for, read from its command line.</summary>
/// <param name="FolderPath">The folder to open, already made absolute, or null to show Home.</param>
/// <param name="ElevatedJobPath">The job file to apply when started as the elevated helper, or null for a normal start.</param>
/// <param name="DataMove">When restarted to move its data, where to.</param>
public sealed partial record LaunchArguments(string? FolderPath, string? ElevatedJobPath, DataMoveRequest? DataMove = null)
{
    public const string ElevatedJobSwitch = "--apply-elevated";

    public const string DataMoveSwitch = "--move-data";

    public const string PortableTarget = "portable";

    public const string AppDataTarget = "appdata";

    public static readonly LaunchArguments None = new(null, null);

    /// <summary>
    /// Reads a raw command line. The program path is skipped when it comes first, since Windows includes it in some activations and not in others.
    /// </summary>
    public static LaunchArguments Parse(string? commandLine)
    {
        var tokens = SplitCommandLine(commandLine ?? "");
        if (tokens.Count > 0 && tokens[0].EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            tokens.RemoveAt(0);
        }
        if (tokens.Count == 0)
        {
            return None;
        }
        if (string.Equals(tokens[0], ElevatedJobSwitch, StringComparison.OrdinalIgnoreCase))
        {
            return tokens.Count > 1 ? new LaunchArguments(null, tokens[1]) : None;
        }
        if (string.Equals(tokens[0], DataMoveSwitch, StringComparison.OrdinalIgnoreCase))
        {
            return new LaunchArguments(null, null, ParseDataMove(tokens));
        }
        return new LaunchArguments(NormalizeFolderPath(tokens[0]), null);
    }

    /// <summary>The command line that restarts the app to move its data.</summary>
    public static string FormatDataMove(DataMoveRequest request)
    {
        return $"{DataMoveSwitch} {(request.ToPortable ? PortableTarget : AppDataTarget)} {request.PreviousProcessId.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>Makes a folder path absolute, or returns null when it is not a usable path.</summary>
    public static string? NormalizeFolderPath(string? folderPath)
    {
        var trimmedPath = folderPath?.Trim().Trim('"').Trim();
        if (string.IsNullOrEmpty(trimmedPath))
        {
            return null;
        }
        // "C:" alone means C:'s current folder to Windows, not its root.
        if (DriveLetterOnlyPattern().IsMatch(trimmedPath))
        {
            trimmedPath += Path.DirectorySeparatorChar;
        }
        try
        {
            return Path.GetFullPath(trimmedPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>
    /// Splits on spaces outside quotes. Unlike the standard Windows rules, a backslash never escapes a quote: Explorer passes a drive as "D:\", which the standard rules would read as D:" followed by nothing. Quotes can't appear in Windows paths, so nothing is lost.
    /// </summary>
    internal static List<string> SplitCommandLine(string commandLine)
    {
        var tokens = new List<string>();
        var currentToken = new StringBuilder();
        var isInsideQuotes = false;
        var hasToken = false;
        foreach (var character in commandLine)
        {
            if (character == '"')
            {
                isInsideQuotes = !isInsideQuotes;
                hasToken = true;
            }
            else if (char.IsWhiteSpace(character) && !isInsideQuotes)
            {
                if (hasToken)
                {
                    tokens.Add(currentToken.ToString());
                    currentToken.Clear();
                    hasToken = false;
                }
            }
            else
            {
                currentToken.Append(character);
                hasToken = true;
            }
        }
        if (hasToken)
        {
            tokens.Add(currentToken.ToString());
        }
        return tokens;
    }

    private static DataMoveRequest? ParseDataMove(List<string> tokens)
    {
        if (tokens.Count < 3 || !int.TryParse(tokens[2], NumberStyles.None, CultureInfo.InvariantCulture, out var processId))
        {
            return null;
        }
        if (string.Equals(tokens[1], PortableTarget, StringComparison.OrdinalIgnoreCase))
        {
            return new DataMoveRequest(true, processId);
        }
        return string.Equals(tokens[1], AppDataTarget, StringComparison.OrdinalIgnoreCase) ? new DataMoveRequest(false, processId) : null;
    }

    [GeneratedRegex(@"^[A-Za-z]:$")]
    private static partial Regex DriveLetterOnlyPattern();
}

using System;

namespace AutoTint.Services;

/// <summary>
/// The string form of a "start with Windows" entry. Pure, because quoting and comparing
/// executable paths is exactly the sort of fiddly detail that is worth testing and not
/// worth debugging through the registry.
/// </summary>
internal static class StartupCommand
{
    /// <summary>
    /// The value Windows should run at logon. Always quoted: a path through
    /// "C:\Program Files\..." or any user folder with a space in it would otherwise be
    /// parsed as a command plus arguments.
    /// </summary>
    internal static string For(string executablePath) =>
        "\"" + executablePath.Trim('"') + "\"";

    /// <summary>
    /// Whether an existing registry value already refers to this executable. Compared
    /// case-insensitively and with quotes stripped, since Windows paths are not
    /// case-sensitive and the value may have been written by hand.
    /// </summary>
    internal static bool PointsAt(string? registryValue, string executablePath)
    {
        if (string.IsNullOrWhiteSpace(registryValue)) return false;

        string stored = registryValue.Trim().Trim('"').Trim();
        string wanted = executablePath.Trim().Trim('"').Trim();

        return string.Equals(stored, wanted, StringComparison.OrdinalIgnoreCase);
    }
}

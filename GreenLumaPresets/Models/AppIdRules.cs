namespace GreenLumaPresets.Models;

public static class AppIdRules
{
    /// <summary>
    /// Value given to App IDs created empty, waiting to be edited by the user.
    /// </summary>
    public const string Placeholder = "0";

    /// <summary>
    /// Normalizes a Steam App ID: trimmed, strictly positive integer without leading zeros.
    /// </summary>
    /// <returns>false when the value is not a valid App ID.</returns>
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!uint.TryParse(value.Trim(), out var appId) || appId == 0) return false;

        normalized = appId.ToString();
        return true;
    }
}

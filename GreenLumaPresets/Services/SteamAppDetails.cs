namespace GreenLumaPresets.Services;

/// <summary>
/// What the Steam store knows about an AppID.
/// </summary>
/// <param name="Name">Store name; null when Steam doesn't expose it.</param>
/// <param name="IconUrl">Small library icon; null when neither the app nor its parent game has one.</param>
public record SteamAppDetails(string? Name, string? IconUrl);

using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;

namespace GreenLumaPresets.Services;

public class SteamService : ISteamService
{
    private const string ConnectionErrorMessage = "Couldn't connect to Steam API";

    // A single HttpClient for the app lifetime avoids socket exhaustion.
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly HttpClient client;

    public SteamService() : this(SharedClient)
    {
    }

    public SteamService(HttpClient client)
    {
        this.client = client;
    }

    public async Task<Result<SteamApp>> GetAppData(int appId)
    {
        string url = $"https://store.steampowered.com/api/appdetails/?appids={appId}";

        try
        {
            using HttpResponseMessage response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode || json == null)
                return Result.Fail("Failed to get the app data");

            var parsedJson = JsonSerializer.Deserialize<Dictionary<string, SteamResponse>>(json, new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip });
            if (parsedJson == null)
                return Result.Fail("Failed to parse the JSON response");

            var appResponse = parsedJson.FirstOrDefault().Value;
            if (appResponse.success == false)
                return Result.Fail("This AppID doesn't exist on Steam");

            if (appResponse.data == null)
                return Result.Fail("Couldn't get data from Steam's response");

            return SteamApp.From(appResponse.data);
        }
        catch (Exception err) when (err is HttpRequestException or TaskCanceledException)
        {
            return Result.Fail(ConnectionErrorMessage);
        }
        catch (JsonException)
        {
            return Result.Fail("Couldn't read Steam's response");
        }
    }

    public async Task<Result<List<SteamSearchResult>>> SearchAppsByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Fail("Search term is required");
        }

        string url = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(name)}&l=english&cc=US";

        try
        {
            using HttpResponseMessage response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode || json == null)
                return Result.Fail("Failed to search Steam");

            var parsedJson = JsonSerializer.Deserialize<SteamSearchResponse>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
                });

            if (parsedJson?.Items == null)
                return Result.Fail("Failed to parse Steam search response");

            var results = parsedJson.Items
                .Where(item => item.Id != null && !string.IsNullOrWhiteSpace(item.Name))
                .Select(item => new SteamSearchResult(item.Id!.Value, item.Name!))
                .ToList();

            return Result.Ok(results);
        }
        catch (Exception err) when (err is HttpRequestException or TaskCanceledException)
        {
            return Result.Fail(ConnectionErrorMessage);
        }
        catch (JsonException)
        {
            return Result.Fail("Couldn't read Steam's response");
        }
    }
}

public record SteamResponse(
    bool? success,
    SteamDataResponse? data
);

// Steam omits the "dlc" key entirely for apps without DLC, so it must be nullable.
public record SteamDataResponse(string name, int? steam_appid, IReadOnlyList<int?>? dlc);

public record SteamSearchResult(int AppId, string Name);

public class SteamSearchResponse
{
    [JsonPropertyName("items")]
    public List<SteamSearchItem>? Items { get; set; }
}

public class SteamSearchItem
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public record SteamApp(string Name, List<string> AppIds)
{
    public static SteamApp From(SteamDataResponse steamDataResponse)
    {
        var dlcIds = (steamDataResponse.dlc ?? [])
            .Where(x => x != null)
            .Select(x => x!.Value.ToString());

        List<string> appIds = steamDataResponse.steam_appid is int mainAppId
            ? dlcIds.Prepend(mainAppId.ToString()).ToList()
            : dlcIds.ToList();

        return new(steamDataResponse.name, appIds);
    }
}

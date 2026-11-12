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

    // Keeps the GetItems URL well under common length limits.
    private const int StoreItemsBatchSize = 100;

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

    public async Task<Result<Dictionary<uint, SteamAppDetails>>> GetAppDetails(IEnumerable<uint> appIds)
    {
        var requested = appIds.Distinct().ToList();
        var items = new Dictionary<uint, StoreItem>();

        try
        {
            var result = await FetchStoreItems(requested, items);
            if (result.IsFailed) return result;

            // DLC rarely have an icon of their own: Steam shows their game's.
            var parents = items.Values
                .Where(item => item.Icon == null && item.ParentAppId is uint parent && !items.ContainsKey(parent))
                .Select(item => item.ParentAppId!.Value)
                .Distinct()
                .ToList();
            if (parents.Count > 0)
            {
                // Without the parents, these DLC just stay without an icon.
                await FetchStoreItems(parents, items);
            }
        }
        catch (Exception err) when (err is HttpRequestException or TaskCanceledException)
        {
            return Result.Fail(ConnectionErrorMessage);
        }
        catch (JsonException)
        {
            return Result.Fail("Couldn't read Steam's response");
        }

        var details = new Dictionary<uint, SteamAppDetails>();
        foreach (var appId in requested)
        {
            if (!items.TryGetValue(appId, out var item)) continue;

            var iconUrl = item.Icon != null
                ? IconUrl(appId, item.Icon)
                : item.ParentAppId is uint parent && items.TryGetValue(parent, out var parentItem) && parentItem.Icon != null
                    ? IconUrl(parent, parentItem.Icon)
                    : null;
            details[appId] = new SteamAppDetails(item.Name, iconUrl);
        }

        return Result.Ok(details);
    }

    private static string IconUrl(uint appId, string hash)
        => $"https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/{appId}/{hash}.jpg";

    /// <summary>
    /// Adds the store items of the given apps that Steam knows to <paramref name="items"/>, in batches.
    /// </summary>
    private async Task<Result> FetchStoreItems(IReadOnlyList<uint> appIds, Dictionary<uint, StoreItem> items)
    {
        foreach (var batch in appIds.Chunk(StoreItemsBatchSize))
        {
            var input = JsonSerializer.Serialize(new
            {
                ids = batch.Select(id => new { appid = id }),
                context = new { language = "english", country_code = "US" },
                data_request = new { include_assets = true },
            });
            var url = $"https://api.steampowered.com/IStoreBrowseService/GetItems/v1/?input_json={Uri.EscapeDataString(input)}";

            using HttpResponseMessage response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return Result.Fail("Failed to get the app details");

            var json = await response.Content.ReadAsStringAsync();
            var parsed = JsonSerializer.Deserialize<StoreItemsResponse>(json);

            foreach (var item in parsed?.Response?.StoreItems ?? [])
            {
                // Unknown or hidden apps come back with another success code and no data.
                if (item.Success != 1 || item.Id is not uint id) continue;

                items[id] = new StoreItem(
                    string.IsNullOrWhiteSpace(item.Name) ? null : item.Name,
                    string.IsNullOrWhiteSpace(item.Assets?.CommunityIcon) ? null : item.Assets.CommunityIcon,
                    item.RelatedItems?.ParentAppId);
            }
        }

        return Result.Ok();
    }

    private sealed record StoreItem(string? Name, string? Icon, uint? ParentAppId);

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

internal sealed class StoreItemsResponse
{
    [JsonPropertyName("response")]
    public StoreItemsBody? Response { get; set; }
}

internal sealed class StoreItemsBody
{
    [JsonPropertyName("store_items")]
    public List<StoreItemJson>? StoreItems { get; set; }
}

internal sealed class StoreItemJson
{
    [JsonPropertyName("id")]
    public uint? Id { get; set; }

    [JsonPropertyName("success")]
    public int? Success { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("assets")]
    public StoreItemAssets? Assets { get; set; }

    [JsonPropertyName("related_items")]
    public StoreItemRelations? RelatedItems { get; set; }
}

internal sealed class StoreItemAssets
{
    [JsonPropertyName("community_icon")]
    public string? CommunityIcon { get; set; }
}

internal sealed class StoreItemRelations
{
    [JsonPropertyName("parent_appid")]
    public uint? ParentAppId { get; set; }
}

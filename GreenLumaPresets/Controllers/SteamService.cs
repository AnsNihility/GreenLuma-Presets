using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;

namespace GreenLumaPresets.Controllers;

public class SteamService
{
    public SteamService()
    {
    }

    public async Task<Result<SteamApp>> GetAppData(int appId)
    {
        string url = $"http://store.steampowered.com/api/appdetails/?appids={appId}";

        using HttpClient client = new();
        client.DefaultRequestHeaders.Add("Host", "store.steampowered.com");

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
        catch(Exception err)
        {
            if (err.Source == "System.Net.Http")
                return Result.Fail("Couldn't connect to Steam API");
            return Result.Fail(err.Message);
        }
    }
}

public record SteamResponse(
    bool? success,
    SteamDataResponse? data
);

public record SteamDataResponse(string name, int? steam_appid, IReadOnlyList<int?> dlc);

public record SteamApp(string Name, List<string> AppIds)
{
    public static SteamApp From(SteamDataResponse steamDataResponse)
    {
        List<string> appIds = steamDataResponse.dlc.Where(x => x != null)
                                          .Select(x => x!.Value.ToString())
                                          .Prepend(steamDataResponse.steam_appid!.ToString())
                                          .ToList() ?? [];

        return new(steamDataResponse.name, appIds);
    }
}

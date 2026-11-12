using GreenLumaPresets.Services;
using System.Net;
using System.Text;

namespace GreenLumaPresets.Tests;

public class SteamServiceTests
{
    [Fact]
    public async Task GetAppData_WithDlc_ReturnsMainAppFollowedByDlc()
    {
        var service = CreateService("""
            {"400":{"success":true,"data":{"type":"game","name":"Portal","steam_appid":400,"dlc":[323170,2012840]}}}
            """);

        var result = await service.GetAppData(400);

        Assert.True(result.IsSuccess);
        Assert.Equal("Portal", result.Value.Name);
        Assert.Equal(["400", "323170", "2012840"], result.Value.AppIds);
    }

    [Fact]
    public async Task GetAppData_WithoutDlcKey_ReturnsMainAppOnly()
    {
        // Steam omits the "dlc" key for apps without DLC.
        var service = CreateService("""
            {"10":{"success":true,"data":{"type":"game","name":"Counter-Strike","steam_appid":10}}}
            """);

        var result = await service.GetAppData(10);

        Assert.True(result.IsSuccess);
        Assert.Equal(["10"], result.Value.AppIds);
    }

    [Fact]
    public async Task GetAppData_UnknownApp_Fails()
    {
        var service = CreateService("""{"1":{"success":false}}""");

        var result = await service.GetAppData(1);

        Assert.True(result.IsFailed);
        Assert.Equal("This AppID doesn't exist on Steam", result.Errors.Single().Message);
    }

    [Fact]
    public async Task GetAppData_ConnectionFailure_ReportsConnectionError()
    {
        var service = new SteamService(new HttpClient(new StubHandler(_ => throw new HttpRequestException("offline"))));

        var result = await service.GetAppData(400);

        Assert.True(result.IsFailed);
        Assert.Equal("Couldn't connect to Steam API", result.Errors.Single().Message);
    }

    [Fact]
    public async Task GetAppData_UsesHttps()
    {
        Uri? requested = null;
        var service = new SteamService(new HttpClient(new StubHandler(request =>
        {
            requested = request.RequestUri;
            return JsonResponse("""{"400":{"success":false}}""");
        })));

        await service.GetAppData(400);

        Assert.Equal("https", requested?.Scheme);
    }

    [Fact]
    public async Task SearchAppsByName_SkipsIncompleteItems()
    {
        var service = CreateService("""
            {"total":3,"items":[{"id":620,"name":"Portal 2"},{"id":null,"name":"Broken"},{"id":400,"name":""}]}
            """);

        var result = await service.SearchAppsByName("portal");

        Assert.True(result.IsSuccess);
        Assert.Equal([new SteamSearchResult(620, "Portal 2")], result.Value);
    }

    [Fact]
    public async Task GetAppDetails_ReturnsNamesAndIconsAndSkipsHiddenApps()
    {
        var service = CreateService("""
            {"response":{"store_items":[
              {"id":620,"success":1,"name":"Portal 2","assets":{"community_icon":"abc"}},
              {"id":228980,"success":15,"name":"","appid":0}
            ]}}
            """);

        var result = await service.GetAppDetails([620, 228980]);

        Assert.True(result.IsSuccess);
        var portal = Assert.Single(result.Value);
        Assert.Equal(620u, portal.Key);
        Assert.Equal("Portal 2", portal.Value.Name);
        Assert.Equal("https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/620/abc.jpg", portal.Value.IconUrl);
    }

    [Fact]
    public async Task GetAppDetails_DlcWithoutIcon_UsesItsGamesIcon()
    {
        var requests = new List<string>();
        var service = new SteamService(new HttpClient(new StubHandler(request =>
        {
            var query = Uri.UnescapeDataString(request.RequestUri!.Query);
            requests.Add(query);
            return JsonResponse(query.Contains("2778580")
                ? """{"response":{"store_items":[{"id":2778580,"success":1,"name":"Shadow of the Erdtree","assets":{},"related_items":{"parent_appid":1245620}}]}}"""
                : """{"response":{"store_items":[{"id":1245620,"success":1,"name":"ELDEN RING","assets":{"community_icon":"ring"}}]}}""");
        })));

        var result = await service.GetAppDetails([2778580]);

        Assert.Equal(2, requests.Count);
        var dlc = Assert.Single(result.Value);
        Assert.Equal("Shadow of the Erdtree", dlc.Value.Name);
        Assert.EndsWith("/apps/1245620/ring.jpg", dlc.Value.IconUrl);
    }

    [Fact]
    public async Task GetAppDetails_SplitsLargeListsIntoBatches()
    {
        var batchSizes = new List<int>();
        var service = new SteamService(new HttpClient(new StubHandler(request =>
        {
            var query = Uri.UnescapeDataString(request.RequestUri!.Query);
            batchSizes.Add(query.Split("\"appid\"").Length - 1);
            return JsonResponse("""{"response":{}}""");
        })));

        var result = await service.GetAppDetails(Enumerable.Range(1, 250).Select(i => (uint)i));

        Assert.True(result.IsSuccess);
        Assert.Equal([100, 100, 50], batchSizes);
    }

    [Fact]
    public async Task GetAppDetails_ConnectionFailure_ReportsConnectionError()
    {
        var service = new SteamService(new HttpClient(new StubHandler(_ => throw new HttpRequestException("offline"))));

        var result = await service.GetAppDetails([620]);

        Assert.Equal("Couldn't connect to Steam API", result.Errors.Single().Message);
    }

    private static SteamService CreateService(string json)
        => new(new HttpClient(new StubHandler(_ => JsonResponse(json))));

    private static HttpResponseMessage JsonResponse(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}

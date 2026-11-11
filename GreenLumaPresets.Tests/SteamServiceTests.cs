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

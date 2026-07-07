using GlobalTeamWidget.Services;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using Xunit;

namespace GlobalTeamWidget.Tests.Services;

public class HolidayServiceTests
{
    private static IHttpClientFactory MakeFactory(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        var client = new HttpClient(handler.Object) { BaseAddress = new Uri("https://date.nager.at/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("holidays")).Returns(client);
        return factory.Object;
    }

    [Fact]
    public async Task GetHolidaysAsync_ReturnsHolidays_OnSuccess()
    {
        var json = """[{"date":"2026-01-01","name":"New Year's Day","types":["Public"]}]""";
        var cache = new CacheService();
        var svc = new HolidayService(MakeFactory(json), cache);

        var result = await svc.GetHolidaysAsync("JP", 2026);

        Assert.Single(result);
        Assert.Equal("New Year's Day", result[0].Name);
        Assert.Equal(new DateOnly(2026, 1, 1), result[0].Date);
    }

    [Fact]
    public async Task GetHolidaysAsync_ReturnsEmpty_ForUnsupportedCountry()
    {
        var cache = new CacheService();
        var svc = new HolidayService(MakeFactory("{}", HttpStatusCode.NotFound), cache);

        var result = await svc.GetHolidaysAsync("XX", 2026);

        Assert.Empty(result);
        Assert.False(svc.IsCountrySupported("XX"));
    }

    [Fact]
    public async Task IsTodayHolidayAsync_ReturnsFalse_WhenNoHoliday()
    {
        var json = """[{"date":"2026-01-01","name":"New Year's Day","types":["Public"]}]""";
        var cache = new CacheService();
        var svc = new HolidayService(MakeFactory(json), cache);

        var result = await svc.IsTodayHolidayAsync("JP", new DateOnly(2026, 6, 15));
        Assert.False(result);
    }
}

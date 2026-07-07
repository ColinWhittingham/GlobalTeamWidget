using GlobalTeamWidget.Services;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using Xunit;

namespace GlobalTeamWidget.Tests.Services;

public class CurrencyServiceTests
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
        var client = new HttpClient(handler.Object) { BaseAddress = new Uri("https://api.frankfurter.dev/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("currency")).Returns(client);
        return factory.Object;
    }

    [Fact]
    public async Task GetRateAsync_ReturnsSameCurrencyRate_WithoutApiCall()
    {
        var factory = new Mock<IHttpClientFactory>();
        var cache = new CacheService();
        var svc = new CurrencyService(factory.Object, cache);

        var result = await svc.GetRateAsync(Guid.NewGuid(), "GBP", "GBP");

        Assert.NotNull(result);
        Assert.Equal(1m, result!.Rate);
        Assert.True(result.IsAvailable);
        factory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetRateAsync_ReturnsRate_OnSuccess()
    {
        var json = """{"base":"GBP","date":"2026-06-29","rates":{"JPY":194.32}}""";
        var cache = new CacheService();
        var svc = new CurrencyService(MakeFactory(json), cache);

        var result = await svc.GetRateAsync(Guid.NewGuid(), "JPY", "GBP");

        Assert.NotNull(result);
        Assert.Equal(194.32m, result!.Rate);
        Assert.True(result.IsAvailable);
    }

    [Fact]
    public async Task GetRateAsync_SetsIsAvailableFalse_On404()
    {
        var cache = new CacheService();
        var svc = new CurrencyService(MakeFactory("{}", HttpStatusCode.NotFound), cache);

        var result = await svc.GetRateAsync(Guid.NewGuid(), "XYZ", "GBP");

        Assert.NotNull(result);
        Assert.False(result!.IsAvailable);
    }
}

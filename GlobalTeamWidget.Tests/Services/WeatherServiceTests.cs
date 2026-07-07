using GlobalTeamWidget.Models;
using GlobalTeamWidget.Services;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using Xunit;

namespace GlobalTeamWidget.Tests.Services;

public class WeatherServiceTests
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
        var client = new HttpClient(handler.Object) { BaseAddress = new Uri("https://api.open-meteo.com/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("weather")).Returns(client);
        return factory.Object;
    }

    [Fact]
    public async Task GetWeatherAsync_ReturnsSnapshot_OnSuccess()
    {
        var json = """{"current":{"temperature_2m":18.4,"weathercode":2}}""";
        var cache = new CacheService();
        var svc = new WeatherService(MakeFactory(json), cache);

        var result = await svc.GetWeatherAsync(Guid.NewGuid(), 51.5, -0.1);

        Assert.NotNull(result);
        Assert.Equal(18.4m, result!.TemperatureCelsius);
        Assert.Equal("Partly cloudy", result.ConditionLabel);
    }

    [Fact]
    public async Task GetWeatherAsync_ReturnsCachedData_OnNetworkFailure()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("weather"))
               .Throws<HttpRequestException>();
        var cache = new CacheService();
        var svc = new WeatherService(factory.Object, cache);

        // No cache exists — should return null gracefully
        var result = await svc.GetWeatherAsync(Guid.NewGuid(), 51.5, -0.1);
        Assert.Null(result);
    }

    [Fact]
    public void WeatherSnapshot_IsStale_WhenFetchedAtOld()
    {
        var snap = new WeatherSnapshot { FetchedAt = DateTimeOffset.UtcNow.AddMinutes(-20) };
        Assert.True(snap.IsStale);
    }

    [Fact]
    public void WeatherSnapshot_NotStale_WhenRecent()
    {
        var snap = new WeatherSnapshot { FetchedAt = DateTimeOffset.UtcNow.AddMinutes(-5) };
        Assert.False(snap.IsStale);
    }

    [Fact]
    public void WeatherSnapshot_FahrenheitConversion_IsCorrect()
    {
        var snap = new WeatherSnapshot { TemperatureCelsius = 0m };
        Assert.Equal(32m, snap.TemperatureFahrenheit);

        snap.TemperatureCelsius = 100m;
        Assert.Equal(212m, snap.TemperatureFahrenheit);
    }
}

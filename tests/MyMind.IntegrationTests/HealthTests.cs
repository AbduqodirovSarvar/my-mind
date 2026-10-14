using System.Net;
using MyMind.IntegrationTests.Infrastructure;

namespace MyMind.IntegrationTests;

public class HealthTests(ApiFactory factory)
{
    [Fact]
    public async Task Health_ReturnsOk_WhenDatabaseIsReachable()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

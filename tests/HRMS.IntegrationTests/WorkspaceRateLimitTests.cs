using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HRMS.IntegrationTests;

public class WorkspaceRateLimitTests
{
    [Fact]
    public async Task PageReadsAreLimitedPerUserAndReturnRetryAfter()
    {
        using var app = new HttpFactory();
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");
        for (var i = 0; i < 120; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Home/Guide")).StatusCode);
        var limited = await client.GetAsync("/Home/Guide");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task MutationsHaveSeparateThirtyRequestBudgetAndKeepAuthorization()
    {
        using var app = new HttpFactory();
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Role", "Employee");
        // Authorization rejects these requests before any database operation.
        for (var i = 0; i < 30; i++)
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/UserAccounts/SetActive?id=unknown", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await client.PostAsync("/UserAccounts/SetActive?id=unknown", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Home/Guide")).StatusCode);
    }
}

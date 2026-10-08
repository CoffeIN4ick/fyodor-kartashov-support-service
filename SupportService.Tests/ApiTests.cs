using System.Net;
using System.Net.Http.Json;

namespace SupportService.Tests;

public class ApiTests
    : IClassFixture<SupportServiceFactory>
{
    private readonly SupportServiceFactory _factory;

    public ApiTests(
        SupportServiceFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Tickets_WithoutAuthorization_Returns401()
    {
        var client = _factory.CreateClient();

        var response =
            await client.GetAsync("/api/tickets");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_Returns200()
    {
        var client = _factory.CreateClient();

        var response =
            await client.PostAsJsonAsync(
                "/api/login",
                new
                {
                    username = "demo",
                    password = "demo"
                });

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = _factory.CreateClient();

        var response =
            await client.PostAsJsonAsync(
                "/api/login",
                new
                {
                    username = "demo",
                    password = "wrong"
                });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Categories_AfterLogin_Returns200()
    {
        var client = _factory.CreateClient();

        await LoginAsync(client);

        var response =
            await client.GetAsync(
                "/api/categories");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task Tickets_WithPagination_Returns200()
    {
        var client = _factory.CreateClient();

        await LoginAsync(client);

        var response =
            await client.GetAsync(
                "/api/tickets?page=1&size=20");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var content =
            await response.Content
                .ReadAsStringAsync();

        Assert.Contains(
            "\"page\":1",
            content);

        Assert.Contains(
            "\"size\":20",
            content);

        Assert.Contains(
            "\"total\"",
            content);

        Assert.Contains(
            "\"items\"",
            content);
    }

    [Fact]
    public async Task MissingTicket_Returns404()
    {
        var client = _factory.CreateClient();

        await LoginAsync(client);

        var response =
            await client.GetAsync(
                "/api/tickets/999999");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task Tickets_WithInvalidPage_Returns400()
    {
        var client = _factory.CreateClient();

        await LoginAsync(client);

        var response =
            await client.GetAsync(
                "/api/tickets?page=abc&size=20");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    private static async Task LoginAsync(
        HttpClient client)
    {
        var response =
            await client.PostAsJsonAsync(
                "/api/login",
                new
                {
                    username = "demo",
                    password = "demo"
                });

        response.EnsureSuccessStatusCode();
    }
}
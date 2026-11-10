using System.Net;
using System.Net.Http.Json;
using System.Text;
using Claims.Application;
using Claims.Infrastructure;
using Claims.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Claims.Tests;

public class ClaimsControllerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static HttpClient CreateClient(ClaimsApiFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost")
    });

    [Fact]
    public async Task Empty_collection_returns_an_empty_array()
    {
        using var factory = new ClaimsApiFactory();
        using var client = CreateClient(factory);
        var claims = await client.GetFromJsonAsync<Claim[]>("/Claims", ClaimsApiFactory.JsonOptions, Token);
        Assert.NotNull(claims);
        Assert.Empty(claims);
    }

    [Fact]
    public async Task Create_read_and_delete_workflow_has_correct_statuses_and_pending_audits()
    {
        using var factory = new ClaimsApiFactory();
        using var client = CreateClient(factory);
        using var coverResponse = await client.PostAsJsonAsync("/Covers", new CreateCoverRequest
        {
            StartDate = new(2028, 1, 1), EndDate = new(2028, 2, 1), Type = CoverType.Yacht
        }, Token);
        Assert.Equal(HttpStatusCode.Created, coverResponse.StatusCode);
        var cover = await coverResponse.Content.ReadFromJsonAsync<Cover>(ClaimsApiFactory.JsonOptions, Token);
        Assert.NotNull(cover);
        Assert.Equal(42556.25m, cover.Premium);
        Assert.NotNull(coverResponse.Headers.Location);
        using var fetchedCover = await client.GetAsync(coverResponse.Headers.Location, Token);
        Assert.Equal(HttpStatusCode.OK, fetchedCover.StatusCode);

        using var claimResponse = await client.PostAsJsonAsync("/Claims", new CreateClaimRequest
        {
            CoverId = cover.Id, Created = cover.StartDate, Name = "Collision", Type = ClaimType.Collision, DamageCost = 100000
        }, Token);
        Assert.Equal(HttpStatusCode.Created, claimResponse.StatusCode);
        Assert.NotNull(claimResponse.Headers.Location);
        var claim = await client.GetFromJsonAsync<Claim>(claimResponse.Headers.Location, ClaimsApiFactory.JsonOptions, Token);
        Assert.NotNull(claim);
        Assert.Equal(cover.Id, claim.CoverId);
        var claims = await client.GetFromJsonAsync<Claim[]>("/Claims", ClaimsApiFactory.JsonOptions, Token);
        Assert.Equal(claim.Id, Assert.Single(claims!).Id);

        using var blockedDelete = await client.DeleteAsync($"/Covers/{cover.Id}", Token);
        Assert.Equal(HttpStatusCode.Conflict, blockedDelete.StatusCode);
        using var deletedClaim = await client.DeleteAsync($"/Claims/{claim.Id}", Token);
        Assert.Equal(HttpStatusCode.NoContent, deletedClaim.StatusCode);
        using var missingClaim = await client.GetAsync($"/Claims/{claim.Id}", Token);
        Assert.Equal(HttpStatusCode.NotFound, missingClaim.StatusCode);
        using var repeatedDelete = await client.DeleteAsync($"/Claims/{claim.Id}", Token);
        Assert.Equal(HttpStatusCode.NotFound, repeatedDelete.StatusCode);
        using var deletedCover = await client.DeleteAsync($"/Covers/{cover.Id}", Token);
        Assert.Equal(HttpStatusCode.NoContent, deletedCover.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
        Assert.Equal(4, await context.AuditOutbox.CountAsync(Token));
        Assert.Empty(await context.ClaimAudits.ToListAsync(Token));
        Assert.Empty(await context.CoverAudits.ToListAsync(Token));
    }

    [Theory]
    [InlineData("/Covers", "{}")]
    [InlineData("/Claims", "{}")]
    [InlineData("/Covers", "{\"startDate\":\"2027-12-31\",\"endDate\":\"2028-01-02\",\"type\":\"Yacht\"}")]
    [InlineData("/Covers", "{\"startDate\":\"2028-01-01\",\"endDate\":\"2029-01-02\",\"type\":\"Yacht\"}")]
    [InlineData("/Covers", "{\"startDate\":\"2028-01-01\",\"endDate\":\"2028-02-01\",\"type\":99}")]
    [InlineData("/Claims", "{\"coverId\":\"missing\",\"created\":\"2028-01-01\",\"name\":\"Claim\",\"type\":\"Fire\",\"damageCost\":100001}")]
    [InlineData("/Claims", "{\"coverId\":\"missing\",\"created\":\"2028-01-01\",\"name\":\"Claim\",\"type\":\"Fire\",\"damageCost\":100}")]
    public async Task Invalid_requests_return_problem_details_without_writes(string endpoint, string json)
    {
        using var factory = new ClaimsApiFactory();
        using var client = CreateClient(factory);
        using var response = await client.PostAsync(endpoint, new StringContent(json, Encoding.UTF8, "application/json"), Token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Token);
        Assert.NotNull(problem);
        Assert.NotEmpty(problem.Errors);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
        Assert.Equal(0, await context.AuditOutbox.CountAsync(Token));
        Assert.Equal(0, await context.Claims.CountAsync(Token));
        Assert.Equal(0, await context.Covers.CountAsync(Token));
    }

    [Theory]
    [InlineData("/Claims/missing")]
    [InlineData("/Covers/missing")]
    public async Task Missing_resource_returns_404_problem_details(string endpoint)
    {
        using var factory = new ClaimsApiFactory();
        using var client = CreateClient(factory);
        using var response = await client.GetAsync(endpoint, Token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Compute_endpoint_uses_the_same_pricing_rules()
    {
        using var factory = new ClaimsApiFactory();
        using var client = CreateClient(factory);
        using var response = await client.PostAsync("/Covers/compute?startDate=2028-01-01&endDate=2028-02-01&coverType=Yacht", null, Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(42556.25m, await response.Content.ReadFromJsonAsync<decimal>(Token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("?startDate=2028-01-01&endDate=2028-02-01")]
    [InlineData("?startDate=2028-01-01&coverType=Yacht")]
    [InlineData("?endDate=2028-02-01&coverType=Yacht")]
    public async Task Compute_requires_all_parameters(string query)
    {
        using var factory = new ClaimsApiFactory();
        using var client = CreateClient(factory);
        using var response = await client.PostAsync("/Covers/compute" + query, null, Token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Client_cannot_override_generated_id_or_premium()
    {
        using var factory = new ClaimsApiFactory();
        using var client = CreateClient(factory);
        using var response = await client.PostAsJsonAsync("/Covers", new
        {
            id = "client-id", premium = 1,
            startDate = "2028-01-01", endDate = "2028-02-01", type = "Yacht"
        }, Token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var cover = await response.Content.ReadFromJsonAsync<Cover>(ClaimsApiFactory.JsonOptions, Token);
        Assert.NotNull(cover);
        Assert.NotEqual("client-id", cover.Id);
        Assert.Equal(42556.25m, cover.Premium);
    }

    [Fact]
    public async Task Unsupported_accept_header_does_not_change_business_error_status()
    {
        using var factory = new ClaimsApiFactory();
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/xml");
        using var response = await client.GetAsync("/Claims/missing", Token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}

using Claims.Application;
using Claims.Models;
using NSubstitute;
using Xunit;

namespace Claims.Tests;

public class ServiceTests
{
    private readonly IClaimRepository claims = Substitute.For<IClaimRepository>();
    private readonly ICoverRepository covers = Substitute.For<ICoverRepository>();
    private readonly IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static DateOnly Today => new(2028, 1, 1);
    private ClaimService ClaimService => new(claims, covers, unitOfWork);
    private CoverService CoverService => new(covers, claims, unitOfWork, new TestClock());

    private static CreateClaimRequest ValidClaim => new()
    {
        CoverId = "cover", Created = Today, Name = "Collision", Type = ClaimType.Collision, DamageCost = 100000
    };

    [Fact]
    public void Core_has_no_api_or_infrastructure_dependencies()
    {
        var references = typeof(ClaimService).Assembly.GetReferencedAssemblies();
        Assert.Equal("Claims.Core", typeof(ClaimService).Assembly.GetName().Name);
        Assert.DoesNotContain(references, reference => reference.Name is "Claims" or "Claims.Infrastructure");
        Assert.DoesNotContain(references, reference =>
            (reference.Name ?? string.Empty).StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
            (reference.Name ?? string.Empty).StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    private void GivenCover() => covers.GetAsync("cover", Token).Returns(new Cover
    {
        Id = "cover", StartDate = Today, EndDate = Today.AddDays(31), Type = CoverType.Yacht
    });

    [Theory]
    [InlineData(-1)]
    [InlineData(100000.01)]
    [InlineData(0.001)]
    public async Task Invalid_cost_is_rejected_without_writing(decimal cost)
    {
        GivenCover();
        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            ClaimService.CreateAsync(ValidClaim with { DamageCost = cost }, Token));
        Assert.Equal("damageCost", exception.Field);
        Assert.Empty(unitOfWork.ReceivedCalls());
        claims.DidNotReceive().Add(Arg.Any<Claim>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100000)]
    public async Task Cost_boundaries_are_accepted_and_audited(decimal cost)
    {
        GivenCover();
        var claim = await ClaimService.CreateAsync(ValidClaim with { DamageCost = cost }, Token);
        Assert.Equal(cost, claim.DamageCost);
        claims.Received(1).Add(claim);
        unitOfWork.Received(1).EnqueueAudit(AuditEntity.Claim, claim.Id, AuditOperation.Create);
        await unitOfWork.Received(1).SaveChangesAsync(Token);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public async Task Claim_date_respects_half_open_cover_period(int offset, bool valid)
    {
        GivenCover();
        var request = ValidClaim with { Created = Today.AddDays(offset) };
        if (valid)
            Assert.Equal(request.Created, (await ClaimService.CreateAsync(request, Token)).Created);
        else
        {
            var exception = await Assert.ThrowsAsync<BusinessException>(() => ClaimService.CreateAsync(request, Token));
            Assert.Equal("created", exception.Field);
            Assert.Empty(unitOfWork.ReceivedCalls());
        }
    }

    [Fact]
    public async Task Missing_cover_is_a_validation_error()
    {
        var exception = await Assert.ThrowsAsync<BusinessException>(() => ClaimService.CreateAsync(ValidClaim, Token));
        Assert.Equal("coverId", exception.Field);
        Assert.Empty(unitOfWork.ReceivedCalls());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_name_is_rejected(string name)
    {
        GivenCover();
        await Assert.ThrowsAsync<BusinessException>(() => ClaimService.CreateAsync(ValidClaim with { Name = name }, Token));
        Assert.Empty(unitOfWork.ReceivedCalls());
    }

    [Fact]
    public async Task Invalid_claim_enum_is_rejected()
    {
        GivenCover();
        await Assert.ThrowsAsync<BusinessException>(() => ClaimService.CreateAsync(ValidClaim with { Type = (ClaimType)99 }, Token));
        Assert.Empty(unitOfWork.ReceivedCalls());
    }

    [Fact]
    public void Past_cover_start_is_rejected()
    {
        var exception = Assert.Throws<BusinessException>(() => CoverService.ComputePremium(Today.AddDays(-1), Today, CoverType.Yacht));
        Assert.Equal("startDate", exception.Field);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(367)]
    public void Invalid_cover_duration_is_rejected(int days)
    {
        var exception = Assert.Throws<BusinessException>(() => CoverService.ComputePremium(Today, Today.AddDays(days), CoverType.Yacht));
        Assert.Equal("endDate", exception.Field);
    }

    [Fact]
    public void Calendar_year_can_include_leap_day()
    {
        Assert.Equal(472477.5m, CoverService.ComputePremium(Today, Today.AddYears(1), CoverType.Yacht));
    }

    [Fact]
    public void Leap_day_anniversary_uses_calendar_year()
    {
        var start = new DateOnly(2028, 2, 29);
        Assert.True(CoverService.ComputePremium(start, new DateOnly(2029, 2, 28), CoverType.Yacht) > 0);
        Assert.Throws<BusinessException>(() => CoverService.ComputePremium(start, new DateOnly(2029, 3, 1), CoverType.Yacht));
    }

    [Fact]
    public void Invalid_cover_enum_is_rejected() =>
        Assert.Throws<BusinessException>(() => CoverService.ComputePremium(Today, Today.AddDays(1), (CoverType)99));

    [Fact]
    public void Last_calendar_year_does_not_overflow()
    {
        Assert.Equal(1375m, CoverService.ComputePremium(new DateOnly(9999, 12, 30), DateOnly.MaxValue, CoverType.Yacht));
    }

    [Fact]
    public async Task Creating_cover_computes_premium_and_commits_one_audit()
    {
        var cover = await CoverService.CreateAsync(new CreateCoverRequest
        {
            StartDate = Today, EndDate = Today.AddDays(31), Type = CoverType.Yacht
        }, Token);
        Assert.Equal(42556.25m, cover.Premium);
        covers.Received(1).Add(cover);
        unitOfWork.Received(1).EnqueueAudit(AuditEntity.Cover, cover.Id, AuditOperation.Create);
        await unitOfWork.Received(1).SaveChangesAsync(Token);
    }

    [Fact]
    public async Task Missing_delete_does_not_produce_an_audit()
    {
        var claimException = await Assert.ThrowsAsync<BusinessException>(() => ClaimService.DeleteAsync("missing", Token));
        var coverException = await Assert.ThrowsAsync<BusinessException>(() => CoverService.DeleteAsync("missing", Token));
        Assert.Equal(BusinessError.NotFound, claimException.Error);
        Assert.Equal(BusinessError.NotFound, coverException.Error);
        Assert.Empty(unitOfWork.ReceivedCalls());
    }

    [Fact]
    public async Task Cover_with_claims_cannot_be_deleted()
    {
        GivenCover();
        claims.ExistsForCoverAsync("cover", Token).Returns(true);
        var exception = await Assert.ThrowsAsync<BusinessException>(() => CoverService.DeleteAsync("cover", Token));
        Assert.Equal(BusinessError.Conflict, exception.Error);
        Assert.Empty(unitOfWork.ReceivedCalls());
    }

    [Fact]
    public async Task Successful_delete_removes_and_audits_each_entity()
    {
        GivenCover();
        var claim = new Claim { Id = "claim", CoverId = "cover", Name = "Collision", Created = Today };
        claims.GetAsync("claim", Token).Returns(claim);
        await ClaimService.DeleteAsync("claim", Token);
        await CoverService.DeleteAsync("cover", Token);
        claims.Received(1).Remove(claim);
        covers.Received(1).Remove(Arg.Is<Cover>(cover => cover.Id == "cover"));
        unitOfWork.Received(1).EnqueueAudit(AuditEntity.Claim, "claim", AuditOperation.Delete);
        unitOfWork.Received(1).EnqueueAudit(AuditEntity.Cover, "cover", AuditOperation.Delete);
        await unitOfWork.Received(2).SaveChangesAsync(Token);
        Assert.Equal(2, unitOfWork.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IUnitOfWork.EnqueueAudit)));
    }
}
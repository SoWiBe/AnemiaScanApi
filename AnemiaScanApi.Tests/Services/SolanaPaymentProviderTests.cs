using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Exceptions;
using AnemiaScanApi.Services.Payments;
using AnemiaScanApi.Settings;
using AnemiaScanApi.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AnemiaScanApi.Tests.Services;

public class SolanaPaymentProviderTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
    private const string Treasury = "9xQeWvG816bUx9EPjHmaT23yvVM2ZWbrrpZb9PusVFin";
    private const string UsdcMint = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";

    private readonly FixedTimeProvider _clock = new(Now);

    private SolanaPaymentProvider NewProvider(Action<SolanaSettings>? configure = null)
    {
        var settings = new SolanaSettings
        {
            Cluster = "devnet",
            TreasuryAddress = Treasury,
            UsdcMint = UsdcMint,
            UseMock = true,
            MockAutoConfirmSeconds = 10,
            IntentTtlMinutes = 15,
            Label = "AnemiaScan"
        };
        configure?.Invoke(settings);

        return new SolanaPaymentProvider(
            Options.Create(settings),
            _clock,
            NullLogger<SolanaPaymentProvider>.Instance);
    }

    private static PaymentIntent NewIntent(Guid? userId = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId ?? Guid.NewGuid(),
        CourseId = Guid.NewGuid(),
        Provider = PaymentProviderType.Solana
    };

    [Fact]
    public void Create_BuildsSolanaPayUrlWithAmountMintAndReference()
    {
        var course = new CourseBuilder().Paid(priceUsdc: 12.5m).Build();
        var intent = NewIntent();

        var initiation = NewProvider().Create(intent, course);

        initiation.PayUrl.Should().StartWith($"solana:{Treasury}?");
        initiation.PayUrl.Should().Contain("amount=12.5");
        initiation.PayUrl.Should().Contain($"spl-token={UsdcMint}");
        initiation.PayUrl.Should().Contain($"reference={intent.ReferenceKey}");
        initiation.QrPayload.Should().Be(initiation.PayUrl);
        initiation.Currency.Should().Be("USDC");
        initiation.Amount.Should().Be(12.5m);
        initiation.Cluster.Should().Be("devnet");
        initiation.IsMock.Should().BeTrue();
    }

    [Fact]
    public void Create_StampsReferenceAndTtlOnTheIntent()
    {
        var course = new CourseBuilder().Paid().Build();
        var intent = NewIntent();

        NewProvider().Create(intent, course);

        intent.ReferenceKey.Should().NotBeNullOrWhiteSpace();
        intent.CreatedAt.Should().Be(Now);
        intent.ExpiresAt.Should().Be(Now.AddMinutes(15));
        intent.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public void Create_TwoIntents_GetDistinctReferences()
    {
        var course = new CourseBuilder().Paid().Build();
        var provider = NewProvider();
        var first = NewIntent();
        var second = NewIntent();

        provider.Create(first, course);
        provider.Create(second, course);

        first.ReferenceKey.Should().NotBe(second.ReferenceKey);
    }

    [Fact]
    public void Create_WithoutTreasuryAddress_Throws503()
    {
        var course = new CourseBuilder().Paid().Build();
        var provider = NewProvider(s => s.TreasuryAddress = "");

        var act = () => provider.Create(NewIntent(), course);

        act.Should().Throw<SASException>().Where(e => e.StatusCode == 503);
    }

    [Fact]
    public async Task GetStatus_BeforeAutoConfirmDelay_StaysPending()
    {
        var course = new CourseBuilder().Paid().Build();
        var provider = NewProvider();
        var intent = NewIntent();
        provider.Create(intent, course);

        _clock.Advance(TimeSpan.FromSeconds(5));
        var result = await provider.GetStatusAsync(intent);

        result.Status.Should().Be(PaymentStatus.Pending);
        result.TransactionSignature.Should().BeNull();
    }

    [Fact]
    public async Task GetStatus_AfterAutoConfirmDelay_ConfirmsWithStableSignature()
    {
        var course = new CourseBuilder().Paid().Build();
        var provider = NewProvider();
        var intent = NewIntent();
        provider.Create(intent, course);

        _clock.Advance(TimeSpan.FromSeconds(10));
        var first = await provider.GetStatusAsync(intent);
        _clock.Advance(TimeSpan.FromSeconds(30));
        var second = await provider.GetStatusAsync(intent);

        first.Status.Should().Be(PaymentStatus.Confirmed);
        first.TransactionSignature.Should().NotBeNullOrWhiteSpace();
        second.TransactionSignature.Should().Be(first.TransactionSignature);
    }

    [Fact]
    public async Task GetStatus_PastTtlWithAutoConfirmDisabled_Expires()
    {
        var course = new CourseBuilder().Paid().Build();
        var provider = NewProvider(s => s.MockAutoConfirmSeconds = -1);
        var intent = NewIntent();
        provider.Create(intent, course);

        _clock.Advance(TimeSpan.FromMinutes(15));
        var result = await provider.GetStatusAsync(intent);

        result.Status.Should().Be(PaymentStatus.Expired);
    }

    [Fact]
    public async Task GetStatus_TerminalIntent_IsNotReEvaluated()
    {
        var provider = NewProvider();
        var intent = NewIntent();
        intent.Status = PaymentStatus.Expired;

        var result = await provider.GetStatusAsync(intent);

        result.Status.Should().Be(PaymentStatus.Expired);
    }

    [Fact]
    public async Task GetStatus_OutsideMockMode_Throws503RatherThanReportingPending()
    {
        var course = new CourseBuilder().Paid().Build();
        var provider = NewProvider(s => s.UseMock = false);
        var intent = NewIntent();
        provider.Create(intent, course);

        var act = async () => await provider.GetStatusAsync(intent);

        await act.Should().ThrowAsync<SASException>().Where(e => e.StatusCode == 503);
    }
}

using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Exceptions;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Infrastructure.Services.Payments.Core;
using AnemiaScanApi.Services.Payments;
using AnemiaScanApi.Settings;
using AnemiaScanApi.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace AnemiaScanApi.Tests.Services;

public class PaymentIntentServiceTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ICoursesRepository> _coursesRepo = new();
    private readonly Mock<IPaymentIntentsRepository> _intentsRepo = new();
    private readonly Mock<ICourseEnrollmentsRepository> _enrollmentsRepo = new();
    private readonly FixedTimeProvider _clock = new(Now);
    private readonly SolanaSettings _solana = new()
    {
        Cluster = "devnet",
        TreasuryAddress = "9xQeWvG816bUx9EPjHmaT23yvVM2ZWbrrpZb9PusVFin",
        UsdcMint = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v",
        UseMock = true,
        MockAutoConfirmSeconds = 10,
        IntentTtlMinutes = 15
    };

    /// <summary>
    /// Intents the repository "holds", so create-then-poll flows see their own writes.
    /// </summary>
    private readonly List<PaymentIntent> _stored = new();

    private PaymentIntentService NewService()
    {
        _intentsRepo.Setup(r => r.CreateAsync(It.IsAny<PaymentIntent>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentIntent, CancellationToken>((i, _) => _stored.Add(i))
            .ReturnsAsync((PaymentIntent i, CancellationToken _) => i);
        _intentsRepo.Setup(r => r.UpdateAsync(It.IsAny<Guid>(), It.IsAny<PaymentIntent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, PaymentIntent i, CancellationToken _) => i);
        _intentsRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _stored.FirstOrDefault(i => i.Id == id)!);
        _intentsRepo.Setup(r => r.GetByUserAndCourseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid u, Guid c, CancellationToken _) =>
                _stored.Where(i => i.UserId == u && i.CourseId == c).ToList());

        var providers = new IPaymentProvider[]
        {
            new SolanaPaymentProvider(Options.Create(_solana), _clock, NullLogger<SolanaPaymentProvider>.Instance),
            new KaspiPaymentProvider()
        };

        return new PaymentIntentService(
            _coursesRepo.Object,
            _intentsRepo.Object,
            _enrollmentsRepo.Object,
            providers,
            Options.Create(_solana),
            _clock,
            NullLogger<PaymentIntentService>.Instance);
    }

    private Course GivenCourse(Action<CourseBuilder>? configure = null)
    {
        var builder = new CourseBuilder().Paid();
        configure?.Invoke(builder);
        var course = builder.Build();

        _coursesRepo.Setup(r => r.GetBySlugAsync(course.Slug, It.IsAny<CancellationToken>())).ReturnsAsync(course);
        return course;
    }

    private void GivenNoExistingEnrollment()
    {
        _enrollmentsRepo.Setup(r => r.GetByUserAndCourseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CourseEnrollment?)null);
        _enrollmentsRepo.Setup(r => r.CreateAsync(It.IsAny<CourseEnrollment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CourseEnrollment e, CancellationToken _) => e);
    }

    [Fact]
    public async Task Create_UnknownCourse_Throws404()
    {
        _coursesRepo.Setup(r => r.GetBySlugAsync("nope", It.IsAny<CancellationToken>())).ReturnsAsync((Course?)null);

        var act = async () => await NewService()
            .CreateCourseIntentAsync(Guid.NewGuid(), "nope", PaymentProviderType.Solana);

        await act.Should().ThrowAsync<SASException>().Where(e => e.StatusCode == 404);
    }

    [Fact]
    public async Task Create_FreeCourse_Throws409()
    {
        var course = GivenCourse(b => b.WithSlug("free-one"));
        course.IsFree = true;

        var act = async () => await NewService()
            .CreateCourseIntentAsync(Guid.NewGuid(), course.Slug, PaymentProviderType.Solana);

        await act.Should().ThrowAsync<SASException>().Where(e => e.StatusCode == 409);
    }

    [Fact]
    public async Task Create_PaidCourse_PersistsPendingIntentWithSnapshottedPrice()
    {
        var userId = Guid.NewGuid();
        var course = GivenCourse(b => b.Paid(priceUsdc: 19.99m));

        var initiation = await NewService()
            .CreateCourseIntentAsync(userId, course.Slug, PaymentProviderType.Solana);

        initiation.Amount.Should().Be(19.99m);
        initiation.ExpiresAt.Should().Be(Now.AddMinutes(15));

        var stored = _stored.Should().ContainSingle().Subject;
        stored.UserId.Should().Be(userId);
        stored.CourseId.Should().Be(course.Id);
        stored.Status.Should().Be(PaymentStatus.Pending);
        stored.Amount.Should().Be(19.99m);
    }

    [Fact]
    public async Task Create_WhileAnIntentIsStillPayable_ReusesItInsteadOfMintingASecond()
    {
        var userId = Guid.NewGuid();
        var course = GivenCourse();
        var service = NewService();

        var first = await service.CreateCourseIntentAsync(userId, course.Slug, PaymentProviderType.Solana);
        _clock.Advance(TimeSpan.FromMinutes(5));
        var second = await service.CreateCourseIntentAsync(userId, course.Slug, PaymentProviderType.Solana);

        second.IntentId.Should().Be(first.IntentId);
        second.Reference.Should().Be(first.Reference);
        _stored.Should().ContainSingle();
    }

    [Fact]
    public async Task Create_AfterTheOldIntentExpired_MintsAFreshOne()
    {
        var userId = Guid.NewGuid();
        var course = GivenCourse();
        var service = NewService();

        var first = await service.CreateCourseIntentAsync(userId, course.Slug, PaymentProviderType.Solana);
        _clock.Advance(TimeSpan.FromMinutes(16));
        var second = await service.CreateCourseIntentAsync(userId, course.Slug, PaymentProviderType.Solana);

        second.IntentId.Should().NotBe(first.IntentId);
        _stored.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_WhenAlreadyPaid_Throws409()
    {
        var userId = Guid.NewGuid();
        var course = GivenCourse();
        GivenNoExistingEnrollment();
        var service = NewService();

        var initiation = await service.CreateCourseIntentAsync(userId, course.Slug, PaymentProviderType.Solana);
        _clock.Advance(TimeSpan.FromSeconds(10));
        await service.GetStatusAsync(userId, initiation.IntentId);

        var act = async () => await service.CreateCourseIntentAsync(userId, course.Slug, PaymentProviderType.Solana);

        await act.Should().ThrowAsync<SASException>().Where(e => e.StatusCode == 409);
    }

    [Fact]
    public async Task Create_OnKaspi_Throws503()
    {
        var course = GivenCourse();

        var act = async () => await NewService()
            .CreateCourseIntentAsync(Guid.NewGuid(), course.Slug, PaymentProviderType.Kaspi);

        await act.Should().ThrowAsync<SASException>().Where(e => e.StatusCode == 503);
    }

    [Fact]
    public async Task GetStatus_OtherUsersIntent_Throws404()
    {
        var owner = Guid.NewGuid();
        var course = GivenCourse();
        var service = NewService();
        var initiation = await service.CreateCourseIntentAsync(owner, course.Slug, PaymentProviderType.Solana);

        var act = async () => await service.GetStatusAsync(Guid.NewGuid(), initiation.IntentId);

        await act.Should().ThrowAsync<SASException>().Where(e => e.StatusCode == 404);
    }

    [Fact]
    public async Task GetStatus_BeforeConfirmation_ReportsPendingWithoutEnrollment()
    {
        var userId = Guid.NewGuid();
        var course = GivenCourse();
        GivenNoExistingEnrollment();
        var service = NewService();
        var initiation = await service.CreateCourseIntentAsync(userId, course.Slug, PaymentProviderType.Solana);

        var status = await service.GetStatusAsync(userId, initiation.IntentId);

        status.Status.Should().Be(PaymentStatus.Pending);
        status.EnrollmentId.Should().BeNull();
        _enrollmentsRepo.Verify(r => r.CreateAsync(It.IsAny<CourseEnrollment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetStatus_OnConfirmation_CreatesEnrollmentLinkedToTheIntent()
    {
        var userId = Guid.NewGuid();
        var course = GivenCourse();
        GivenNoExistingEnrollment();

        CourseEnrollment? created = null;
        _enrollmentsRepo.Setup(r => r.CreateAsync(It.IsAny<CourseEnrollment>(), It.IsAny<CancellationToken>()))
            .Callback<CourseEnrollment, CancellationToken>((e, _) => created = e)
            .ReturnsAsync((CourseEnrollment e, CancellationToken _) => e);

        var service = NewService();
        var initiation = await service.CreateCourseIntentAsync(userId, course.Slug, PaymentProviderType.Solana);
        _clock.Advance(TimeSpan.FromSeconds(10));

        var status = await service.GetStatusAsync(userId, initiation.IntentId);

        status.Status.Should().Be(PaymentStatus.Confirmed);
        status.TransactionSignature.Should().NotBeNullOrWhiteSpace();
        created.Should().NotBeNull();
        created!.UserId.Should().Be(userId);
        created.CourseId.Should().Be(course.Id);
        created.PaidIntentId.Should().Be(initiation.IntentId);
        status.EnrollmentId.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetStatus_PolledRepeatedlyAfterConfirmation_EnrollsExactlyOnce()
    {
        var userId = Guid.NewGuid();
        var course = GivenCourse();
        GivenNoExistingEnrollment();
        var service = NewService();
        var initiation = await service.CreateCourseIntentAsync(userId, course.Slug, PaymentProviderType.Solana);
        _clock.Advance(TimeSpan.FromSeconds(10));

        var first = await service.GetStatusAsync(userId, initiation.IntentId);
        var second = await service.GetStatusAsync(userId, initiation.IntentId);
        var third = await service.GetStatusAsync(userId, initiation.IntentId);

        second.EnrollmentId.Should().Be(first.EnrollmentId);
        third.EnrollmentId.Should().Be(first.EnrollmentId);
        _enrollmentsRepo.Verify(r => r.CreateAsync(It.IsAny<CourseEnrollment>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetStatus_WhenUserAlreadyEnrolledForFree_UpgradesThatEnrollmentInPlace()
    {
        var userId = Guid.NewGuid();
        var course = GivenCourse();
        var existing = new CourseEnrollment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CourseId = course.Id
        };

        _enrollmentsRepo.Setup(r => r.GetByUserAndCourseAsync(userId, course.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _enrollmentsRepo.Setup(r => r.UpdateAsync(existing.Id, It.IsAny<CourseEnrollment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, CourseEnrollment e, CancellationToken _) => e);

        var service = NewService();
        var initiation = await service.CreateCourseIntentAsync(userId, course.Slug, PaymentProviderType.Solana);
        _clock.Advance(TimeSpan.FromSeconds(10));

        var status = await service.GetStatusAsync(userId, initiation.IntentId);

        status.EnrollmentId.Should().Be(existing.Id);
        existing.PaidIntentId.Should().Be(initiation.IntentId);
        _enrollmentsRepo.Verify(r => r.CreateAsync(It.IsAny<CourseEnrollment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetStatus_PastTtlWithoutPayment_Expires()
    {
        _solana.MockAutoConfirmSeconds = -1;
        var userId = Guid.NewGuid();
        var course = GivenCourse();
        GivenNoExistingEnrollment();
        var service = NewService();
        var initiation = await service.CreateCourseIntentAsync(userId, course.Slug, PaymentProviderType.Solana);

        _clock.Advance(TimeSpan.FromMinutes(15));
        var status = await service.GetStatusAsync(userId, initiation.IntentId);

        status.Status.Should().Be(PaymentStatus.Expired);
        status.EnrollmentId.Should().BeNull();
    }

    [Fact]
    public async Task MockConfirm_ConfirmsImmediatelyWithoutWaitingForTheDelay()
    {
        _solana.MockAutoConfirmSeconds = 600;
        var userId = Guid.NewGuid();
        var course = GivenCourse();
        GivenNoExistingEnrollment();
        var service = NewService();
        var initiation = await service.CreateCourseIntentAsync(userId, course.Slug, PaymentProviderType.Solana);

        var status = await service.MockConfirmAsync(userId, initiation.IntentId);

        status.Status.Should().Be(PaymentStatus.Confirmed);
        status.EnrollmentId.Should().NotBeNull();
    }

    [Fact]
    public async Task MockConfirm_OutsideMockMode_Throws404()
    {
        var userId = Guid.NewGuid();
        var course = GivenCourse();
        var service = NewService();
        var initiation = await service.CreateCourseIntentAsync(userId, course.Slug, PaymentProviderType.Solana);

        _solana.UseMock = false;
        var act = async () => await service.MockConfirmAsync(userId, initiation.IntentId);

        await act.Should().ThrowAsync<SASException>().Where(e => e.StatusCode == 404);
    }

    [Fact]
    public async Task MockConfirm_OnExpiredIntent_Throws409()
    {
        _solana.MockAutoConfirmSeconds = -1;
        var userId = Guid.NewGuid();
        var course = GivenCourse();
        var service = NewService();
        var initiation = await service.CreateCourseIntentAsync(userId, course.Slug, PaymentProviderType.Solana);

        _clock.Advance(TimeSpan.FromMinutes(16));
        var act = async () => await service.MockConfirmAsync(userId, initiation.IntentId);

        await act.Should().ThrowAsync<SASException>().Where(e => e.StatusCode == 409);
    }

    [Fact]
    public async Task Reconcile_SettlesAnIntentTheClientNeverPolled()
    {
        var userId = Guid.NewGuid();
        var course = GivenCourse();
        GivenNoExistingEnrollment();
        var service = NewService();
        await service.CreateCourseIntentAsync(userId, course.Slug, PaymentProviderType.Solana);
        var intent = _stored.Single();

        _clock.Advance(TimeSpan.FromSeconds(10));
        var status = await service.ReconcileAsync(intent);

        status.Should().Be(PaymentStatus.Confirmed);
        intent.EnrollmentId.Should().NotBeNull();
        _enrollmentsRepo.Verify(r => r.CreateAsync(It.IsAny<CourseEnrollment>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

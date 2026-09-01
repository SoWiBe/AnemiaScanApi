using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Common.Responses.Payments;
using AnemiaScanApi.Exceptions;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Infrastructure.Services.Core;
using AnemiaScanApi.Infrastructure.Services.Payments.Core;
using AnemiaScanApi.Settings;

using Microsoft.Extensions.Options;

namespace AnemiaScanApi.Services.Payments;

/// <summary>
/// Owns the payment intent lifecycle. Providers only observe; every state transition and the
/// enrollment a confirmation unlocks are written here, so the client poll and the reconciliation
/// worker cannot diverge.
/// </summary>
public class PaymentIntentService(
    ICoursesRepository coursesRepository,
    IPaymentIntentsRepository intentsRepository,
    ICourseEnrollmentsRepository enrollmentsRepository,
    IEnumerable<IPaymentProvider> providers,
    IOptions<SolanaSettings> solanaSettings,
    TimeProvider timeProvider,
    ILogger<PaymentIntentService> logger)
    : BaseService<PaymentIntentService>(logger), IPaymentIntentService
{
    private readonly List<IPaymentProvider> _providers = providers.ToList();

    public async Task<PaymentInitiationResponse> CreateCourseIntentAsync(
        Guid userId,
        string courseSlug,
        PaymentProviderType provider,
        CancellationToken cancellationToken = default)
    {
        var course = await coursesRepository.GetBySlugAsync(courseSlug, cancellationToken)
            ?? throw new SASException(ExceptionMessage.CourseNotFound, 404);

        if (course.IsFree)
        {
            throw new SASException(ExceptionMessage.CourseIsFree, 409);
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var history = (await intentsRepository.GetByUserAndCourseAsync(userId, course.Id, cancellationToken)).ToList();

        if (history.Any(i => i.Status == PaymentStatus.Confirmed))
        {
            throw new SASException(ExceptionMessage.CourseAlreadyPaid, 409);
        }

        var handler = ResolveProvider(provider);

        // Hand back the intent already in flight rather than minting a second one — a user who
        // reopens the payment screen must keep the same reference key, or an in-flight transfer
        // would settle against an intent nobody is polling.
        var reusable = history.FirstOrDefault(i => i.Provider == provider && i.IsPayable(utcNow));
        if (reusable is not null)
        {
            Logger.LogInformation("Reusing payable intent {IntentId} for user {UserId} course {CourseId}",
                reusable.Id, userId, course.Id);
            return handler.Describe(reusable, course);
        }

        var intent = new PaymentIntent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CourseId = course.Id,
            Provider = provider
        };

        var initiation = handler.Create(intent, course);
        await intentsRepository.CreateAsync(intent, cancellationToken);

        return initiation;
    }

    public async Task<PaymentStatusResponse> GetStatusAsync(Guid userId, Guid intentId, CancellationToken cancellationToken = default)
    {
        var intent = await LoadOwnedIntentAsync(userId, intentId, cancellationToken);
        await ReconcileAsync(intent, cancellationToken);
        return ToResponse(intent);
    }

    public async Task<PaymentStatus> ReconcileAsync(PaymentIntent intent, CancellationToken cancellationToken = default)
    {
        if (intent.Status != PaymentStatus.Pending)
        {
            return intent.Status;
        }

        var handler = ResolveProvider(intent.Provider);
        var observed = await handler.GetStatusAsync(intent, cancellationToken);

        if (observed.Status == PaymentStatus.Pending)
        {
            return intent.Status;
        }

        await ApplyTerminalStatusAsync(intent, observed.Status, observed.TransactionSignature, cancellationToken);
        return intent.Status;
    }

    public async Task<PaymentStatusResponse> MockConfirmAsync(Guid userId, Guid intentId, CancellationToken cancellationToken = default)
    {
        if (!solanaSettings.Value.UseMock)
        {
            // Not "forbidden" — outside mock mode this endpoint does not exist at all.
            throw new SASException(ExceptionMessage.PaymentIntentNotFound, 404);
        }

        var intent = await LoadOwnedIntentAsync(userId, intentId, cancellationToken);

        if (intent.Status == PaymentStatus.Confirmed)
        {
            return ToResponse(intent);
        }

        if (!intent.IsPayable(timeProvider.GetUtcNow().UtcDateTime))
        {
            throw new SASException(ExceptionMessage.PaymentExpired, 409);
        }

        await ApplyTerminalStatusAsync(
            intent,
            PaymentStatus.Confirmed,
            SolanaPaymentProvider.SyntheticSignature(intent),
            cancellationToken);

        return ToResponse(intent);
    }

    /// <summary>
    /// Writes a terminal status onto the intent, creating the enrollment first when confirming so
    /// a crash between the two writes leaves the intent Pending (retryable) rather than confirmed
    /// with no course access.
    /// </summary>
    private async Task ApplyTerminalStatusAsync(
        PaymentIntent intent,
        PaymentStatus status,
        string? signature,
        CancellationToken cancellationToken)
    {
        if (status == PaymentStatus.Confirmed)
        {
            intent.EnrollmentId = await EnsureEnrollmentAsync(intent, cancellationToken);
            intent.ConfirmedAt = timeProvider.GetUtcNow().UtcDateTime;
            intent.TransactionSignature = signature;
        }

        intent.Status = status;
        await intentsRepository.UpdateAsync(intent.Id, intent, cancellationToken);

        Logger.LogInformation(
            "Payment intent {IntentId} for user {UserId} course {CourseId} → {Status} (enrollment {EnrollmentId})",
            intent.Id, intent.UserId, intent.CourseId, intent.Status, intent.EnrollmentId);
    }

    /// <summary>
    /// Returns the enrollment this payment unlocks, creating it only if the user has none for the
    /// course. Idempotent — the client poll and the worker can both land here for the same intent.
    /// </summary>
    private async Task<Guid> EnsureEnrollmentAsync(PaymentIntent intent, CancellationToken cancellationToken)
    {
        if (intent.EnrollmentId is not null)
        {
            return intent.EnrollmentId.Value;
        }

        var existing = await enrollmentsRepository.GetByUserAndCourseAsync(intent.UserId, intent.CourseId, cancellationToken);
        if (existing is not null)
        {
            // The user may have enrolled while the course was still free, or during the preview
            // window — upgrade that enrollment in place instead of creating a second one.
            if (existing.PaidIntentId != intent.Id)
            {
                existing.PaidIntentId = intent.Id;
                await enrollmentsRepository.UpdateAsync(existing.Id, existing, cancellationToken);
            }
            return existing.Id;
        }

        var enrollment = new CourseEnrollment
        {
            Id = Guid.NewGuid(),
            UserId = intent.UserId,
            CourseId = intent.CourseId,
            EnrolledAt = timeProvider.GetUtcNow().UtcDateTime,
            PaidIntentId = intent.Id
        };

        await enrollmentsRepository.CreateAsync(enrollment, cancellationToken);
        return enrollment.Id;
    }

    private async Task<PaymentIntent> LoadOwnedIntentAsync(Guid userId, Guid intentId, CancellationToken cancellationToken)
    {
        var intent = await intentsRepository.GetByIdAsync(intentId, cancellationToken);
        if (intent is null || intent.UserId != userId)
        {
            throw new SASException(ExceptionMessage.PaymentIntentNotFound, 404);
        }
        return intent;
    }

    private IPaymentProvider ResolveProvider(PaymentProviderType type)
        => _providers.FirstOrDefault(p => p.Type == type)
           ?? throw new SASException(ExceptionMessage.PaymentProviderNotAvailable, 503);

    private static PaymentStatusResponse ToResponse(PaymentIntent intent) => new(
        intent.Id,
        intent.Status,
        intent.EnrollmentId,
        intent.TransactionSignature,
        intent.ExpiresAt);
}

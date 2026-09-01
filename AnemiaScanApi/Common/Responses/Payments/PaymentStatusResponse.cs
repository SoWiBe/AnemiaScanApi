using AnemiaScanApi.Common.Enums;

namespace AnemiaScanApi.Common.Responses.Payments;

/// <summary>
/// Result of a status poll.
/// </summary>
/// <param name="IntentId">Intent that was polled.</param>
/// <param name="Status">Current lifecycle state.</param>
/// <param name="EnrollmentId">Set once <paramref name="Status"/> is Confirmed — the enrollment unlocked by this payment.</param>
/// <param name="TransactionSignature">On-chain signature once confirmed; synthetic in mock mode.</param>
/// <param name="ExpiresAt">Deadline for a still-Pending intent.</param>
public record PaymentStatusResponse(
    Guid IntentId,
    PaymentStatus Status,
    Guid? EnrollmentId,
    string? TransactionSignature,
    DateTime ExpiresAt);

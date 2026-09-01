using AnemiaScanApi.Common.Enums;
using MongoDB.Bson.Serialization.Attributes;

namespace AnemiaScanApi.Common;

/// <summary>
/// A single attempt by a user to pay for a course through one provider.
/// Persisted so both the client poll and the reconciliation worker converge on the same record.
/// </summary>
public class PaymentIntent : BaseMongoModel
{
    /// <summary>
    /// User who initiated the payment.
    /// </summary>
    [BsonElement("user_id")] public Guid UserId { get; set; }

    /// <summary>
    /// Course being purchased.
    /// </summary>
    [BsonElement("course_id")] public Guid CourseId { get; set; }

    /// <summary>
    /// Rail this intent was created on. Determines which <c>IPaymentProvider</c> handles it.
    /// </summary>
    [BsonElement("provider")] public PaymentProviderType Provider { get; set; }

    /// <summary>
    /// Amount in <see cref="Currency"/>, snapshotted from the course at creation time so a
    /// later price change cannot invalidate an in-flight payment.
    /// </summary>
    [BsonElement("amount")] public decimal Amount { get; set; }

    /// <summary>
    /// Currency code — "USDC" for Solana, "KZT" for Kaspi.
    /// </summary>
    [BsonElement("currency")] public string Currency { get; set; } = null!;

    /// <summary>
    /// Solana Pay reference key (base58 of 32 random bytes). Unique per intent — this is what
    /// identifies the transaction on-chain via <c>getSignaturesForAddress</c>.
    /// </summary>
    [BsonElement("reference_key")] public string ReferenceKey { get; set; } = null!;

    [BsonElement("status")] public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>
    /// On-chain transaction signature once confirmed. In mock mode this is a synthetic value.
    /// </summary>
    [BsonElement("transaction_signature")] public string? TransactionSignature { get; set; }

    /// <summary>
    /// Enrollment created when this intent was confirmed. Guards against double-enrolling
    /// when the client poll and the reconciliation worker race.
    /// </summary>
    [BsonElement("enrollment_id")] public Guid? EnrollmentId { get; set; }

    [BsonElement("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// After this moment a still-Pending intent flips to <see cref="PaymentStatus.Expired"/>.
    /// </summary>
    [BsonElement("expires_at")] public DateTime ExpiresAt { get; set; }

    [BsonElement("confirmed_at")] public DateTime? ConfirmedAt { get; set; }

    /// <summary>
    /// True while the intent can still be paid — pending and not past its TTL.
    /// </summary>
    public bool IsPayable(DateTime utcNow) => Status == PaymentStatus.Pending && utcNow < ExpiresAt;
}

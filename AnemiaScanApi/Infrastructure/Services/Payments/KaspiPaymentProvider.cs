using AnemiaScanApi.Common;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.Enums;
using AnemiaScanApi.Common.Responses.Payments;
using AnemiaScanApi.Exceptions;
using AnemiaScanApi.Infrastructure.Services.Payments.Core;

namespace AnemiaScanApi.Services.Payments;

/// <summary>
/// Kaspi/card rail. Registered from day one so the provider lookup, the DTOs and the client's
/// provider picker are exercised against two rails — but the integration itself lands in Phase 3.
/// Every call fails with 503 rather than pretending to accept money.
/// </summary>
public class KaspiPaymentProvider : IPaymentProvider
{
    public PaymentProviderType Type => PaymentProviderType.Kaspi;

    public PaymentInitiationResponse Create(PaymentIntent intent, Course course)
        => throw new SASException(ExceptionMessage.PaymentProviderNotAvailable, 503);

    public PaymentInitiationResponse Describe(PaymentIntent intent, Course course)
        => throw new SASException(ExceptionMessage.PaymentProviderNotAvailable, 503);

    public Task<ProviderStatusResult> GetStatusAsync(PaymentIntent intent, CancellationToken cancellationToken = default)
        => throw new SASException(ExceptionMessage.PaymentProviderNotAvailable, 503);
}

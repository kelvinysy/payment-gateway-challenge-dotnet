using JetBrains.Annotations;

namespace PaymentGateway.Api.Enums;

[PublicAPI]
public enum PaymentStatus
{
    Authorized,
    Declined,
    Rejected
}
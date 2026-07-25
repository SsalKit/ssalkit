namespace SsalKit.Guard.IntegrationTests.TestDomain;

/// <summary>
/// A second, unrelated code set. Its only job is to prove that two containers over two code enums
/// coexist in one assembly without either one seeing the other's registrations.
/// </summary>
public enum BillingStatusCode
{
    /// <summary>No code.</summary>
    Unspecified = 0,

    /// <summary>The payment instrument was refused.</summary>
    CardDeclined = 5001,

    /// <summary>The payment gateway did not answer in time.</summary>
    PaymentTimeout = 5002,
}

/// <summary>
/// The billing domain's own exception. <see cref="GameErrors"/> must not map it: it declares a code
/// in a different enum, so it belongs to a different container.
/// </summary>
[ErrorCode<BillingStatusCode>(BillingStatusCode.CardDeclined)]
public sealed class CardDeclinedException : ErrorCodedException
{
    /// <summary>Initializes a new instance of the <see cref="CardDeclinedException"/> class.</summary>
    /// <param name="message">The message that describes the error.</param>
    public CardDeclinedException(string? message = null)
        : base(message)
    {
    }
}

/// <summary>
/// The billing domain's mapping container.
/// </summary>
/// <remarks>
/// <see cref="TimeoutException"/> is registered here too, with a different code than the one
/// <see cref="GameErrors"/> gives it -- the same exception type means different things at different
/// boundaries, which is precisely why the mapping lives in a container rather than on the exception.
/// </remarks>
[ErrorCodes<BillingStatusCode>]
[ExternalErrorCode<BillingStatusCode>(typeof(TimeoutException), BillingStatusCode.PaymentTimeout)]
public static partial class BillingErrors
{
}

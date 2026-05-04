using System.Diagnostics;

using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Services;

public interface IPaymentsRepository
{
    public void Add(StoredPayment payment);
    public StoredPayment? Get(Guid id);
}

public class PaymentsRepository(ActivitySource activitySource, ILogger<PaymentsRepository> logger) : IPaymentsRepository
{
    private readonly HashSet<StoredPayment> _payments = [];
    
    public void Add(StoredPayment payment)
    {
        activitySource.StartActivity();
        
        try
        {
            // Assumption: Id is unique so we should not be able to put duplicates in
            _ = _payments.First(p => p.PaymentResponse.Id == payment.PaymentResponse.Id);
            logger.LogWarning("Payment with {Id} already exists in repository", payment.PaymentResponse.Id);
        }
        catch (InvalidOperationException)
        {
            _payments.Add(payment);
        }
    }

    public StoredPayment? Get(Guid id)
    {
        activitySource.StartActivity();
        
        try
        {
            return _payments.First(p => p.PaymentResponse.Id == id);
        }
        catch (Exception e)
        {
            logger.LogError("Payment with {Id} not found in repository with {Exception}", id, e.Message);
            return null;
        }
    }
}
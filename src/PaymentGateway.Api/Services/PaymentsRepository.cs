using System.Collections.Concurrent;
using System.Diagnostics;

using PaymentGateway.Api.Models;

namespace PaymentGateway.Api.Services;

public interface IPaymentsRepository
{
    public StoredPayment Add(StoredPayment payment);
    public StoredPayment? Get(Guid id);
}

public class PaymentsRepository(ActivitySource activitySource, ILogger<PaymentsRepository> logger) : IPaymentsRepository
{
    private readonly ConcurrentDictionary<Guid, StoredPayment> _payments = new();

    public StoredPayment Add(StoredPayment payment)
    {
        using var activity = activitySource.StartActivity();
        activity?.AddTag("payment.id", payment.PaymentResponse.Id);
        
        try
        {
            var result = _payments.AddOrUpdate(
                payment.PaymentResponse.Id,
                payment,
                (_, existingPayment) => existingPayment);
            
            if (!ReferenceEquals(result, payment))
            {
                activity?.AddTag("repository.action", "duplicate_detected");
                logger.LogWarning("Payment with {Id} already exists in repository, returning existing payment", 
                    payment.PaymentResponse.Id);
                return result;
            }

            activity?.AddTag("repository.action", "add");
            return payment;
        }
        catch (Exception ex)
        {
            activity?.AddTag("error", true);
            activity?.AddTag("error.message", ex.Message);
            logger.LogError(ex, "Error adding payment to repository");
            throw;
        }
    }

    public StoredPayment? Get(Guid id)
    {
        using var activity = activitySource.StartActivity();
        activity?.AddTag("payment.id", id);
        
        try
        {
            if (_payments.TryGetValue(id, out var payment))
            {
                activity?.AddTag("repository.cache_hit", true);
                return payment;
            }

            activity?.AddTag("repository.cache_hit", false);
            return null;
        }
        catch (Exception ex)
        {
            activity?.AddTag("error", true);
            activity?.AddTag("error.message", ex.Message);
            logger.LogError(ex, "Error retrieving payment from repository with id {Id}", id);
            return null;
        }
    }
}
using Bansang.Domain.Common;

namespace Bansang.Domain.Sales;

public sealed record PaymentInput(PaymentMethod Method, decimal Amount, string? Reference = null);

/// <summary>รับเงิน / คืนเงิน ของบิล (IsRefund = คืนเงินลูกค้า)</summary>
public class Payment
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public PaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    public string? Reference { get; private set; }
    public bool IsRefund { get; private set; }
    public Guid? ReturnId { get; private set; }
    public string UserId { get; private set; } = null!;
    public DateTimeOffset At { get; private set; }

    private Payment() { }

    internal Payment(Guid orderId, PaymentMethod method, decimal amount, string? reference, bool isRefund,
        Guid? returnId, string userId, DateTimeOffset at)
    {
        if (amount <= 0) throw new DomainException("payment_not_positive", "ยอดเงินต้องมากกว่า 0");
        Id = Guid.CreateVersion7();
        OrderId = orderId;
        Method = method;
        Amount = Quantity.RoundMoney(amount);
        Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        IsRefund = isRefund;
        ReturnId = returnId;
        UserId = userId;
        At = at;
    }
}

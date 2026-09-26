namespace Bansang.Domain.Sales;

/// <summary>รอบรับของ / ส่งของ 1 ครั้ง</summary>
public class Fulfillment
{
    private readonly List<FulfillmentLine> _lines = [];

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string Number { get; private set; } = null!;
    public string? Note { get; private set; }
    public string UserId { get; private set; } = null!;
    public DateTimeOffset At { get; private set; }
    public IReadOnlyList<FulfillmentLine> Lines => _lines;

    private Fulfillment() { }

    internal Fulfillment(Guid orderId, string number, string? note, string userId, DateTimeOffset at)
    {
        Id = Guid.CreateVersion7();
        OrderId = orderId;
        Number = number;
        Note = note;
        UserId = userId;
        At = at;
    }

    internal void Add(SalesLine line, decimal qty, decimal qtyBase)
        => _lines.Add(new FulfillmentLine(Id, line.Id, qty, qtyBase));
}

public class FulfillmentLine
{
    public Guid Id { get; private set; }
    public Guid FulfillmentId { get; private set; }
    public Guid SalesLineId { get; private set; }
    public decimal Qty { get; private set; }
    public decimal QtyBase { get; private set; }

    private FulfillmentLine() { }

    internal FulfillmentLine(Guid fulfillmentId, Guid salesLineId, decimal qty, decimal qtyBase)
    {
        Id = Guid.CreateVersion7();
        FulfillmentId = fulfillmentId;
        SalesLineId = salesLineId;
        Qty = qty;
        QtyBase = qtyBase;
    }
}

/// <summary>ใบคืนของ</summary>
public class SalesReturn
{
    private readonly List<SalesReturnLine> _lines = [];

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string Number { get; private set; } = null!;
    public string Reason { get; private set; } = null!;
    public decimal RefundAmount { get; private set; }
    public string UserId { get; private set; } = null!;
    public DateTimeOffset At { get; private set; }
    public IReadOnlyList<SalesReturnLine> Lines => _lines;

    private SalesReturn() { }

    internal SalesReturn(Guid orderId, string number, string reason, string userId, DateTimeOffset at)
    {
        Id = Guid.CreateVersion7();
        OrderId = orderId;
        Number = number;
        Reason = reason;
        UserId = userId;
        At = at;
    }

    internal void Add(SalesLine line, decimal qty, decimal qtyBase, decimal amount)
    {
        _lines.Add(new SalesReturnLine(Id, line.Id, qty, qtyBase, amount));
        RefundAmount += amount;
    }
}

public class SalesReturnLine
{
    public Guid Id { get; private set; }
    public Guid ReturnId { get; private set; }
    public Guid SalesLineId { get; private set; }
    public decimal Qty { get; private set; }
    public decimal QtyBase { get; private set; }
    public decimal Amount { get; private set; }

    private SalesReturnLine() { }

    internal SalesReturnLine(Guid returnId, Guid salesLineId, decimal qty, decimal qtyBase, decimal amount)
    {
        Id = Guid.CreateVersion7();
        ReturnId = returnId;
        SalesLineId = salesLineId;
        Qty = qty;
        QtyBase = qtyBase;
        Amount = amount;
    }
}

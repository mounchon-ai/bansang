using Bansang.Application.Abstractions;
using Bansang.Application.Contracts;
using Bansang.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Bansang.Application.Sales;

public sealed class CustomerService(IAppDbContext db, DocumentNumbers numbers)
{
    public async Task<PagedResult<CustomerDto>> ListAsync(CustomerQuery q, CancellationToken ct)
    {
        var query = db.Customers.AsNoTracking();
        if (q.Tier is { } tier) query = query.Where(c => c.Tier == tier);
        if (q.IsActive is { } active) query = query.Where(c => c.IsActive == active);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            query = query.Where(c => EF.Functions.ILike(c.Name, term) || EF.Functions.ILike(c.Code, term)
                                     || EF.Functions.ILike(c.Phone!, term) || EF.Functions.ILike(c.TaxId!, term));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(c => c.Name).Skip(q.Skip).Take(q.SafePageSize).ToListAsync(ct);
        return new(items.Select(c => c.ToDto()).ToList(), q.SafePage, q.SafePageSize, total);
    }

    public async Task<CustomerDto> GetAsync(Guid id, CancellationToken ct) => (await FindAsync(id, ct)).ToDto();

    public async Task<CustomerDto> CreateAsync(SaveCustomerRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var code = string.IsNullOrWhiteSpace(r.Code) ? await numbers.NextPlainAsync(DocumentNumbers.Customer, ct) : r.Code.Trim();
        if (await db.Customers.AnyAsync(c => c.Code == code, ct))
            throw new ConflictException("customer_code_duplicate", $"รหัสลูกค้า '{code}' มีอยู่แล้ว");
        var c = new Customer(code, r.Name, r.Tier, r.Phone, r.TaxId, r.Address);
        if (!r.IsActive) c.Update(r.Name, r.Tier, r.Phone, r.TaxId, r.Address, false);
        db.Customers.Add(c);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return c.ToDto();
    }

    public async Task<CustomerDto> UpdateAsync(Guid id, SaveCustomerRequest r, CancellationToken ct)
    {
        var c = await FindAsync(id, ct);
        c.Update(r.Name, r.Tier, r.Phone, r.TaxId, r.Address, r.IsActive);
        await db.SaveChangesAsync(ct);
        return c.ToDto();
    }

    private async Task<Customer> FindAsync(Guid id, CancellationToken ct)
        => await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("ลูกค้า", id);
}

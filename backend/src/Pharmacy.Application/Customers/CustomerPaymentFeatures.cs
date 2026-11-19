using Pharmacy.Shared.Localization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Customers;

namespace Pharmacy.Application.Customers;

/// <param name="Id">Optional client-generated id (idempotent; used by offline sync).</param>
public sealed record CreateCustomerPaymentCommand(
    Guid CustomerId,
    decimal Amount,
    string? PaymentMethod = null,
    Guid? LocationId = null,
    string? Notes = null,
    Guid? Id = null,
    string? Number = null,
    DateTimeOffset? PaidAt = null,
    string? DeviceId = null,
    bool IsOffline = false) : IRequest<CreateCustomerPaymentResult>;

public sealed record CreateCustomerPaymentResult(Guid Id, string Number, decimal NewBalance, bool AlreadyExisted, IReadOnlyList<string> Warnings);

public sealed class CreateCustomerPaymentCommandValidator : AbstractValidator<CreateCustomerPaymentCommand>
{
    public CreateCustomerPaymentCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Notes).MaximumLength(512);
        RuleFor(x => x.Number).MaximumLength(64);
    }
}

public sealed class CreateCustomerPaymentCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<CreateCustomerPaymentCommand, CreateCustomerPaymentResult>
{
    public async Task<CreateCustomerPaymentResult> Handle(CreateCustomerPaymentCommand request, CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) throw new InvalidOperationException("Tenant is required.");
        var now = DateTimeOffset.UtcNow;

        if (request.Id.HasValue)
        {
            var existing = await db.CustomerPayments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            if (existing is not null)
            {
                var bal = (await CustomerBalances.GetAsync(db, [existing.CustomerId], cancellationToken)).GetValueOrDefault(existing.CustomerId);
                return new CreateCustomerPaymentResult(existing.Id, existing.Number, bal, true, []);
            }
        }

        if (!await db.Customers.AnyAsync(x => x.Id == request.CustomerId, cancellationToken))
            throw new InvalidOperationException("Customer not found.");

        var warnings = new List<string>();
        var balance = (await CustomerBalances.GetAsync(db, [request.CustomerId], cancellationToken)).GetValueOrDefault(request.CustomerId);
        if (request.Amount > balance)
        {
            var msg = $"Payment {request.Amount:0.##} exceeds outstanding balance {balance:0.##}; the customer will have a credit.";
            if (!request.IsOffline && balance <= 0) throw new InvalidOperationException("Customer has no outstanding balance.");
            warnings.Add(msg);
        }

        var paidAt = request.PaidAt ?? now;
        if (paidAt > now.AddMinutes(10)) paidAt = now;

        var number = string.IsNullOrWhiteSpace(request.Number)
            ? $"RCV-{paidAt:yyMMdd}-{Random.Shared.Next(0, 1_000_000):000000}"
            : request.Number.Trim();

        var payment = new CustomerPayment
        {
            Id = request.Id ?? Guid.NewGuid(),
            TenantId = currentTenant.TenantId.Value,
            Number = number,
            CustomerId = request.CustomerId,
            LocationId = request.LocationId,
            Amount = Math.Round(request.Amount, 2),
            PaymentMethod = PaymentMethods.Normalize(request.PaymentMethod),
            PaidAt = paidAt,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            DeviceId = request.DeviceId,
            CreatedAt = now
        };
        db.CustomerPayments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);
        return new CreateCustomerPaymentResult(payment.Id, payment.Number, balance - payment.Amount, false, ErrorMessages.LocalizeAll(warnings));
    }
}

public sealed record CustomerStatementLineDto(
    DateTimeOffset Date,
    string Type,
    string Reference,
    Guid ReferenceId,
    decimal Debit,
    decimal Credit,
    decimal Balance,
    string? Notes);

public sealed record CustomerStatementDto(
    Guid CustomerId,
    string Code,
    string NameEn,
    string NameAr,
    string? Phone,
    decimal CreditLimit,
    decimal Balance,
    IReadOnlyList<CustomerStatementLineDto> Lines);

/// <summary>كشف حساب العميل — every credit sale, return credit and payment with a running balance.</summary>
public sealed record GetCustomerStatementQuery(Guid CustomerId) : IRequest<CustomerStatementDto>;

public sealed class GetCustomerStatementQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetCustomerStatementQuery, CustomerStatementDto>
{
    public async Task<CustomerStatementDto> Handle(GetCustomerStatementQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken)
            ?? throw new InvalidOperationException("Customer not found.");

        var sales = await db.Sales.AsNoTracking()
            .Where(x => x.CustomerId == customer.Id && x.DueAmount != 0)
            .Select(x => new { x.SoldAt, x.Number, x.Id, x.DueAmount, x.TotalAmount, x.PaidAmount })
            .ToListAsync(cancellationToken);

        var payments = await db.CustomerPayments.AsNoTracking()
            .Where(x => x.CustomerId == customer.Id)
            .Select(x => new { x.PaidAt, x.Number, x.Id, x.Amount, x.PaymentMethod, x.Notes })
            .ToListAsync(cancellationToken);

        var credits = await (
            from r in db.SaleReturns.AsNoTracking()
            join s in db.Sales.AsNoTracking() on r.SaleId equals s.Id
            where s.CustomerId == customer.Id && r.CreditAmount != 0
            select new { r.ReturnedAt, r.Number, r.Id, r.CreditAmount, SaleNumber = s.Number }
        ).ToListAsync(cancellationToken);

        var raw = sales.Select(x => (x.SoldAt, "Sale", x.Number, x.Id, x.DueAmount, 0m,
                    (string?)$"Total {x.TotalAmount:0.##}, paid {x.PaidAmount:0.##}"))
            .Concat(payments.Select(x => (x.PaidAt, "Payment", x.Number, x.Id, 0m, x.Amount, (string?)(x.PaymentMethod + (x.Notes is null ? "" : " - " + x.Notes)))))
            .Concat(credits.Select(x => (x.ReturnedAt, "Return", x.Number, x.Id, 0m, x.CreditAmount, (string?)$"Sale {x.SaleNumber}")))
            .OrderBy(x => x.Item1)
            .ToList();

        decimal running = 0;
        var lines = new List<CustomerStatementLineDto>(raw.Count);
        foreach (var (date, type, reference, id, debit, credit, notes) in raw)
        {
            running += debit - credit;
            lines.Add(new CustomerStatementLineDto(date, type, reference, id, debit, credit, running, notes));
        }

        return new CustomerStatementDto(customer.Id, customer.Code, customer.NameEn, customer.NameAr, customer.Phone,
            customer.CreditLimit, running, lines);
    }
}

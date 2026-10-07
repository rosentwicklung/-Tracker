namespace BudgetTracker.Models;

public record Transaction(
    Guid Id,
    DateTimeOffset Timestamp,
    TransactionType Type,
    string Description,
    decimal Amount
);

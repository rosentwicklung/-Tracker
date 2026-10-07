namespace BudgetTracker.Services;

using BudgetTracker.Events;
using BudgetTracker.Models;

public class TransactionService
{
    private readonly IStorageService _storageService;

    public event EventHandler<TransactionAddedEventArgs>? TransactionAdded;

    public TransactionService(IStorageService storageService)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
    }

    public Transaction AddTransaction(TransactionType type, string description, decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description cannot be empty or whitespace.", nameof(description));
        }

        var transaction = new Transaction(
            Guid.NewGuid(),
            DateTimeOffset.Now,
            type,
            description.Trim(),
            amount
        );

        _storageService.SaveTransaction(transaction);
        OnTransactionAdded(transaction);

        return transaction;
    }

    public bool RemoveTransaction(Guid id)
    {
        return _storageService.RemoveTransaction(id);
    }

    public IEnumerable<Transaction> GetTransactions(DateOnly from, DateOnly to, TransactionType? filterType = null)
    {
        var transactions = _storageService.LoadTransactions(from, to);

        if (filterType.HasValue)
        {
            transactions = transactions.Where(t => t.Type == filterType.Value);
        }

        return transactions;
    }

    protected virtual void OnTransactionAdded(Transaction transaction)
    {
        TransactionAdded?.Invoke(this, new TransactionAddedEventArgs(transaction));
    }
}

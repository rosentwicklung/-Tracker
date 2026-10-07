namespace BudgetTracker.Events;

using BudgetTracker.Models;

public class TransactionAddedEventArgs : EventArgs
{
    public Transaction Transaction { get; }

    public TransactionAddedEventArgs(Transaction transaction)
    {
        Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
    }
}

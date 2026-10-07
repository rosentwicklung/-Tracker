namespace BudgetTracker.Services;

using BudgetTracker.Models;

public interface IStorageService
{
    void SaveTransaction(Transaction transaction);
    IEnumerable<Transaction> LoadTransactions(DateOnly from, DateOnly to);
    bool RemoveTransaction(Guid id);
}

namespace BudgetTracker.Services;

using BudgetTracker.Events;

public class LoggerService
{
    private readonly string _logFilePath;

    public LoggerService(TransactionService transactionService, string logFilePath = "logs/transactions.log")
    {
        ArgumentNullException.ThrowIfNull(transactionService);
        _logFilePath = logFilePath;

        string? directory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        transactionService.TransactionAdded += OnTransactionAdded;
    }

    private void OnTransactionAdded(object? sender, TransactionAddedEventArgs e)
    {
        var t = e.Transaction;
        string logLine = $"{t.Timestamp:yyyy-MM-dd HH:mm:ss zzz} | {t.Type,-7} | Amount: {t.Amount,10:F2} | Id: {t.Id} | {t.Description}{Environment.NewLine}";

        try
        {
            File.AppendAllText(_logFilePath, logLine);
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"[Warning] Failed to append to log file '{_logFilePath}': {ex.Message}");
        }
    }
}

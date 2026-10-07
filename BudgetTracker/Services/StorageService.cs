namespace BudgetTracker.Services;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using BudgetTracker.Models;

public class StorageService : IStorageService
{
    private readonly string _dataDirectory;
    private readonly JsonSerializerOptions _jsonOptions;

    public StorageService(string dataDirectory)
    {
        _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        EnsureDirectoryExists();
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_dataDirectory))
        {
            Directory.CreateDirectory(_dataDirectory);
        }
    }

    public void SaveTransaction(Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        EnsureDirectoryExists();

        string dayFileName = $"{transaction.Timestamp:yyyy-MM-dd}.json";
        string filePath = Path.Combine(_dataDirectory, dayFileName);

        List<Transaction> transactions = new();

        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    transactions = JsonSerializer.Deserialize<List<Transaction>>(json, _jsonOptions) ?? new List<Transaction>();
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException($"Error deserializing file '{dayFileName}': {ex.Message}", ex);
                }
            }
        }

        transactions.Add(transaction);

        string updatedJson = JsonSerializer.Serialize(transactions, _jsonOptions);
        File.WriteAllText(filePath, updatedJson);
    }

    public IEnumerable<Transaction> LoadTransactions(DateOnly from, DateOnly to)
    {
        EnsureDirectoryExists();

        string[] files = Directory.GetFiles(_dataDirectory, "*.json");

        var matchingFiles = files
            .Select(file =>
            {
                string name = Path.GetFileNameWithoutExtension(file);
                bool isValid = DateOnly.TryParseExact(name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly day);
                return new { FilePath = file, Day = day, IsValid = isValid };
            })
            .Where(f => f.IsValid && f.Day >= from && f.Day <= to)
            .OrderBy(f => f.Day)
            .Select(f => f.FilePath)
            .ToList();

        var dayTransactionLists = matchingFiles.Select(file =>
        {
            try
            {
                string json = File.ReadAllText(file);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<Transaction>();
                }
                return JsonSerializer.Deserialize<List<Transaction>>(json, _jsonOptions) ?? new List<Transaction>();
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Error reading transaction file '{Path.GetFileName(file)}': Corrupted JSON.", ex);
            }
        });

        // Flatten day transaction lists using SelectMany as specified in FR006
        return dayTransactionLists.SelectMany(txList => txList);
    }

    public bool RemoveTransaction(Guid id)
    {
        EnsureDirectoryExists();

        string[] files = Directory.GetFiles(_dataDirectory, "*.json");

        foreach (string file in files)
        {
            List<Transaction> transactions;
            try
            {
                string json = File.ReadAllText(file);
                if (string.IsNullOrWhiteSpace(json))
                {
                    continue;
                }
                transactions = JsonSerializer.Deserialize<List<Transaction>>(json, _jsonOptions) ?? new List<Transaction>();
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Error reading transaction file '{Path.GetFileName(file)}': Corrupted JSON.", ex);
            }

            int index = transactions.FindIndex(t => t.Id == id);
            if (index >= 0)
            {
                transactions.RemoveAt(index);
                string updatedJson = JsonSerializer.Serialize(transactions, _jsonOptions);
                File.WriteAllText(file, updatedJson);
                return true;
            }
        }

        return false;
    }
}

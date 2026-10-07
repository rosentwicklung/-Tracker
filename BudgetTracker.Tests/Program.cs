using System.Globalization;
using BudgetTracker.Events;
using BudgetTracker.Models;
using BudgetTracker.Services;

Console.WriteLine("========================================");
Console.WriteLine("        RUNNING BUDGETTRACKER TESTS     ");
Console.WriteLine("========================================");

int passed = 0;
int failed = 0;

void RunTest(string testName, Action testAction)
{
    try
    {
        testAction();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[PASS] {testName}");
        Console.ResetColor();
        passed++;
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[FAIL] {testName}: {ex.Message}");
        Console.ResetColor();
        failed++;
    }
}

// --------------------------------------------------------------------------
// Unit Tests: TransactionService
// --------------------------------------------------------------------------
RunTest("TransactionService: Adding raises TransactionAdded once with the right transaction", () =>
{
    var fakeStorage = new FakeStorageService();
    var service = new TransactionService(fakeStorage);

    int eventCount = 0;
    Transaction? capturedTransaction = null;
    service.TransactionAdded += (sender, args) =>
    {
        eventCount++;
        capturedTransaction = args.Transaction;
    };

    var added = service.AddTransaction(TransactionType.Income, "Bonus", 500m);

    if (eventCount != 1)
        throw new Exception($"Expected TransactionAdded to be raised 1 time, but got {eventCount}");
    if (capturedTransaction == null || capturedTransaction.Id != added.Id)
        throw new Exception("Captured transaction did not match the returned transaction");
    if (capturedTransaction.Description != "Bonus" || capturedTransaction.Amount != 500m || capturedTransaction.Type != TransactionType.Income)
        throw new Exception("Transaction fields were not set properly");
    if (fakeStorage.SavedTransactions.Count != 1)
        throw new Exception("StorageService.SaveTransaction was not called");
});

RunTest("TransactionService: Amount of 0 or less is rejected (ArgumentOutOfRangeException)", () =>
{
    var fakeStorage = new FakeStorageService();
    var service = new TransactionService(fakeStorage);

    bool threwOnZero = false;
    try
    {
        service.AddTransaction(TransactionType.Income, "Free", 0m);
    }
    catch (ArgumentOutOfRangeException)
    {
        threwOnZero = true;
    }

    if (!threwOnZero)
        throw new Exception("Expected ArgumentOutOfRangeException for amount = 0");

    bool threwOnNegative = false;
    try
    {
        service.AddTransaction(TransactionType.Expense, "Loss", -50m);
    }
    catch (ArgumentOutOfRangeException)
    {
        threwOnNegative = true;
    }

    if (!threwOnNegative)
        throw new Exception("Expected ArgumentOutOfRangeException for amount < 0");
});

RunTest("TransactionService: Removing an unknown Id returns false", () =>
{
    var fakeStorage = new FakeStorageService();
    var service = new TransactionService(fakeStorage);

    bool result = service.RemoveTransaction(Guid.NewGuid());
    if (result)
        throw new Exception("Expected RemoveTransaction to return false for unknown Id");
});

RunTest("TransactionService: Removing an existing Id returns true", () =>
{
    var fakeStorage = new FakeStorageService();
    var service = new TransactionService(fakeStorage);

    var t = service.AddTransaction(TransactionType.Income, "Freelance", 200m);
    bool removed = service.RemoveTransaction(t.Id);

    if (!removed)
        throw new Exception("Expected RemoveTransaction to return true for existing Id");
    if (fakeStorage.SavedTransactions.Count != 0)
        throw new Exception("Expected transaction to be removed from storage");
});

RunTest("TransactionService: GetTransactions filters by TransactionType correctly", () =>
{
    var fakeStorage = new FakeStorageService();
    var service = new TransactionService(fakeStorage);

    service.AddTransaction(TransactionType.Income, "Salary", 3000m);
    service.AddTransaction(TransactionType.Expense, "Rent", 1000m);
    service.AddTransaction(TransactionType.Expense, "Utilities", 200m);

    var today = DateOnly.FromDateTime(DateTime.Today);
    var incomes = service.GetTransactions(today.AddDays(-1), today.AddDays(1), TransactionType.Income).ToList();
    var expenses = service.GetTransactions(today.AddDays(-1), today.AddDays(1), TransactionType.Expense).ToList();

    if (incomes.Count != 1 || incomes[0].Description != "Salary")
        throw new Exception("Income filter failed");
    if (expenses.Count != 2)
        throw new Exception("Expense filter failed");
});

// --------------------------------------------------------------------------
// Integration / Storage Tests: StorageService against temporary folder
// --------------------------------------------------------------------------
RunTest("StorageService: SaveTransaction stores JSON array in daily file", () =>
{
    string tempDir = Path.Combine(Path.GetTempPath(), "budget_test_" + Guid.NewGuid().ToString("N"));
    try
    {
        var storage = new StorageService(tempDir);
        var now = DateTimeOffset.Now;
        var t = new Transaction(Guid.NewGuid(), now, TransactionType.Expense, "Coffee", 4.50m);

        storage.SaveTransaction(t);

        string expectedFile = Path.Combine(tempDir, $"{now:yyyy-MM-dd}.json");
        if (!File.Exists(expectedFile))
            throw new Exception($"Expected day file '{expectedFile}' was not created");

        string content = File.ReadAllText(expectedFile);
        if (!content.Contains("\"Type\": \"Expense\""))
            throw new Exception("TransactionType enum was not serialized as string text");
        if (!content.Contains("Coffee") || !content.Contains("4.5"))
            throw new Exception("Transaction content missing in JSON file");
    }
    finally
    {
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }
});

RunTest("StorageService: Multiple transactions on the same day append to the array", () =>
{
    string tempDir = Path.Combine(Path.GetTempPath(), "budget_test_" + Guid.NewGuid().ToString("N"));
    try
    {
        var storage = new StorageService(tempDir);
        var now = DateTimeOffset.Now;
        var t1 = new Transaction(Guid.NewGuid(), now, TransactionType.Income, "Gift", 50m);
        var t2 = new Transaction(Guid.NewGuid(), now, TransactionType.Expense, "Dinner", 35m);

        storage.SaveTransaction(t1);
        storage.SaveTransaction(t2);

        var loaded = storage.LoadTransactions(DateOnly.FromDateTime(now.DateTime), DateOnly.FromDateTime(now.DateTime)).ToList();
        if (loaded.Count != 2)
            throw new Exception($"Expected 2 transactions, got {loaded.Count}");
        if (loaded[0].Description != "Gift" || loaded[1].Description != "Dinner")
            throw new Exception("Stored transactions do not match original inputs");
    }
    finally
    {
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }
});

RunTest("StorageService: LoadTransactions filters day files outside range", () =>
{
    string tempDir = Path.Combine(Path.GetTempPath(), "budget_test_" + Guid.NewGuid().ToString("N"));
    try
    {
        var storage = new StorageService(tempDir);

        var tJan = new Transaction(Guid.NewGuid(), new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero), TransactionType.Income, "Jan Income", 100m);
        var tFeb = new Transaction(Guid.NewGuid(), new DateTimeOffset(2026, 2, 15, 12, 0, 0, TimeSpan.Zero), TransactionType.Income, "Feb Income", 200m);
        var tMar = new Transaction(Guid.NewGuid(), new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero), TransactionType.Income, "Mar Income", 300m);

        storage.SaveTransaction(tJan);
        storage.SaveTransaction(tFeb);
        storage.SaveTransaction(tMar);

        var febOnly = storage.LoadTransactions(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)).ToList();
        if (febOnly.Count != 1 || febOnly[0].Description != "Feb Income")
            throw new Exception($"Expected 1 transaction in Feb, found {febOnly.Count}");

        var janToMar = storage.LoadTransactions(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31)).ToList();
        if (janToMar.Count != 3)
            throw new Exception($"Expected 3 transactions, found {janToMar.Count}");
    }
    finally
    {
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }
});

RunTest("StorageService: RemoveTransaction removes item and rewrites file", () =>
{
    string tempDir = Path.Combine(Path.GetTempPath(), "budget_test_" + Guid.NewGuid().ToString("N"));
    try
    {
        var storage = new StorageService(tempDir);
        var now = DateTimeOffset.Now;
        var t1 = new Transaction(Guid.NewGuid(), now, TransactionType.Income, "First", 10m);
        var t2 = new Transaction(Guid.NewGuid(), now, TransactionType.Income, "Second", 20m);

        storage.SaveTransaction(t1);
        storage.SaveTransaction(t2);

        bool removed = storage.RemoveTransaction(t1.Id);
        if (!removed)
            throw new Exception("RemoveTransaction should return true for existing Id");

        var remaining = storage.LoadTransactions(DateOnly.FromDateTime(now.DateTime), DateOnly.FromDateTime(now.DateTime)).ToList();
        if (remaining.Count != 1 || remaining[0].Id != t2.Id)
            throw new Exception("Expected only t2 to remain in storage");
    }
    finally
    {
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }
});

RunTest("LoggerService: Subscribes to TransactionAdded and writes log entries", () =>
{
    string tempDir = Path.Combine(Path.GetTempPath(), "budget_log_test_" + Guid.NewGuid().ToString("N"));
    string logFile = Path.Combine(tempDir, "transactions.log");
    try
    {
        var fakeStorage = new FakeStorageService();
        var service = new TransactionService(fakeStorage);
        var logger = new LoggerService(service, logFile);

        var t = service.AddTransaction(TransactionType.Expense, "Book", 19.99m);

        if (!File.Exists(logFile))
            throw new Exception("Log file was not created by LoggerService");

        string logContent = File.ReadAllText(logFile);
        if (!logContent.Contains("Book") || !logContent.Contains("Expense") || !logContent.Contains(t.Id.ToString()))
            throw new Exception("Log entry does not contain expected transaction details");
    }
    finally
    {
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }
});

Console.WriteLine();
Console.WriteLine($"Result: {passed} passed, {failed} failed.");

if (failed > 0)
{
    Environment.Exit(1);
}

// --------------------------------------------------------------------------
// Fake Storage Service for unit testing TransactionService in isolation
// --------------------------------------------------------------------------
class FakeStorageService : IStorageService
{
    public List<Transaction> SavedTransactions { get; } = new();

    public void SaveTransaction(Transaction transaction)
    {
        SavedTransactions.Add(transaction);
    }

    public IEnumerable<Transaction> LoadTransactions(DateOnly from, DateOnly to)
    {
        return SavedTransactions.Where(t =>
        {
            var day = DateOnly.FromDateTime(t.Timestamp.DateTime);
            return day >= from && day <= to;
        });
    }

    public bool RemoveTransaction(Guid id)
    {
        int index = SavedTransactions.FindIndex(t => t.Id == id);
        if (index >= 0)
        {
            SavedTransactions.RemoveAt(index);
            return true;
        }
        return false;
    }
}

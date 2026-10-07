using System.Globalization;
using BudgetTracker.Models;
using BudgetTracker.Services;

// FR009: Directory Bootstrap
const string dataDirectory = "data";
const string logsDirectory = "logs";

try
{
    Directory.CreateDirectory(dataDirectory);
    Directory.CreateDirectory(logsDirectory);
}
catch (Exception ex)
{
    Console.WriteLine($"[Fatal Error] Could not create required application directories: {ex.Message}");
    return;
}

// Services initialization
var storageService = new StorageService(dataDirectory);
var transactionService = new TransactionService(storageService);
var loggerService = new LoggerService(transactionService, Path.Combine(logsDirectory, "transactions.log"));

Console.WriteLine("========================================");
Console.WriteLine("       WELCOME TO BUDGET TRACKER        ");
Console.WriteLine("========================================");

// FR002: Main Menu Loop
bool running = true;
while (running)
{
    Console.WriteLine();
    Console.WriteLine("--- MAIN MENU ---");
    Console.WriteLine("1. Add Transaction");
    Console.WriteLine("2. Remove Transaction");
    Console.WriteLine("3. Generate Report");
    Console.WriteLine("4. Exit");
    Console.Write("Please select an option (1-4): ");

    string? choice = Console.ReadLine()?.Trim();
    Console.WriteLine();

    // FR007: Top-level error handling to prevent application crashes
    try
    {
        switch (choice)
        {
            case "1":
                AddTransactionWorkflow(transactionService);
                break;
            case "2":
                RemoveTransactionWorkflow(transactionService);
                break;
            case "3":
                GenerateReportWorkflow(transactionService);
                break;
            case "4":
                running = false;
                Console.WriteLine("Thank you for using Budget Tracker. Goodbye!");
                break;
            default:
                Console.WriteLine("Invalid selection. Please choose an option from 1 to 4.");
                break;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] An unexpected error occurred: {ex.Message}");
    }
}

// FR003: Add Transaction Workflow
static void AddTransactionWorkflow(TransactionService transactionService)
{
    Console.WriteLine("--- ADD TRANSACTION ---");

    // Prompt for Transaction Type
    TransactionType type;
    while (true)
    {
        Console.Write("Enter transaction type (Income / Expense): ");
        string? input = Console.ReadLine()?.Trim();

        if (Enum.TryParse<TransactionType>(input, ignoreCase: true, out type))
        {
            break;
        }
        Console.WriteLine("Invalid type. Please enter 'Income' or 'Expense'.");
    }

    // Prompt for Description
    string description;
    while (true)
    {
        Console.Write("Enter description: ");
        string? input = Console.ReadLine()?.Trim();

        if (!string.IsNullOrWhiteSpace(input))
        {
            description = input;
            break;
        }
        Console.WriteLine("Description cannot be empty. Please enter a valid description.");
    }

    // Prompt for Amount
    decimal amount;
    while (true)
    {
        Console.Write("Enter amount (positive number): ");
        string? input = Console.ReadLine()?.Trim();

        if ((decimal.TryParse(input, CultureInfo.InvariantCulture, out amount) ||
             decimal.TryParse(input, CultureInfo.CurrentCulture, out amount)) &&
            amount > 0)
        {
            break;
        }
        Console.WriteLine("Invalid amount. Amount must be a positive number greater than 0.");
    }

    try
    {
        var transaction = transactionService.AddTransaction(type, description, amount);

        Console.WriteLine();
        Console.WriteLine("✓ Transaction added successfully!");
        Console.WriteLine($"  Id:          {transaction.Id}");
        Console.WriteLine($"  Timestamp:   {transaction.Timestamp:yyyy-MM-dd HH:mm:ss zzz}");
        Console.WriteLine($"  Type:        {transaction.Type}");
        Console.WriteLine($"  Description: {transaction.Description}");
        Console.WriteLine($"  Amount:      {transaction.Amount:F2}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] Failed to add transaction: {ex.Message}");
    }
}

// FR005: Remove Transaction Workflow
static void RemoveTransactionWorkflow(TransactionService transactionService)
{
    Console.WriteLine("--- REMOVE TRANSACTION ---");
    Console.Write("Enter transaction Id (GUID): ");
    string? idInput = Console.ReadLine()?.Trim();

    if (!Guid.TryParse(idInput, out Guid id))
    {
        Console.WriteLine("Invalid GUID format. Example: 550e8400-e29b-41d4-a716-446655440000");
        return;
    }

    try
    {
        bool removed = transactionService.RemoveTransaction(id);
        if (removed)
        {
            Console.WriteLine($"\n✓ Transaction with Id '{id}' was successfully removed.");
        }
        else
        {
            Console.WriteLine($"\n✗ No transaction found with Id '{id}'.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] Failed to remove transaction: {ex.Message}");
    }
}

// FR006: Report Generation Workflow
static void GenerateReportWorkflow(TransactionService transactionService)
{
    Console.WriteLine("--- GENERATE REPORT ---");

    DateOnly fromDate;
    while (true)
    {
        Console.Write("Enter start date (yyyy-MM-dd): ");
        string? input = Console.ReadLine()?.Trim();

        if (DateOnly.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fromDate))
        {
            break;
        }
        Console.WriteLine("Invalid date format. Expected 'yyyy-MM-dd' (e.g. 2026-03-01).");
    }

    DateOnly toDate;
    while (true)
    {
        Console.Write("Enter end date (yyyy-MM-dd): ");
        string? input = Console.ReadLine()?.Trim();

        if (DateOnly.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out toDate))
        {
            if (toDate >= fromDate)
            {
                break;
            }
            Console.WriteLine("End date cannot be earlier than start date.");
        }
        else
        {
            Console.WriteLine("Invalid date format. Expected 'yyyy-MM-dd' (e.g. 2026-03-31).");
        }
    }

    Console.WriteLine("\nFilter by transaction type:");
    Console.WriteLine("  1. All");
    Console.WriteLine("  2. Income only");
    Console.WriteLine("  3. Expense only");
    Console.Write("Select filter [1-3, default: 1]: ");
    string? filterInput = Console.ReadLine()?.Trim();

    TransactionType? filterType = filterInput switch
    {
        "2" => TransactionType.Income,
        "3" => TransactionType.Expense,
        _ => null
    };

    try
    {
        var allTransactions = transactionService.GetTransactions(fromDate, toDate, null).ToList();

        if (allTransactions.Count == 0)
        {
            Console.WriteLine($"\nNo transactions found between {fromDate:yyyy-MM-dd} and {toDate:yyyy-MM-dd}.");
            return;
        }

        var displayedTransactions = filterType.HasValue
            ? allTransactions.Where(t => t.Type == filterType.Value).ToList()
            : allTransactions;

        Console.WriteLine();
        Console.WriteLine($"=== Transactions Report ({fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd}) ===");
        if (filterType.HasValue)
        {
            Console.WriteLine($"Filter applied: {filterType.Value} only");
        }
        Console.WriteLine(new string('-', 100));
        Console.WriteLine($"{"Timestamp",-25} | {"Type",-8} | {"Amount",12} | {"Description",-25} | {"Id",-36}");
        Console.WriteLine(new string('-', 100));

        foreach (var t in displayedTransactions)
        {
            string formattedTimestamp = t.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
            Console.WriteLine($"{formattedTimestamp,-25} | {t.Type,-8} | {t.Amount,12:F2} | {t.Description,-25} | {t.Id}");
        }

        if (displayedTransactions.Count == 0)
        {
            Console.WriteLine("No transactions matched the selected filter.");
        }

        Console.WriteLine(new string('-', 100));

        // FR006: Totals calculated with Where and Sum
        decimal totalIncome = allTransactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
        decimal totalExpenses = allTransactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
        decimal balance = totalIncome - totalExpenses;

        Console.WriteLine($"Total Income:   {totalIncome,14:F2}");
        Console.WriteLine($"Total Expenses: {totalExpenses,14:F2}");
        Console.WriteLine($"Net Balance:    {balance,14:F2}");
        Console.WriteLine(new string('=', 100));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] Failed to generate report: {ex.Message}");
    }
}

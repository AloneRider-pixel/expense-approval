using ExpenseApproval.Api.Data;
using ExpenseApproval.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseApproval.Tests;

/// <summary>
/// Two managers load the same pending expense and both try to decide it.
/// The first save wins; the second must fail with a concurrency exception
/// instead of silently overwriting. Status is configured as a concurrency
/// token in ExpensesDbContext.
/// </summary>
public class ConcurrencyTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"concurrency-{Guid.NewGuid()}.db");

    private ExpensesDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new ExpensesDbContext(options);
    }

    [Fact]
    public async Task Second_concurrent_decision_fails_instead_of_overwriting()
    {
        int expenseId;
        using (var setup = NewContext())
        {
            setup.Database.EnsureCreated();
            var expense = new Expense
            {
                Title = "Laptop", Category = "Equipment", Amount = 60000m,
                SubmittedBy = "Himanshu", CreatedAt = DateTime.UtcNow,
                Status = ExpenseStatus.Pending,
            };
            setup.Expenses.Add(expense);
            await setup.SaveChangesAsync();
            expenseId = expense.Id;
        }

        // Both contexts load the expense while it is still Pending.
        using var first = NewContext();
        using var second = NewContext();
        var firstExpense = await first.Expenses.FindAsync(expenseId);
        var secondExpense = await second.Expenses.FindAsync(expenseId);

        firstExpense!.Status = ExpenseStatus.Approved;
        await first.SaveChangesAsync(); // manager 1 wins

        secondExpense!.Status = ExpenseStatus.Rejected;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        // The stored value is the winner's decision, not the loser's.
        using var check = NewContext();
        Assert.Equal(ExpenseStatus.Approved, (await check.Expenses.FindAsync(expenseId))!.Status);
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { /* best effort */ }
    }
}

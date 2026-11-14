using ExpenseApproval.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExpenseApproval.Api.Data;

public class ExpensesDbContext : DbContext
{
    public ExpensesDbContext(DbContextOptions<ExpensesDbContext> options) : base(options) { }

    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<DecisionRecord> DecisionRecords => Set<DecisionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Expense>()
            .HasMany(e => e.History)
            .WithOne()
            .HasForeignKey(d => d.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Expense>().Property(e => e.Amount).HasPrecision(18, 2);

        // Optimistic concurrency: EF includes the original Status in the UPDATE's
        // WHERE clause, so a second decision on the same expense fails instead
        // of overwriting the first.
        modelBuilder.Entity<Expense>().Property(e => e.Status).IsConcurrencyToken();
    }
}

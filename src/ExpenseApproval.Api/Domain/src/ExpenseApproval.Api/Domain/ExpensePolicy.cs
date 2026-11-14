namespace ExpenseApproval.Api.Domain;

/// <summary>
/// The business rules, kept out of the controller so they can be
/// unit-tested without a web server or database.
/// </summary>
public class ExpensePolicy
{
    /// <summary>Expenses above this amount need a manager's decision.</summary>
    public decimal ApprovalThreshold { get; }

    public ExpensePolicy(decimal approvalThreshold)
    {
        if (approvalThreshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(approvalThreshold));
        ApprovalThreshold = approvalThreshold;
    }

    public bool RequiresManagerApproval(decimal amount) => amount > ApprovalThreshold;

    /// <summary>Validates a new expense. Returns an error string, or null when valid.</summary>
    public string? ValidateNewExpense(string title, string category, decimal amount, string submittedBy)
    {
        if (string.IsNullOrWhiteSpace(title)) return "Title is required.";
        if (string.IsNullOrWhiteSpace(category)) return "Category is required.";
        if (amount <= 0) return "Amount must be greater than zero.";
        if (string.IsNullOrWhiteSpace(submittedBy)) return "SubmittedBy is required.";
        return null;
    }

    /// <summary>Validates a manager's approve/reject decision.</summary>
    public string? ValidateDecision(Expense expense, bool approve, string manager, string? comment)
    {
        if (expense.Status != ExpenseStatus.Pending)
            return $"Expense is already {expense.Status.ToString().ToLowerInvariant()} and cannot be decided again.";
        if (string.IsNullOrWhiteSpace(manager))
            return "Manager name is required.";
        if (!approve && string.IsNullOrWhiteSpace(comment))
            return "A comment is required when rejecting an expense.";
        return null;
    }
}

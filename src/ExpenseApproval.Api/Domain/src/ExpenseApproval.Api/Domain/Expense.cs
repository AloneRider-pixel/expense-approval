namespace ExpenseApproval.Api.Domain;

public enum ExpenseStatus
{
    Pending,
    Approved,
    Rejected
}

public enum DecisionAction
{
    Submitted,
    AutoApproved,
    Approved,
    Rejected
}

/// <summary>
/// One row in an expense's decision history (audit trail).
/// Every state change on an expense appends one of these.
/// </summary>
public class DecisionRecord
{
    public int Id { get; set; }
    public int ExpenseId { get; set; }
    public DecisionAction Action { get; set; }
    public string Actor { get; set; } = "";
    public string? Comment { get; set; }
    public DateTime Timestamp { get; set; }
}

public class Expense
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Amount { get; set; }
    public string SubmittedBy { get; set; } = "";
    public ExpenseStatus Status { get; set; } = ExpenseStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public List<DecisionRecord> History { get; set; } = new();
}

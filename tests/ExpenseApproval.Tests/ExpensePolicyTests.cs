using ExpenseApproval.Api.Domain;

namespace ExpenseApproval.Tests;

public class ExpensePolicyTests
{
    private readonly ExpensePolicy _policy = new(approvalThreshold: 5000m);

    [Fact]
    public void Amount_at_threshold_does_not_need_manager()
        => Assert.False(_policy.RequiresManagerApproval(5000m));

    [Fact]
    public void Amount_above_threshold_needs_manager()
        => Assert.True(_policy.RequiresManagerApproval(5000.01m));

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void New_expense_with_non_positive_amount_is_rejected(decimal amount)
        => Assert.Equal("Amount must be greater than zero.",
            _policy.ValidateNewExpense("Taxi", "Travel", amount, "himanshu"));

    [Fact]
    public void New_expense_without_title_is_rejected()
        => Assert.Equal("Title is required.",
            _policy.ValidateNewExpense(" ", "Travel", 100m, "himanshu"));

    [Fact]
    public void Valid_new_expense_passes()
        => Assert.Null(_policy.ValidateNewExpense("Taxi", "Travel", 100m, "himanshu"));

    [Fact]
    public void Rejection_without_comment_is_rejected()
    {
        var expense = new Expense { Status = ExpenseStatus.Pending };
        Assert.Equal("A comment is required when rejecting an expense.",
            _policy.ValidateDecision(expense, approve: false, manager: "priya", comment: ""));
    }

    [Fact]
    public void Already_decided_expense_cannot_be_decided_again()
    {
        var expense = new Expense { Status = ExpenseStatus.Approved };
        Assert.Contains("already approved",
            _policy.ValidateDecision(expense, approve: false, manager: "priya", comment: "changed my mind"));
    }

    [Fact]
    public void Pending_expense_can_be_approved()
    {
        var expense = new Expense { Status = ExpenseStatus.Pending };
        Assert.Null(_policy.ValidateDecision(expense, approve: true, manager: "priya", comment: null));
    }
}

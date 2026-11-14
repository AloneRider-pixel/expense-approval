using ExpenseApproval.Api.Data;
using ExpenseApproval.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseApproval.Api.Controllers;

public record CreateExpenseRequest(string Title, string Category, decimal Amount);
public record DecideExpenseRequest(bool Approve, string? Comment);

[ApiController]
[Route("api/[controller]")]
public class ExpensesController : ControllerBase
{
    private readonly ExpensesDbContext _db;
    private readonly ExpensePolicy _policy;
    private readonly DemoUserContext _users;

    public ExpensesController(ExpensesDbContext db, ExpensePolicy policy, DemoUserContext users)
    {
        _db = db;
        _policy = policy;
        _users = users;
    }

    private DemoUser? CurrentUser =>
        _users.Resolve(Request.Headers[DemoUserContext.HeaderName].FirstOrDefault());

    // GET /api/expenses - list all expenses, newest first, with their decision history.
    [HttpGet]
    public async Task<ActionResult<List<Expense>>> GetAll()
    {
        var expenses = await _db.Expenses
            .Include(e => e.History.OrderBy(h => h.Timestamp))
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
        return Ok(expenses);
    }

    // GET /api/expenses/5 - one expense with history.
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Expense>> GetById(int id)
    {
        var expense = await _db.Expenses.Include(e => e.History).FirstOrDefaultAsync(e => e.Id == id);
        return expense is null ? NotFound() : Ok(expense);
    }

    // POST /api/expenses - submit an expense as the current demo user.
    // The policy decides whether it is auto-approved or waits for a manager.
    [HttpPost]
    public async Task<ActionResult<Expense>> Create(CreateExpenseRequest request)
    {
        var user = CurrentUser;
        if (user is null)
            return Unauthorized(new { error = $"Send a valid {DemoUserContext.HeaderName} header for one of the demo users." });

        var error = _policy.ValidateNewExpense(request.Title, request.Category, request.Amount, user.Name);
        if (error is not null) return BadRequest(new { error });

        var now = DateTime.UtcNow;
        var autoApprove = !_policy.RequiresManagerApproval(request.Amount);

        var expense = new Expense
        {
            Title = request.Title.Trim(),
            Category = request.Category.Trim(),
            Amount = request.Amount,
            SubmittedBy = user.Name,
            CreatedAt = now,
            Status = autoApprove ? ExpenseStatus.Approved : ExpenseStatus.Pending,
        };
        expense.History.Add(new DecisionRecord
        {
            Action = DecisionAction.Submitted,
            Actor = user.Name,
            Timestamp = now,
        });
        if (autoApprove)
        {
            expense.History.Add(new DecisionRecord
            {
                Action = DecisionAction.AutoApproved,
                Actor = "system",
                Comment = $"Amount is at or below the approval threshold of {_policy.ApprovalThreshold}.",
                Timestamp = now,
            });
        }

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = expense.Id }, expense);
    }

    // POST /api/expenses/5/decide - a manager approves or rejects a pending expense.
    // Role enforcement is server-side: only a demo user with the Manager role may decide.
    [HttpPost("{id:int}/decide")]
    public async Task<ActionResult<Expense>> Decide(int id, DecideExpenseRequest request)
    {
        var user = CurrentUser;
        if (user is null)
            return Unauthorized(new { error = $"Send a valid {DemoUserContext.HeaderName} header for one of the demo users." });
        if (user.Role != DemoRole.Manager)
            return StatusCode(403, new { error = $"Only a manager can approve or reject expenses. {user.Name} is an employee." });

        var expense = await _db.Expenses.Include(e => e.History).FirstOrDefaultAsync(e => e.Id == id);
        if (expense is null) return NotFound();

        var error = _policy.ValidateDecision(expense, request.Approve, user.Name, request.Comment);
        if (error is not null) return BadRequest(new { error });

        expense.Status = request.Approve ? ExpenseStatus.Approved : ExpenseStatus.Rejected;
        expense.History.Add(new DecisionRecord
        {
            Action = request.Approve ? DecisionAction.Approved : DecisionAction.Rejected,
            Actor = user.Name,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            Timestamp = DateTime.UtcNow,
        });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another manager decided the same expense first. Status is a
            // concurrency token, so the losing write fails instead of
            // silently overwriting the winner.
            return Conflict(new { error = "This expense was just decided by someone else. Refresh to see the outcome." });
        }

        return Ok(expense);
    }
}

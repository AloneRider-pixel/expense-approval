using System.Net;
using System.Net.Http.Json;
using ExpenseApproval.Api.Data;
using ExpenseApproval.Api.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseApproval.Tests;

/// <summary>
/// End-to-end API tests: real HTTP through the real app, against an
/// in-memory SQLite database so each run is isolated.
/// </summary>
public class ExpensesApiTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly SqliteConnection _connection;
    private readonly HttpClient _client;

    private const string Employee = "Himanshu";
    private const string Manager = "Priya";

    public ExpensesApiTests()
    {
        // One open in-memory SQLite connection shared by every DbContext in this test class.
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<ExpensesDbContext>));
                services.Remove(descriptor);
                services.AddDbContext<ExpensesDbContext>(o => o.UseSqlite(_connection));
            });
        });
        _client = _factory.CreateClient();
    }

    private record CreateExpenseRequest(string Title, string Category, decimal Amount);
    private record DecideExpenseRequest(bool Approve, string? Comment);

    private HttpRequestMessage Post(string url, object body, string? asUser)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        if (asUser is not null) request.Headers.Add(DemoUserContext.HeaderName, asUser);
        return request;
    }

    private async Task<Expense> CreateExpense(string title, decimal amount, string asUser = Employee)
    {
        var response = await _client.SendAsync(Post("/api/expenses",
            new CreateExpenseRequest(title, "Travel", amount), asUser));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Expense>())!;
    }

    [Fact]
    public async Task Small_expense_is_auto_approved_with_history()
    {
        var response = await _client.SendAsync(Post("/api/expenses",
            new CreateExpenseRequest("Team lunch", "Food", 1200m), Employee));
        response.EnsureSuccessStatusCode();

        var expense = await response.Content.ReadFromJsonAsync<Expense>();
        Assert.Equal(ExpenseStatus.Approved, expense!.Status);
        Assert.Equal(Employee, expense.SubmittedBy);
        Assert.Equal(new[] { DecisionAction.Submitted, DecisionAction.AutoApproved },
            expense.History.Select(h => h.Action).ToArray());
    }

    [Fact]
    public async Task Expense_exactly_at_threshold_is_auto_approved()
    {
        var expense = await CreateExpense("Boundary", 5000m);
        Assert.Equal(ExpenseStatus.Approved, expense.Status);
    }

    [Fact]
    public async Task Expense_one_paisa_above_threshold_stays_pending()
    {
        var expense = await CreateExpense("Boundary+1", 5000.01m);
        Assert.Equal(ExpenseStatus.Pending, expense.Status);
    }

    [Fact]
    public async Task Large_expense_stays_pending_until_manager_approves()
    {
        var created = await CreateExpense("Laptop", 60000m);
        Assert.Equal(ExpenseStatus.Pending, created.Status);

        var decided = await (await _client.SendAsync(Post($"/api/expenses/{created.Id}/decide",
            new DecideExpenseRequest(true, null), Manager)))
            .Content.ReadFromJsonAsync<Expense>();

        Assert.Equal(ExpenseStatus.Approved, decided!.Status);
        Assert.Equal(new[] { DecisionAction.Submitted, DecisionAction.Approved },
            decided.History.Select(h => h.Action).ToArray());
        Assert.Equal(Manager, decided.History.Last().Actor);
    }

    [Fact]
    public async Task Employee_cannot_decide_returns_403()
    {
        var created = await CreateExpense("Laptop", 60000m);
        var response = await _client.SendAsync(Post($"/api/expenses/{created.Id}/decide",
            new DecideExpenseRequest(true, null), Employee));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Missing_or_unknown_user_returns_401()
    {
        var noHeader = await _client.SendAsync(Post("/api/expenses",
            new CreateExpenseRequest("Taxi", "Travel", 100m), null));
        Assert.Equal(HttpStatusCode.Unauthorized, noHeader.StatusCode);

        var unknown = await _client.SendAsync(Post("/api/expenses",
            new CreateExpenseRequest("Taxi", "Travel", 100m), "stranger"));
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);

        var created = await CreateExpense("Laptop", 60000m);
        var strangerDecide = await _client.SendAsync(Post($"/api/expenses/{created.Id}/decide",
            new DecideExpenseRequest(true, null), "stranger"));
        Assert.Equal(HttpStatusCode.Unauthorized, strangerDecide.StatusCode);
    }

    [Fact]
    public async Task Invalid_amount_returns_400()
    {
        var response = await _client.SendAsync(Post("/api/expenses",
            new CreateExpenseRequest("Bad", "Misc", -5m), Employee));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rejection_without_comment_returns_400()
    {
        var created = await CreateExpense("Flight", 9000m);
        var response = await _client.SendAsync(Post($"/api/expenses/{created.Id}/decide",
            new DecideExpenseRequest(false, null), Manager));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deciding_twice_returns_400()
    {
        var created = await CreateExpense("Monitor", 15000m);
        await _client.SendAsync(Post($"/api/expenses/{created.Id}/decide",
            new DecideExpenseRequest(true, null), Manager));
        var second = await _client.SendAsync(Post($"/api/expenses/{created.Id}/decide",
            new DecideExpenseRequest(false, "actually no"), Manager));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Unknown_expense_returns_404()
    {
        var response = await _client.SendAsync(Post("/api/expenses/999/decide",
            new DecideExpenseRequest(true, null), Manager));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _connection.Dispose();
    }
}

using ExpenseApproval.Api.Data;
using ExpenseApproval.Api.Domain;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// SQLite file database - zero setup, real SQL, easy to show in an interview.
builder.Services.AddDbContext<ExpensesDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Expenses") ?? "Data Source=expenses.db"));

// The approval threshold is configuration, not a magic number in code.
builder.Services.AddSingleton(new ExpensePolicy(
    builder.Configuration.GetValue<decimal>("ApprovalThreshold", 5000m)));

// Demo identities for the local workflow demo (see README "Limitations").
builder.Services.AddSingleton(new DemoUserContext(
    builder.Configuration.GetSection("DemoUsers").Get<List<DemoUser>>()
    ?? new List<DemoUser>
    {
        new() { Name = "Himanshu", Role = DemoRole.Employee },
        new() { Name = "Priya", Role = DemoRole.Manager },
    }));

builder.Services.AddCors(options =>
    options.AddPolicy("dev", policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// Create the database on startup so the app runs with no migration step.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ExpensesDbContext>().Database.EnsureCreated();
}

app.UseCors("dev");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();

// Lets the test project host this Program with WebApplicationFactory.
public partial class Program { }

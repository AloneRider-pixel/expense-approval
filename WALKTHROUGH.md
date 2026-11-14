# Interview walkthrough - how to explain this project

## The one-minute version

"This is an expense approval system. An employee submits an expense; if it's
above a configurable threshold it goes to a manager for approval, otherwise a
rule auto-approves it. Roles are enforced server-side, every state change is
written to a decision history so each expense has a full audit trail, and
optimistic concurrency stops two managers from overwriting each other. The
backend is an ASP.NET Core Web API with EF Core over SQLite, the frontend is
React, and there are 20 tests covering the rules, the API and the concurrency
case."

## How the pieces fit

- `Domain/Expense.cs` - the entities: `Expense`, `DecisionRecord`, and the
  status/action enums. An expense owns a list of decision records.
- `Domain/ExpensePolicy.cs` - the business rules, deliberately kept out of the
  controller. It takes no dependencies, so the unit tests are plain C# - no
  web server, no database. Open this class first in an interview.
- `Domain/DemoUser.cs` - the demo identity model and role resolution.
- `Data/ExpensesDbContext.cs` - EF Core mapping: one expense has many decision
  records with cascade delete, and `Status` is a concurrency token.
- `Controllers/ExpensesController.cs` - thin HTTP layer: resolve the user,
  enforce the role, validate via the policy, apply the state change, append to
  history, save. Returns 401/403/400/404/409 with clear messages.
- `Program.cs` - dependency injection: the DbContext, the policy as a
  singleton whose threshold comes from config, and the demo user list.
- `client/src/App.jsx` - a single React page: user switcher, summary chips,
  submit form, pending and decided sections with expandable history.

## Questions you'll likely get

**Why is the policy a separate class?**
So the rules are testable in isolation and the controller stays thin. Changing
a rule never touches HTTP code.

**How does the auto-approval rule work?**
On submit, the controller asks the policy whether the amount is above the
threshold. If not, it sets status to Approved and writes both a Submitted and
an AutoApproved record to the history.

**How is authorization handled?**
The UI sends the acting user as a header; the server resolves it against a
configured user list and enforces the role on the decide endpoint - employees
get 403. Be honest: the header is a demo stand-in, not real auth, and the
production answer is ASP.NET Core Identity or JWT with role claims.

**What happens if two managers approve at once?**
`Expense.Status` is an EF Core concurrency token, so the second UPDATE matches
zero rows and the API returns 409. There's a test that proves the losing write
fails and the winner's decision is what stays in the database.

**How do you prevent double-approval?**
`ValidateDecision` rejects any decision on an expense whose status isn't
Pending (400), and the concurrency token covers the simultaneous case (409).

**How would you take this to production?**
Real auth (Identity/JWT), SQL Server or Postgres instead of SQLite, Docker,
CI running `dotnet test`, and deployment behind HTTPS.

## Demo script (2 minutes)

1. Act as Himanshu (Employee). Submit "Team lunch", ₹1,200 - Approved
   instantly (auto-approved).
2. Submit "New laptop", ₹62,000 - it lands in "Waiting for a manager".
3. Show role enforcement. The UI hides Approve/Reject for employees, and the
   server still blocks a direct call - run this and show the 403:

       curl -i -X POST http://localhost:5000/api/expenses/2/decide \
         -H 'Content-Type: application/json' \
         -H 'X-Demo-User: Himanshu' \
         -d '{"approve":true}'

   (Replace 2 with the laptop's id if it differs.)
4. Switch to Priya (Manager). Approve the laptop. Open its History.
5. Submit another large expense as Himanshu, then reject it as Priya with a
   comment, and show the audit trail.
6. Run `dotnet test` - 20 passing tests, including the 5000/5000.01 boundary,
   employee-cannot-decide, and concurrent-decision cases.

## Honest framing

This is a learning project built to demonstrate C#/.NET. Describe it as such -
walk them through the code live rather than claiming production experience.
The demo-user header is a deliberate simplification; say so before they ask.

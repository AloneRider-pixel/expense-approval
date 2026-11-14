# Expense Approval (C# / .NET + React)

An employee submits an expense. Small expenses are auto-approved by a rule;
larger ones wait for a manager to approve or reject. Every action is recorded
in a decision history you can inspect per expense.

Built as a learning project to demonstrate C#/.NET, ASP.NET Core Web API,
Entity Framework Core and React, modeled on approval workflows.

## Tech stack

- **Backend:** ASP.NET Core 10 Web API (controllers) - .NET 10 is the current
  LTS release, supported by Microsoft until November 2028
- **Database:** SQLite via Entity Framework Core (code-first, auto-created on startup)
- **Frontend:** React (Vite)
- **Tests:** xUnit - 20 tests (policy unit tests, API integration tests,
  concurrency integrity test)

## How to run

Prereqs: .NET 10 SDK and Node.js 22+ (any active LTS works).

Backend (terminal 1):

    cd src/ExpenseApproval.Api
    dotnet run

It listens on http://localhost:5000 and creates `expenses.db` automatically.
Swagger UI is at /swagger in Development.

Frontend (terminal 2):

    cd client
    npm install
    npm run dev

Open the URL Vite prints (default http://localhost:5173). If you change the
API port, update the `API` constant at the top of `client/src/App.jsx`.

Run the tests:

    dotnet test

## Demo users and roles

There is no login screen. The app ships two demo identities (in
`appsettings.json` under `DemoUsers`) and the UI has an "Acting as" switcher:

- **Himanshu** - Employee: can submit expenses
- **Priya** - Manager: can submit, approve and reject

The selected user is sent as an `X-Demo-User` header and **enforced on the
server**: an employee calling the decide endpoint gets 403, and a missing or
unknown user gets 401. The submitter's name also comes from the server-side
identity, not from a form field.

## The business rules

- An expense amount must be greater than zero, with a title and category.
- Amounts **above** the approval threshold (₹5,000, set in `appsettings.json`
  as `ApprovalThreshold`) stay **Pending** until a manager decides. Exactly
  ₹5,000 is auto-approved; ₹5,000.01 is not (both covered by tests).
- A rejection **must** include a comment.
- An already-decided expense cannot be decided again (400).
- If two managers decide the same expense at the same time, the first save
  wins and the second gets 409 - `Expense.Status` is an EF Core concurrency
  token, so a losing write can never silently overwrite the winner.

## Limitations (be upfront about these in an interview)

- The `X-Demo-User` header is **not authentication**. It exists to demonstrate
  server-side role enforcement without a login system. A production version
  would use ASP.NET Core Identity or JWT bearer tokens and read the role from
  the authenticated user.
- Anyone running the app can act as any demo user; there are no passwords.
- SQLite is right for a demo. Moving to SQL Server/Postgres for production
  means swapping the EF Core provider plus schema migrations, data-type mapping
  and retesting - real work, not a config tweak.
- No deployment, monitoring, or rate limiting - this is a local demo.

## API

| Method | Route | Role | Purpose |
|---|---|---|---|
| GET | /api/expenses | any | List all expenses with history |
| GET | /api/expenses/{id} | any | One expense with history |
| GET | /api/users | any | List demo users |
| POST | /api/expenses | known user | Submit an expense |
| POST | /api/expenses/{id}/decide | Manager only | Approve/reject a pending expense |

Deep links: `?user=Priya` pre-selects the acting user, `?expand=3` opens
expense 3's history.

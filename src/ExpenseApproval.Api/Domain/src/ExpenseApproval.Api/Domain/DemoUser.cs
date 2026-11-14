namespace ExpenseApproval.Api.Domain;

public enum DemoRole
{
    Employee,
    Manager
}

/// <summary>
/// A demo identity from configuration. This is NOT authentication - it exists
/// so the approval workflow can demonstrate real server-side role enforcement
/// locally without a login system. See README "Limitations".
/// </summary>
public class DemoUser
{
    public string Name { get; set; } = "";
    public DemoRole Role { get; set; }
}

/// <summary>Resolves the X-Demo-User header to a configured demo user.</summary>
public class DemoUserContext
{
    public const string HeaderName = "X-Demo-User";
    private readonly IReadOnlyList<DemoUser> _users;

    public DemoUserContext(IReadOnlyList<DemoUser> users) => _users = users;

    public IReadOnlyList<DemoUser> All => _users;

    public DemoUser? Resolve(string? name)
        => _users.FirstOrDefault(u => string.Equals(u.Name, name, StringComparison.OrdinalIgnoreCase));
}

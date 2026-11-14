using ExpenseApproval.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseApproval.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly DemoUserContext _users;
    public UsersController(DemoUserContext users) => _users = users;

    // GET /api/users - the demo identities the UI can switch between.
    [HttpGet]
    public ActionResult<object> GetAll()
        => Ok(_users.All.Select(u => new { u.Name, Role = u.Role.ToString() }));
}

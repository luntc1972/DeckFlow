using Microsoft.AspNetCore.Mvc;

namespace DeckFlow.Web.Controllers.Admin;

/// <summary>
/// Landing page for /Admin. After BasicAuth (Program.cs:330-332 MapWhen branch), operator
/// hits this controller and is invited to pick a sidebar section. No data dependencies.
/// </summary>
[Route("Admin")]
public sealed class AdminLandingController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}

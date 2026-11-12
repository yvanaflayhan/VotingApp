using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Voting_app.Models;

namespace Voting_app.Controllers;

public class HomeController : Controller
{
    #region Public actions

    public IActionResult Index() =>
        User.Identity?.IsAuthenticated == true
            ? RedirectToAction("Community", "Polls")
            : RedirectToAction("Login", "Account");

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(
            new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier }
        );

    #endregion
}

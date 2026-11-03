using System.Diagnostics;
using System.Reflection.Metadata;
using Microsoft.AspNetCore.Mvc;
using Voting_app.Models;

namespace Voting_app.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if(User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login" , "Account");
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

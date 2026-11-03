using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Voting_app.Models;

namespace Voting_app.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller 
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users.OrderBy(u => u.UserName).ToListAsync();
            var list = new List<UserRoleViewModel>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                list.Add(new UserRoleViewModel
                {
                    Id = u.Id,
                    UserName = u.UserName ?? "",
                    Email = u.Email ?? "",
                    FullName = u.FullName,
                    Role = roles.Contains("Admin") ? "Admin" : "User"
                });
            }
            ViewBag.CurrentUserId = _userManager.GetUserId(User);
            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeRole(string userId, string role)
        {
            if (role != "Admin" && role != "User")
                return BadRequest();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            // Rule 2: an admin cannot change their own role
            if (userId == _userManager.GetUserId(User))
            {
                TempData["Error"] = "You cannot change your own role.";
                return RedirectToAction(nameof(Users));
            }

            // Rule 1: at least one admin must always exist
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (isAdmin && role == "User")
            {
                var admins = await _userManager.GetUsersInRoleAsync("Admin");
                if (admins.Count <= 1)
                {
                    TempData["Error"] = "There must always be at least one admin.";
                    return RedirectToAction(nameof(Users));
                }
            }

            // Remove the old role(s), then add the new one
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, role);

            // Makes the old login cookie invalid so the new role applies
            await _userManager.UpdateSecurityStampAsync(user);

            TempData["Success"] = $"{user.UserName} is now {role}.";
            return RedirectToAction(nameof(Users));
        }
        
    }
}
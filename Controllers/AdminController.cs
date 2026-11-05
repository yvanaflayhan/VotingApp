using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Voting_app.Data;
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

        public IActionResult Users()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> List([FromForm] DataTableRequest request)
        {
            var length = Math.Clamp(request.Length, 1, 100);
            var start = Math.Max(0, request.Start);

            var query = _userManager.Users.AsQueryable();
            var recordsTotal = await query.CountAsync();

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var term = $"%{request.Search.Trim()}%";
                query = query.Where(u =>
                    EF.Functions.ILike(u.UserName!, term) ||
                    EF.Functions.ILike(u.Email!, term) ||
                    EF.Functions.ILike(u.FullName, term));
            }

            var recordsFiltered = await query.CountAsync();

            var users = await query
                .OrderBy(u => u.UserName)
                .ThenBy(u => u.Id)
                .Skip(start)
                .Take(length)
                .ToListAsync();

            var adminIds = (await _userManager.GetUsersInRoleAsync("Admin"))
                .Select(u => u.Id)
                .ToHashSet();
            var currentUserId = _userManager.GetUserId(User);

            var data = users.Select(u => new
            {
                id = u.Id,
                fullName = u.FullName,
                userName = u.UserName,
                email = u.Email,
                role = adminIds.Contains(u.Id) ? "Admin" : "User",
                isMe = u.Id == currentUserId,
                isRoot = IsRootAdmin(u)
            });

            return Json(new
            {
                draw = request.Draw,
                recordsTotal,
                recordsFiltered,
                adminCount = adminIds.Count,
                data
            });
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

            // Rule 1: the main admin can never be changed, by anyone
            if (IsRootAdmin(user))
                return Json(new { success = false, message = "The main admin cannot be changed." });

            // Rule 2: nobody changes their own role
            if (userId == _userManager.GetUserId(User))
                return Json(new { success = false, message = "You cannot change your own role." });

            // Nothing to do if the user already has this role
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if ((role == "Admin") == isAdmin)
                return Json(new { success = false, message = $"{user.UserName} is already {(isAdmin ? "an admin" : "a user")}." });

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, role);

            // Invalidates the old login cookie, so the new role applies on the next request
            await _userManager.UpdateSecurityStampAsync(user);

            return Json(new
            {
                success = true,
                message = role == "Admin"
                    ? $"{user.UserName} is now an admin."
                    : $"{user.UserName} is no longer an admin."
            });
        }

        private static bool IsRootAdmin(ApplicationUser u) =>
            string.Equals(u.UserName, DbSeeder.RootAdminUserName, StringComparison.OrdinalIgnoreCase);
    }
}
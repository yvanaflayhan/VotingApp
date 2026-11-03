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

        // Only shows the empty page. The table fills itself with AJAX.
        public IActionResult Users()
        {
            return View();
        }

        // Called by DataTables for every page, sort and search
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

            // Only columns from this fixed list can be sorted
            var desc = request.Sort == "desc";
            IOrderedQueryable<ApplicationUser> ordered = request.Column switch
            {
                "fullName" => desc ? query.OrderByDescending(u => u.FullName) : query.OrderBy(u => u.FullName),
                _          => desc ? query.OrderByDescending(u => u.UserName) : query.OrderBy(u => u.UserName)
            };

            var users = await ordered
                .ThenBy(u => u.Id)          // keeps the order stable when values are equal
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
                isMe = u.Id == currentUserId
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

        // Now answers with JSON instead of redirecting, because the page no longer reloads
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeRole(string userId, string role)
        {
            if (role != "Admin" && role != "User")
                return BadRequest();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            // Rule 1: an admin cannot change their own role
            if (userId == _userManager.GetUserId(User))
                return Json(new { success = false, message = "You cannot change your own role." });

            // Rule 2: at least one admin must always exist
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (isAdmin && role == "User")
            {
                var admins = await _userManager.GetUsersInRoleAsync("Admin");
                if (admins.Count <= 1)
                    return Json(new { success = false, message = "There must always be at least one admin." });
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, role);
            await _userManager.UpdateSecurityStampAsync(user);

            return Json(new { success = true, message = $"{user.UserName} is now {role}." });
        }
    }
}
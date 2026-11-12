using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Voting_app.Common;
using Voting_app.Models;
using Voting_app.Services;

namespace Voting_app.Controllers
{
    [Authorize(Roles = Roles.Admin)]
    public class AdminController : BaseController
    {
        private readonly IUserService _users;

        public AdminController(IUserService users)
        {
            _users = users;
        }

        #region Public actions

        // Only the empty page. The table fills itself with AJAX.
        public IActionResult Users() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> List([FromForm] DataTableRequest request) =>
            Json(await _users.GetListAsync(request, CurrentUserId));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeRole(string userId, string role)
        {
            if (role != Roles.Admin && role != Roles.User)
                return BadRequest();

            return ToJson(await _users.ChangeRoleAsync(userId, role, CurrentUserId));
        }

        #endregion
    }
}
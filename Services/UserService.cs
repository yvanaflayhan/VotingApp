using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Voting_app.Common;
using Voting_app.Data;
using Voting_app.Models;

namespace Voting_app.Services
{
    public interface IUserService
    {
        Task<UsersTableResponse> GetListAsync(DataTableRequest request, string? currentUserId);
        Task<ServiceResult> ChangeRoleAsync(string userId, string role, string? currentUserId);
    }

    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UserService(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<UsersTableResponse> GetListAsync(
            DataTableRequest request,
            string? currentUserId
        )
        {
            var query = _userManager.Users.AsNoTracking();
            var total = await query.CountAsync();

            if (request.SearchPattern is { } term)
                query = query.Where(u =>
                    EF.Functions.ILike(u.UserName!, term)
                    || EF.Functions.ILike(u.Email!, term)
                    || EF.Functions.ILike(u.FullName, term)
                );

            var filtered = await query.CountAsync();

            var users = await query
                .OrderBy(u => u.UserName)
                .ThenBy(u => u.Id) // a stable order, needed for paging
                .Skip(request.SafeStart)
                .Take(request.SafeLength)
                .ToListAsync();

            var adminIds = (await _userManager.GetUsersInRoleAsync(Roles.Admin))
                .Select(u => u.Id)
                .ToHashSet();

            return new UsersTableResponse
            {
                Draw = request.Draw,
                RecordsTotal = total,
                RecordsFiltered = filtered,
                AdminCount = adminIds.Count,
                Data = users
                    .Select(u => new UserListItem
                    {
                        Id = u.Id,
                        FullName = u.FullName,
                        UserName = u.UserName ?? "",
                        Email = u.Email ?? "",
                        Role = adminIds.Contains(u.Id) ? Roles.Admin : Roles.User,
                        IsMe = u.Id == currentUserId,
                        IsRoot = IsRootAdmin(u),
                    })
                    .ToList(),
            };
        }

        public async Task<ServiceResult> ChangeRoleAsync(
            string userId,
            string role,
            string? currentUserId
        )
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return ServiceResult.NotFound("This user no longer exists.");

            // Rule 1: the main admin can never be changed, by anyone
            if (IsRootAdmin(user))
                return ServiceResult.Invalid("The main admin cannot be changed.");

            // Rule 2: nobody changes their own role
            if (user.Id == currentUserId)
                return ServiceResult.Invalid("You cannot change your own role.");

            // Nothing to do if the user already has this role
            var isAdmin = await _userManager.IsInRoleAsync(user, Roles.Admin);
            if ((role == Roles.Admin) == isAdmin)
                return ServiceResult.Invalid(
                    $"{user.UserName} is already {(isAdmin ? "an admin" : "a user")}."
                );

            await _userManager.RemoveFromRolesAsync(user, await _userManager.GetRolesAsync(user));
            await _userManager.AddToRoleAsync(user, role);

            // Invalidates the old login cookie, so the new role applies on the next request
            await _userManager.UpdateSecurityStampAsync(user);

            return ServiceResult.Ok(
                role == Roles.Admin
                    ? $"{user.UserName} is now an admin."
                    : $"{user.UserName} is no longer an admin."
            );
        }

        private static bool IsRootAdmin(ApplicationUser user) =>
            string.Equals(
                user.UserName,
                DbSeeder.RootAdminUserName,
                StringComparison.OrdinalIgnoreCase
            );
    }
}

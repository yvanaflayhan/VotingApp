using Microsoft.AspNetCore.Identity;
using Voting_app.Models;

namespace Voting_app.Data
{
    public static class DbSeeder
    {
        public const string RootAdminUserName = "admin";
        public static async Task SeedAsync(IServiceProvider services, IConfiguration config)
        {
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            //Create the roles if they don't exist yet
            foreach (var roleName in new[] { "Admin", "User" })
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                    await roleManager.CreateAsync(new IdentityRole(roleName));
            }

            //Create the admin account if it doesn't exist yet
            var adminPassword = config["SeedAdmin:Password"];
            if (string.IsNullOrEmpty(adminPassword))
                return; // no password configured, skip creating the admin

            var admin = await userManager.FindByNameAsync("admin");
            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = "admin",
                    Email = "admin@votingapp.com",
                    FullName = "System Admin",
                    DateOfBirth = new DateOnly(1990, 1, 1)
                };

                var result = await userManager.CreateAsync(admin, adminPassword);
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(admin, "Admin");
            }
        }
    }
}
using Microsoft.AspNetCore.Identity;

namespace Voting_app.Models
{
    public class ApplicationUser:IdentityUser
    {
        public string FullName {get; set;} = string.Empty;
        public DateOnly DateOfBirth {get; set;} 
        
    }
}
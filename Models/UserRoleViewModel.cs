namespace Voting_app.Models
{
    public class UserRoleViewModel
    {
        public string Id {get; set;} = string.Empty;
        public string UserName {get; set;} = string.Empty;
        public string Email { get; set;} = string.Empty;
        public string FullName {get; set;} = string.Empty;
        public string Role {get; set;} = "User";
    }
}
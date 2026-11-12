namespace Voting_app.Models
{
    public class UserListItem
    {
        public string Id { get; set; } = "";
        public string FullName { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
        public bool IsMe { get; set; }
        public bool IsRoot { get; set; }
    }

    public class UsersTableResponse : DataTableResponse<UserListItem>
    {
        public int AdminCount { get; set; }
    }
}

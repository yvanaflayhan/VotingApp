namespace Voting_app.Models
{
    public class UsersPageViewModel
    {
        public List<UserRoleViewModel> Users{get; set;} = new();
        public int Page {get; set;}
        public int TotalPages {get; set;}
        public int FilteredCount {get; set;}
        public int TotalUsers{get; set;}
        public int AdminCount {get; set;}
        public string? Search{get; set;} 
    }
}
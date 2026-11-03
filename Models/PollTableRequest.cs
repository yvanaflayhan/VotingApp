namespace Voting_app.Models
{
    public class PollTableRequest : DataTableRequest
    {
        public PollType Type {get; set;}
        
    }
}
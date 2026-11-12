namespace Voting_app.Models
{
    public class PollDetailsViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public PollType Type { get; set; }
        public string? ImagePath { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = "";
        public string CreatorName { get; set; } = "";
        public int TotalVotes { get; set; }
        public bool IsAuthenticated { get; set; }
        public int? MyChoiceId { get; set; }
        public List<OptionResult> Choices { get; set; } = new();
    }
}

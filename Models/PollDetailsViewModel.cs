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
        public List<ChoiceResult> Choices { get; set; } = new();
    }

    public class ChoiceResult
    {
        public string Text { get; set; } = "";
        public int Votes { get; set; }
        public int Percent { get; set; }
    }
}
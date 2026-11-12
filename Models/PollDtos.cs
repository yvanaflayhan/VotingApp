namespace Voting_app.Models
{
    public class PollListItem
    {
        public int Id { get; set; }
        public bool IsMine { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string? ImageUrl { get; set; }
        public int Choices { get; set; }
        public int Votes { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = "";
        public int? MyChoiceId { get; set; }
        public List<OptionResult> Options { get; set; } = new();
    }

    // One choice with its result
    public class OptionResult
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";
        public string? ImageUrl { get; set; }
        public int Votes { get; set; }
        public int Percent { get; set; }
    }
}

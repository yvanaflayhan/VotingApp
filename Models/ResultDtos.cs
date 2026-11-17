namespace Voting_app.Models;

public class ResultsRequest : DataTableRequest
{
    public PollType? Type { get; set; }
}

public record PagedResult<T>(int Total, int Filtered, List<T> Items);

public class PollResultItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Type { get; set; } = "";
    public string? ImageUrl { get; set; }
    public DateTime EndedAt { get; set; }
    public int TotalVotes { get; set; }
    public List<OptionResult> Choices { get; set; } = new();
}

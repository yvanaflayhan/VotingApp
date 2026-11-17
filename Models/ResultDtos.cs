namespace Voting_app.Models;

public class ResultsRequest : DataTableRequest
{
    public PollType? Type { get; set; }
}

public record PagedResult<T>(int Total, int Filtered, List<T> Items);

public class ResultChoice
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
    public string? ImageUrl { get; set; }
    public int Votes { get; set; }
    public double Percent { get; set; }
    public bool IsWinner { get; set; }
}

public class PollResultItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Type { get; set; } = "";
    public string? ImageUrl { get; set; }
    public DateTime EndedAt { get; set; }
    public int TotalVotes { get; set; }
    public List<ResultChoice> Choices { get; set; } = new();
}

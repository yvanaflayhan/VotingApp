using Microsoft.EntityFrameworkCore;
using Voting_app.Common;
using Voting_app.Data;
using Voting_app.Models;


namespace Voting_app.Services;

public interface IResultsService
{
    Task<PagedResult<PollResultItem>> GetFinishedAsync(ResultsRequest request, DateTime nowUtc);
}

public class ResultsService : IResultsService
{
    private readonly ApplicationDbContext _db;

    public ResultsService(ApplicationDbContext db) => _db = db;

    #region Reading

    public async Task<PagedResult<PollResultItem>> GetFinishedAsync(ResultsRequest request, DateTime nowUtc)
    {
        //Finished polls, optionally of one type
        var finished = _db.Polls.Where(p => p.IsActive && p.EndDate <= nowUtc);//ended polls
        if (request.Type != null)
            finished = finished.Where(p => p.Type == request.Type);

        var total = await finished.CountAsync();

        //Search on the title
        var filteredQuery = finished;
        if (!string.IsNullOrWhiteSpace(request.SearchPattern))
            filteredQuery = filteredQuery.Where(p => EF.Functions.ILike(p.Title, request.SearchPattern));

        var filtered = await filteredQuery.CountAsync();

        //The polls of this page, newest ending first (the Id keeps the order stable)
        var page = await filteredQuery
            .OrderByDescending(p => p.EndDate).ThenBy(p => p.Id)
            .Skip(request.SafeStart).Take(request.SafeLength)
            .Select(p => new PollRow
            {
                Id = p.Id,
                Title = p.Title,
                Type = p.Type.ToString(),
                ImageUrl = p.ImagePath,
                EndedAt = p.EndDate,
                Choices = p.Choices.OrderBy(c => c.Id)
                           .Select(c => new ChoiceRow { Id = c.Id, Text = c.Text, ImageUrl = c.ImagePath })
                           .ToList()
            })
            .ToListAsync();

        //Votes per choice for ALL the polls of this page: one query
        var counts = await _db.Votes.CountByChoiceAsync(page.Select(p => p.Id).ToList());

        return new PagedResult<PollResultItem>(total, filtered, page.Select(p => ToItem(p, counts)).ToList());
    }

    #endregion

    #region Private methods

    // Here the winner is decided. This is the only place that knows the rule.
    private static PollResultItem ToItem(PollRow poll, Dictionary<int, int> counts)
    {
        var choices = poll.Choices.Select(c => new ResultChoice
        {
            Id = c.Id,
            Text = c.Text,
            ImageUrl = c.ImageUrl,
            Votes = counts.GetValueOrDefault(c.Id)
        }).ToList();

        var total = choices.Sum(c => c.Votes);
        var best = choices.Count == 0 ? 0 : choices.Max(c => c.Votes);

        foreach (var c in choices)
        {
            c.Percent = total == 0 ? 0 : Math.Round(100.0 * c.Votes / total, 1);
            c.IsWinner = best > 0 && c.Votes == best;      // no votes = no winner; a tie = several winners
        }

        return new PollResultItem
        {
            Id = poll.Id,
            Title = poll.Title,
            Type = poll.Type,
            ImageUrl = poll.ImageUrl,
            EndedAt = poll.EndedAt,
            TotalVotes = total,
            Choices = choices.OrderByDescending(c => c.Votes).ThenBy(c => c.Text).ToList()
        };
    }

    // Small private shapes for the database query (they never leave this file)
    private class PollRow
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Type { get; set; } = "";
        public string? ImageUrl { get; set; }
        public DateTime EndedAt { get; set; }
        public List<ChoiceRow> Choices { get; set; } = new();
    }

    private class ChoiceRow
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";
        public string? ImageUrl { get; set; }
    }

    #endregion
}
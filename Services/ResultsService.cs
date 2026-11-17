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
        //Finished polls of the last KeepDays days, optionally of one type
        var since = nowUtc.AddDays(-ResultsRules.KeepDays);
        var finished = _db.Polls.Where(p => p.IsActive && p.EndDate <= nowUtc && p.EndDate > since);
        if (request.Type != null)
            finished = finished.Where(p => p.Type == request.Type);

        var total = await finished.CountAsync();

        //Search on the title
        var filteredQuery = finished;
        if (!string.IsNullOrWhiteSpace(request.SearchPattern))
            filteredQuery = filteredQuery.Where(p => EF.Functions.ILike(p.Title, request.SearchPattern));

        var filtered = await filteredQuery.CountAsync();

        //The polls of this page (with their choices), newest ending first
        var page = await filteredQuery
            .OrderByDescending(p => p.EndDate).ThenBy(p => p.Id)
            .Skip(request.SafeStart).Take(request.SafeLength)
            .Include(p => p.Choices)
            .AsNoTracking()
            .ToListAsync();

        //Votes per choice for ALL the polls of this page: one query
        var counts = await _db.Votes.CountByChoiceAsync(page.Select(p => p.Id).ToList());

        return new PagedResult<PollResultItem>(total, filtered, page.Select(p => ToItem(p, counts)).ToList());
    }

    #endregion

    #region Private methods

    private static PollResultItem ToItem(Poll poll, IReadOnlyDictionary<int, int> counts)
    {
        var choices = PollResults.Build(poll.Choices, counts);
        PollResults.MarkWinners(choices);

        return new PollResultItem
        {
            Id = poll.Id,
            Title = poll.Title,
            Type = poll.Type.ToString(),
            ImageUrl = poll.ImagePath,
            EndedAt = poll.EndDate,
            TotalVotes = choices.Sum(c => c.Votes),
            Choices = choices.OrderByDescending(c => c.Votes).ThenBy(c => c.Text).ToList()
        };
    }

    #endregion
}
using Microsoft.EntityFrameworkCore;
using Voting_app.Models;

namespace Voting_app.Common
{
    public static class VoteQueryExtensions//how many votes has each choice?
    {
        public static Task<Dictionary<int, int>> CountByChoiceAsync(
            this IQueryable<Vote> votes,
            List<int> pollIds
        ) =>
            votes
                .Where(v => pollIds.Contains(v.PollId))
                .GroupBy(v => v.ChoiceId)
                .Select(g => new { ChoiceId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ChoiceId, x => x.Count);
    }
}

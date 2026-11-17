using Voting_app.Models;

namespace Voting_app.Services
{
    public static class PollResults
    {
        public static List<OptionResult> Build(
            IEnumerable<Choice> choices,
            IReadOnlyDictionary<int, int> votesByChoice
        )
        {
            var ordered = choices.OrderBy(c => c.Id).ToList();
            var total = ordered.Sum(c => votesByChoice.GetValueOrDefault(c.Id));

            return ordered
                .Select(c =>
                {
                    var votes = votesByChoice.GetValueOrDefault(c.Id);
                    return new OptionResult
                    {
                        Id = c.Id,
                        Text = c.Text,
                        ImageUrl = c.ImagePath,
                        Votes = votes,
                        Percent = total == 0 ? 0 : (int)Math.Round(votes * 100.0 / total),
                    };
                })
                .ToList();
        }

        // The choice(s) with the most votes win. Nobody voted = no winner. A tie = several winners.
        public static void MarkWinners(List<OptionResult> results)
        {
            var best = results.Count == 0 ? 0 : results.Max(r => r.Votes);

            foreach (var r in results)
                r.IsWinner = best > 0 && r.Votes == best;
        }
    }
}

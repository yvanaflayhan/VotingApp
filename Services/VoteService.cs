using Microsoft.EntityFrameworkCore;
using Voting_app.Common;
using Voting_app.Data;
using Voting_app.Models;

namespace Voting_app.Services
{
    // A vote answer carries the fresh results, so the card can update without reloading
    public class VoteResult : ServiceResult
    {
        public int? MyChoiceId { get; init; }
        public int Votes { get; init; }
        public List<OptionResult> Options { get; init; } = new();
    }

    public interface IVoteService
    {
        Task<VoteResult> VoteAsync(int pollId, int choiceId, string userId, bool isAdmin);
    }

    public class VoteService : IVoteService
    {
        private const string AlreadyVoted = "You have already voted in this poll.";

        private readonly ApplicationDbContext _db;

        public VoteService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<VoteResult> VoteAsync(
            int pollId,
            int choiceId,
            string userId,
            bool isAdmin
        )
        {
            // Admins manage polls, they do not vote
            if (isAdmin)
                return Fail("Admins manage polls and cannot vote.");

            var poll = await _db
                .Polls.Include(p => p.Choices)
                .FirstOrDefaultAsync(p => p.Id == pollId && p.IsActive);
            if (poll == null)
                return new VoteResult
                {
                    Status = ResultStatus.NotFound,
                    Message = "This poll no longer exists.",
                };

            var status = PollStatus.Of(poll, DateTime.UtcNow);
            if (status == PollStatus.Upcoming)
                return Fail("This poll has not started yet.");
            if (status == PollStatus.Closed)
                return Fail("This poll has ended.");

            // The choice must belong to THIS poll
            if (!poll.Choices.Any(c => c.Id == choiceId))
                return Fail("Invalid choice.");

            if (await _db.Votes.AnyAsync(v => v.PollId == pollId && v.UserId == userId))
                return Fail(AlreadyVoted);

            _db.Votes.Add(
                new Vote
                {
                    UserId = userId,
                    PollId = pollId,
                    ChoiceId = choiceId,
                }
            );

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Two clicks at the same moment: the unique index (UserId, PollId) refused the second one
                return Fail(AlreadyVoted);
            }

            var options = PollResults.Build(
                poll.Choices,
                await _db.Votes.CountByChoiceAsync(new List<int> { pollId })
            );

            return new VoteResult
            {
                Message = "Your vote was saved.",
                MyChoiceId = choiceId,
                Votes = options.Sum(o => o.Votes),
                Options = options,
            };
        }

        private static VoteResult Fail(string message) =>
            new() { Status = ResultStatus.Invalid, Message = message };
    }
}

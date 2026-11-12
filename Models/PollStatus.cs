namespace Voting_app.Models
{
    public static class PollStatus
    {
        public const string Upcoming = "Upcoming";
        public const string Open = "Open";
        public const string Closed = "Closed";

        public static string Of(Poll poll, DateTime now) =>
            poll.StartDate > now ? Upcoming : (poll.EndDate < now ? Closed : Open);
    }
}

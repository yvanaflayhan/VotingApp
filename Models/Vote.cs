using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Voting_app.Models
{
    public class Vote
    {
        public int Id {get; set;}

        //user who voted 
        [Required]
        public string UserId {get; set;} = string.Empty;
        public ApplicationUser? User {get; set;}

        //poll being voted in 
        public int PollId {get; set;}
        public Poll? Poll{get; set;}

        //selected choice
        public int ChoiceId {get; set;}
        public Choice? Choice {get; set;}

        public DateTime VotedAt {get; set;} = DateTime.UtcNow;
    }
}
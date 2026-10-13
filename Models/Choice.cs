using System.ComponentModel.DataAnnotations;

namespace Voting_app.Models
{
    public class Choice
    {
        public int Id {get; set;}
        
        [Required]
        [StringLength(200)]
        public string Text {get; set;} = string.Empty;

        public int PollId {get; set;}
        public Poll? Poll {get; set;}
    }
}
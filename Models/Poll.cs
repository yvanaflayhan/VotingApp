using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;

namespace Voting_app.Models
{
    public class Poll
    {
        public int Id {get; set;}

        [Required]
        [StringLength(150)]
        public string Title {get; set;} = string.Empty;

        [StringLength(1000)]
        public string Description {get; set;} = string.Empty;

        [Required]
        public PollType Type {get; set;}

        public DateTime CreatedAt {get; set;} = DateTime.UtcNow;

        [Required]
        public DateTime StartDate {get; set;} = DateTime.UtcNow;
        [Required]
        public DateTime EndDate {get; set;}

        public bool IsActive {get; set;} = true;

        //user who created this poll
        public string CreatorId {get; set;} = string.Empty;
        public ApplicationUser? Creator {get; set;}

        public ICollection<Choice> Choices {get; set;} = new List<Choice>();

    }
    public enum PollType
    {
        Community,
        Awards
    }
}
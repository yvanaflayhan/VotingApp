using System.ComponentModel.DataAnnotations;

namespace Voting_app.Models
{
    public class CreatePollViewModel
    {
        [Required]
        [StringLength(150)]
        public string Title {get; set;} = string.Empty;

        [StringLength(1000)]
        public string? Description {get; set;}

        [Required]
        public PollType Type {get; set;}

        [Required]
        [Display(Name = "End date")]
        public DateTime EndDate {get; set;} = DateTime.Now.AddDays(7);

        [Required]
        [Display(Name = "Choices (one per line)")]
        public string ChoiceText {get; set;} = string.Empty;

    }
}
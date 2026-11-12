using System.ComponentModel.DataAnnotations;

namespace Voting_app.Models
{
    public class CreatePollViewModel
    {
        [Required]
        [StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [Required]
        public PollType? Type { get; set; } = PollType.Community;

        [Required]
        [Display(Name = "Start date")]
        public DateTime? StartDate { get; set; }

        [Required]
        [Display(Name = "End date")]
        public DateTime? EndDate { get; set; }

        public IFormFile? Image { get; set; }
        public int TimezoneOffset { get; set; }
        public List<ChoiceInput> Choices { get; set; } = new();
    }
    public class ChoiceInput
{
    public string? Text { get; set; }
    public IFormFile? Image { get; set; }
}
}
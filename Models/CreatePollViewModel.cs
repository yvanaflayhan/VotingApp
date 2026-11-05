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

        // Minutes between UTC and the browser's time, sent by JavaScript
        public int TimezoneOffset { get; set; }

        // Bound from several inputs that all have name="Choices"
        public List<string> Choices { get; set; } = new();
    }
}
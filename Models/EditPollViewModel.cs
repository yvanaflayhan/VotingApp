using System.ComponentModel.DataAnnotations;

namespace Voting_app.Models
{
    public class EditPollViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [Display(Name = "Start Date")]
        public DateTime? StartDate { get; set; }

        [Required]
        [Display(Name = "End date")]
        public DateTime? EndDate { get; set; }

        public int TimezoneOffset { get; set; }

        public IFormFile? Image { get; set; }
        public bool RemoveImage { get; set; }

        // Only used to DISPLAY the page. The controller never reads these from the form.
        public PollType Type { get; set; }
        public string? CurrentImagePath { get; set; }
        public string StartUtc { get; set; } = "";
        public string EndUtc { get; set; } = "";
        public bool Started { get; set; }
        public int ChoiceCount { get; set; }
        public int VoteCount { get; set; }
    }
}

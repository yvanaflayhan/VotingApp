using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

namespace Voting_app.Models
{
    public class RegisterViewModel
    {
        [Required]
        [Display(Name = "FullName")]
        public string FullName{get; set;} = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name ="Date of Birth")]
        public DateOnly DateOfBirth{get; set;}
        
        [Required]
        [EmailAddress] //to check that the text look like a valid email
        public string Email {get; set;} =string.Empty;

        [Required]
        [Display(Name = "Username")]//tell Html to set the label username
        public string Username {get; set;}= string.Empty;

        [Required]
        [DataType(DataType.Password)]//tell the browser to hide the characters
        public string Password {get; set;} = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password" , ErrorMessage ="The password and confirmation password do not match.")]
        public string ConfirmPassword {get; set;} = string.Empty;
  
    }

    public class LoginViewModel
        {
            [Required]
            public string Username {get; set;} = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            public string Password {get; set;} = string.Empty;

            [Display(Name = "Remember me?")]
            public bool RememberMe {get; set;}
            
        }
}
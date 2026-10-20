using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Voting_app.Models;

namespace Voting_app.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions <ApplicationDbContext> options) : base(options)
        {
            
        }

        public DbSet<Poll> Polls => Set<Poll>();
        public DbSet<Choice> Choices => Set<Choice>();
        public DbSet<Vote> Votes => Set<Vote>();
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            //User can vote once in each poll
            builder.Entity<Vote>()
            .HasIndex(v=> new{v.UserId, v.PollId})
            .IsUnique();

            //Prevent deleting a choice while votes reference it
            builder.Entity<Vote>()
            .HasOne(v => v.Choice)
            .WithMany()
            .HasForeignKey(v => v.ChoiceId)
            .OnDelete(DeleteBehavior.Restrict);
        }
    }
} 
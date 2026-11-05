using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Voting_app.Data;
using Voting_app.Models;

namespace Voting_app.Controllers
{
    public class PollsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public PollsController(ApplicationDbContext db)
        {
            _db = db;
        }

        // ===== Public pages: guests included =====

        public IActionResult Community() => ShowPolls(PollType.Community);
        public IActionResult Awards()    => ShowPolls(PollType.Awards);

        private IActionResult ShowPolls(PollType type)
        {
            ViewBag.Type = type;
            return View("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> List([FromForm] PollTableRequest request)
        {
            var length = Math.Clamp(request.Length, 1, 100);
            var start = Math.Max(0, request.Start);
            var now = DateTime.UtcNow;

            var query = _db.Polls.Where(p => p.IsActive && p.Type == request.Type);
            var recordsTotal = await query.CountAsync();

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var term = $"%{request.Search.Trim()}%";
                query = query.Where(p =>
                    EF.Functions.ILike(p.Title, term) ||
                    EF.Functions.ILike(p.Description, term));
            }

            var recordsFiltered = await query.CountAsync();

            var rows = await query
                .OrderByDescending(p => p.CreatedAt)
                .ThenBy(p => p.Id)
                .Skip(start)
                .Take(length)
                .Select(p => new
                {
                    id = p.Id,
                    title = p.Title,
                    description = p.Description,
                    choices = p.Choices.Count,
                    votes = _db.Votes.Count(v => v.PollId == p.Id),
                    endDate = p.EndDate,
                    status = p.StartDate > now ? "Upcoming" : (p.EndDate < now ? "Closed" : "Open")
                })
                .ToListAsync();

            return Json(new { draw = request.Draw, recordsTotal, recordsFiltered, data = rows });
        }

        // ===== Admin only =====

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View(new CreatePollViewModel());
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreatePollViewModel model)
        {
            // Choices: trim, remove empty ones, remove duplicates (ignoring case)
            var choices = model.Choices
                .Select(c => (c ?? "").Trim())
                .Where(c => c.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (choices.Count < 2)
                ModelState.AddModelError(nameof(model.Choices), "Enter at least 2 different choices.");
            else if (choices.Count > 10)
                ModelState.AddModelError(nameof(model.Choices), "A poll can have at most 10 choices.");
            else if (choices.Any(c => c.Length > 200))
                ModelState.AddModelError(nameof(model.Choices), "Each choice can have at most 200 characters.");

            // Dates: the browser sends local time, the database stores UTC
            var offset = Math.Clamp(model.TimezoneOffset, -840, 840);
            DateTime? startUtc = model.StartDate.HasValue ? ToUtc(model.StartDate.Value, offset) : null;
            DateTime? endUtc = model.EndDate.HasValue ? ToUtc(model.EndDate.Value, offset) : null;

            if (endUtc.HasValue && endUtc <= DateTime.UtcNow.AddMinutes(1))
                ModelState.AddModelError(nameof(model.EndDate), "The end date must be in the future.");
            else if (startUtc.HasValue && endUtc.HasValue && endUtc <= startUtc)
                ModelState.AddModelError(nameof(model.EndDate), "The end date must be after the start date.");

            if (!ModelState.IsValid)
                return View(model);

            var poll = new Poll
            {
                Title = model.Title.Trim(),
                Description = model.Description?.Trim() ?? "",
                Type = model.Type!.Value,
                StartDate = startUtc!.Value,
                EndDate = endUtc!.Value,
                CreatorId = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                Choices = choices.Select(c => new Choice { Text = c }).ToList()
            };

            _db.Polls.Add(poll);
            await _db.SaveChangesAsync();     // saves the poll AND its choices together

            TempData["Success"] = $"Poll \"{poll.Title}\" was created.";
            return poll.Type == PollType.Awards
                ? RedirectToAction(nameof(Awards))
                : RedirectToAction(nameof(Community));
        }

        // getTimezoneOffset() in JavaScript = UTC minus local, in minutes
        private static DateTime ToUtc(DateTime local, int offsetMinutes)
            => DateTime.SpecifyKind(local.AddMinutes(offsetMinutes), DateTimeKind.Utc);
    }
}
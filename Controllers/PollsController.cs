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
        private const long MaxImageBytes = 2 * 1024 * 1024;
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        public PollsController(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // ===== Public pages: guests included =====

        public IActionResult Community() => ShowPolls(PollType.Community);
        public IActionResult Awards()    => ShowPolls(PollType.Awards);

        private IActionResult ShowPolls(PollType type)
        {
            ViewBag.Type = type;
            // Admin sees the management table, everybody else sees cards
            return View(User.IsInRole("Admin") ? "Index" : "Cards");
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
                    imageUrl = p.ImagePath,
                    choices = p.Choices.Count,
                    votes = _db.Votes.Count(v => v.PollId == p.Id),
                    endDate = p.EndDate,
                    status = p.StartDate > now ? "Upcoming" : (p.EndDate < now ? "Closed" : "Open")
                })
                .ToListAsync();

            return Json(new { draw = request.Draw, recordsTotal, recordsFiltered, data = rows });
        }

        // Everybody can open the details (guests too). Only the admin can see inactive polls.
        public async Task<IActionResult> Details(int id)
        {
            var isAdmin = User.IsInRole("Admin");

            var poll = await _db.Polls
                .Include(p => p.Choices)
                .Include(p => p.Creator)
                .FirstOrDefaultAsync(p => p.Id == id && (p.IsActive || isAdmin));

            if (poll == null)
                return NotFound();

            var counts = await _db.Votes
                .Where(v => v.PollId == id)
                .GroupBy(v => v.ChoiceId)
                .Select(g => new { ChoiceId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ChoiceId, x => x.Count);

            var total = counts.Values.Sum();
            var now = DateTime.UtcNow;

            var model = new PollDetailsViewModel
            {
                Id = poll.Id,
                Title = poll.Title,
                Description = poll.Description,
                Type = poll.Type,
                ImagePath = poll.ImagePath,
                StartDate = poll.StartDate,
                EndDate = poll.EndDate,
                Status = poll.StartDate > now ? "Upcoming" : (poll.EndDate < now ? "Closed" : "Open"),
                CreatorName = poll.Creator?.UserName ?? "",
                TotalVotes = total,
                Choices = poll.Choices
                    .OrderBy(c => c.Id)
                    .Select(c =>
                    {
                        var votes = counts.GetValueOrDefault(c.Id);
                        return new ChoiceResult
                        {
                            Text = c.Text,
                            Votes = votes,
                            Percent = total == 0 ? 0 : (int)Math.Round(votes * 100.0 / total)
                        };
                    }).ToList()
            };

            return View(model);
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
        [RequestSizeLimit(3_000_000)]
        public async Task<IActionResult> Create(CreatePollViewModel model)
        {
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

            var offset = Math.Clamp(model.TimezoneOffset, -840, 840);
            DateTime? startUtc = model.StartDate.HasValue ? ToUtc(model.StartDate.Value, offset) : null;
            DateTime? endUtc = model.EndDate.HasValue ? ToUtc(model.EndDate.Value, offset) : null;

            if (endUtc.HasValue && endUtc <= DateTime.UtcNow.AddMinutes(1))
                ModelState.AddModelError(nameof(model.EndDate), "The end date must be in the future.");
            else if (startUtc.HasValue && endUtc.HasValue && endUtc <= startUtc)
                ModelState.AddModelError(nameof(model.EndDate), "The end date must be after the start date.");

            // The photo is optional, but if there is one it must be a real, small image
            if (model.Image != null && model.Image.Length > 0)
            {
                if (model.Image.Length > MaxImageBytes)
                    ModelState.AddModelError(nameof(model.Image), "The photo must be 2 MB or smaller.");
                else if (!await IsValidImageAsync(model.Image))
                    ModelState.AddModelError(nameof(model.Image), "Please upload a JPG, PNG or WEBP image.");
            }

            if (!ModelState.IsValid)
                return View(model);

            string? imagePath = null;
            if (model.Image != null && model.Image.Length > 0)
                imagePath = await SaveImageAsync(model.Image);

            var poll = new Poll
            {
                Title = model.Title.Trim(),
                Description = model.Description?.Trim() ?? "",
                Type = model.Type!.Value,
                StartDate = startUtc!.Value,
                EndDate = endUtc!.Value,
                ImagePath = imagePath,
                CreatorId = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                Choices = choices.Select(c => new Choice { Text = c }).ToList()
            };

            try
            {
                _db.Polls.Add(poll);
                await _db.SaveChangesAsync();
            }
            catch
            {
                DeleteImage(imagePath);     // do not leave an orphan file if the save failed
                throw;
            }

            TempData["Success"] = $"Poll \"{poll.Title}\" was created.";
            return poll.Type == PollType.Awards
                ? RedirectToAction(nameof(Awards))
                : RedirectToAction(nameof(Community));
        }

        // ===== Helpers =====

        private static DateTime ToUtc(DateTime local, int offsetMinutes)
            => DateTime.SpecifyKind(local.AddMinutes(offsetMinutes), DateTimeKind.Utc);

        // Checks the extension AND the first bytes of the file (the real format)
        private static async Task<bool> IsValidImageAsync(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext)) return false;

            var h = new byte[12];
            await using var stream = file.OpenReadStream();
            if (await stream.ReadAsync(h, 0, h.Length) < h.Length) return false;

            var jpeg = h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF;
            var png  = h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47;
            var webp = h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F'
                    && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P';
            return jpeg || png || webp;
        }

        private async Task<string> SaveImageAsync(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{ext}";          // never trust the original name
            var folder = Path.Combine(_env.WebRootPath, "uploads", "polls");
            Directory.CreateDirectory(folder);

            await using var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/polls/{fileName}";
        }

        private void DeleteImage(string? path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var full = Path.Combine(_env.WebRootPath, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(full))
                System.IO.File.Delete(full);
        }
    }
}
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

        // =====================================================
        // Public pages: guests included (no [Authorize])
        // =====================================================

        public IActionResult Community() => ShowPolls(PollType.Community);

        public IActionResult Awards() => ShowPolls(PollType.Awards);

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
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var query = _db.Polls.Where(p => p.IsActive && p.Type == request.Type);
            var recordsTotal = await query.CountAsync();

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var term = $"%{request.Search.Trim()}%";
                query = query.Where(p =>
                    EF.Functions.ILike(p.Title, term) || EF.Functions.ILike(p.Description, term)
                );
            }

            var recordsFiltered = await query.CountAsync();

            // 1) The polls of this page, with their choices
            var page = await query
                .OrderByDescending(p => p.CreatedAt)
                .ThenBy(p => p.Id)
                .Skip(start)
                .Take(length)
                .Select(p => new
                {
                    p.Id,
                    p.CreatorId,
                    p.Title,
                    p.Description,
                    p.ImagePath,
                    p.StartDate,
                    p.EndDate,
                    Choices = p
                        .Choices.OrderBy(c => c.Id)
                        .Select(c => new { c.Id, c.Text })
                        .ToList(),
                })
                .ToListAsync();

            var pollIds = page.Select(p => p.Id).ToList();

            // 2) Votes per choice for all the polls of this page: one query
            var counts = await _db
                .Votes.Where(v => pollIds.Contains(v.PollId))
                .GroupBy(v => v.ChoiceId)
                .Select(g => new { ChoiceId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ChoiceId, x => x.Count);

            // 3) What this user already voted in these polls: one query
            var myVotes =
                userId == null
                    ? new Dictionary<int, int>()
                    : await _db
                        .Votes.Where(v => v.UserId == userId && pollIds.Contains(v.PollId))
                        .ToDictionaryAsync(v => v.PollId, v => v.ChoiceId);

            var rows = page.Select(p =>
                {
                    var total = p.Choices.Sum(c => counts.GetValueOrDefault(c.Id));
                    return new
                    {
                        id = p.Id,
                        isMine = userId != null && p.CreatorId == userId,
                        title = p.Title,
                        description = p.Description,
                        imageUrl = p.ImagePath,
                        choices = p.Choices.Count, // the admin table uses this number
                        votes = total,
                        startDate = p.StartDate,
                        endDate = p.EndDate,
                        status = p.StartDate > now
                            ? "Upcoming"
                            : (p.EndDate < now ? "Closed" : "Open"),
                        myChoiceId = myVotes.TryGetValue(p.Id, out var mine) ? (int?)mine : null,
                        options = p
                            .Choices.Select(c =>
                            {
                                var v = counts.GetValueOrDefault(c.Id);
                                return new
                                {
                                    id = c.Id,
                                    text = c.Text,
                                    votes = v,
                                    percent = total == 0 ? 0 : (int)Math.Round(v * 100.0 / total),
                                };
                            })
                            .ToList(),
                    };
                })
                .ToList();

            return Json(
                new
                {
                    draw = request.Draw,
                    recordsTotal,
                    recordsFiltered,
                    data = rows,
                }
            );
        }

        // Everybody can open the details (guests too). Only the admin can see inactive polls.
        public async Task<IActionResult> Details(int id)
        {
            var isAdmin = User.IsInRole("Admin");

            var poll = await _db
                .Polls.Include(p => p.Choices)
                .Include(p => p.Creator)
                .FirstOrDefaultAsync(p => p.Id == id && (p.IsActive || isAdmin));

            if (poll == null)
                return NotFound();

            // Votes per choice, counted in one grouped query
            var counts = await _db
                .Votes.Where(v => v.PollId == id)
                .GroupBy(v => v.ChoiceId)
                .Select(g => new { ChoiceId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ChoiceId, x => x.Count);

            var total = counts.Values.Sum();
            var now = DateTime.UtcNow;

            // The choice this user voted for (null for guests and for users who have not voted)
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? myChoiceId =
                userId == null
                    ? null
                    : await _db
                        .Votes.Where(v => v.PollId == id && v.UserId == userId)
                        .Select(v => (int?)v.ChoiceId)
                        .FirstOrDefaultAsync();

            var model = new PollDetailsViewModel
            {
                Id = poll.Id,
                Title = poll.Title,
                Description = poll.Description,
                Type = poll.Type,
                ImagePath = poll.ImagePath,
                StartDate = poll.StartDate,
                EndDate = poll.EndDate,
                Status =
                    poll.StartDate > now ? "Upcoming" : (poll.EndDate < now ? "Closed" : "Open"),
                CreatorName = poll.Creator?.UserName ?? "",
                TotalVotes = total,
                IsAuthenticated = User.Identity?.IsAuthenticated == true,
                MyChoiceId = myChoiceId,
                Choices = poll
                    .Choices.OrderBy(c => c.Id)
                    .Select(c =>
                    {
                        var votes = counts.GetValueOrDefault(c.Id);
                        return new ChoiceResult
                        {
                            Id = c.Id,
                            Text = c.Text,
                            Votes = votes,
                            Percent = total == 0 ? 0 : (int)Math.Round(votes * 100.0 / total),
                        };
                    })
                    .ToList(),
            };

            return View(model);
        }

        // =====================================================
        // Voting: any logged-in user (admins included)
        // =====================================================

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Vote(int pollId, int choiceId)
        {
            // The voter comes from the login cookie, never from the form
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var now = DateTime.UtcNow;

            // Admins manage polls, they do not vote
            if (User.IsInRole("Admin"))
                return Json(
                    new { success = false, message = "Admins manage polls and cannot vote." }
                );

            var poll = await _db
                .Polls.Include(p => p.Choices)
                .FirstOrDefaultAsync(p => p.Id == pollId && p.IsActive);

            if (poll == null)
                return NotFound();

            if (poll.StartDate > now)
                return Json(new { success = false, message = "This poll has not started yet." });

            if (poll.EndDate < now)
                return Json(new { success = false, message = "This poll has ended." });

            // The choice must belong to THIS poll
            if (!poll.Choices.Any(c => c.Id == choiceId))
                return Json(new { success = false, message = "Invalid choice." });

            if (await _db.Votes.AnyAsync(v => v.PollId == pollId && v.UserId == userId))
                return Json(
                    new { success = false, message = "You have already voted in this poll." }
                );

            _db.Votes.Add(
                new Vote
                {
                    UserId = userId,
                    PollId = pollId,
                    ChoiceId = choiceId,
                }
            );

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Two clicks at the same moment: the unique index (UserId, PollId) refused the second one
                return Json(
                    new { success = false, message = "You have already voted in this poll." }
                );
            }

            // Send back the fresh results so the card can show them without reloading
            var counts = await _db
                .Votes.Where(v => v.PollId == pollId)
                .GroupBy(v => v.ChoiceId)
                .Select(g => new { ChoiceId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ChoiceId, x => x.Count);

            var total = counts.Values.Sum();
            var options = poll
                .Choices.OrderBy(c => c.Id)
                .Select(c =>
                {
                    var v = counts.GetValueOrDefault(c.Id);
                    return new
                    {
                        id = c.Id,
                        text = c.Text,
                        votes = v,
                        percent = total == 0 ? 0 : (int)Math.Round(v * 100.0 / total),
                    };
                })
                .ToList();

            return Json(
                new
                {
                    success = true,
                    message = "Your vote was saved.",
                    myChoiceId = choiceId,
                    votes = total,
                    options,
                }
            );
        }

        // =====================================================
        // Create poll: admin only
        // =====================================================

        [Authorize(Roles = "Admin")]
        public IActionResult Create(PollType? type)
        {
            // The form opens with the type of the page the admin came from
            return View(new CreatePollViewModel { Type = type ?? PollType.Community });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(25_000_000)] // up to 10 choice photos + the poll photo
        public async Task<IActionResult> Create(CreatePollViewModel model)
        {
            var isAwards = model.Type == PollType.Awards;

            // Clean the rows: trim, drop empty ones, drop duplicates (ignoring case)
            var rows = model
                .Choices.Select(c => new { Text = (c.Text ?? "").Trim(), c.Image })
                .Where(c => c.Text.Length > 0)
                .GroupBy(c => c.Text, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            if (rows.Count < 2)
                ModelState.AddModelError(
                    nameof(model.Choices),
                    "Enter at least 2 different choices."
                );
            else if (rows.Count > 10)
                ModelState.AddModelError(
                    nameof(model.Choices),
                    "A poll can have at most 10 choices."
                );
            else if (rows.Any(r => r.Text.Length > 200))
                ModelState.AddModelError(
                    nameof(model.Choices),
                    "Each choice can have at most 200 characters."
                );

            // Choice photos exist only in Awards polls (the server decides, not the page)
            if (isAwards)
            {
                foreach (var row in rows)
                {
                    if (row.Image == null || row.Image.Length == 0)
                        continue;

                    if (row.Image.Length > MaxImageBytes)
                        ModelState.AddModelError(
                            nameof(model.Choices),
                            $"Choice \"{row.Text}\": the photo must be 2 MB or smaller."
                        );
                    else if (!await IsValidImageAsync(row.Image))
                        ModelState.AddModelError(
                            nameof(model.Choices),
                            $"Choice \"{row.Text}\": please upload a JPG, PNG or WEBP image."
                        );
                }
            }

            // Dates: the browser sends local time, the database stores UTC
            var offset = Math.Clamp(model.TimezoneOffset, -840, 840);
            DateTime? startUtc = model.StartDate.HasValue
                ? ToUtc(model.StartDate.Value, offset)
                : null;
            DateTime? endUtc = model.EndDate.HasValue ? ToUtc(model.EndDate.Value, offset) : null;

            if (endUtc.HasValue && endUtc <= DateTime.UtcNow.AddMinutes(1))
                ModelState.AddModelError(
                    nameof(model.EndDate),
                    "The end date must be in the future."
                );
            else if (startUtc.HasValue && endUtc.HasValue && endUtc <= startUtc)
                ModelState.AddModelError(
                    nameof(model.EndDate),
                    "The end date must be after the start date."
                );

            // The poll photo is optional, but if there is one it must be a real, small image
            if (model.Image != null && model.Image.Length > 0)
            {
                if (model.Image.Length > MaxImageBytes)
                    ModelState.AddModelError(
                        nameof(model.Image),
                        "The photo must be 2 MB or smaller."
                    );
                else if (!await IsValidImageAsync(model.Image))
                    ModelState.AddModelError(
                        nameof(model.Image),
                        "Please upload a JPG, PNG or WEBP image."
                    );
            }

            if (!ModelState.IsValid)
                return View(model);

            var savedFiles = new List<string>();
            try
            {
                string? imagePath = null;
                if (model.Image != null && model.Image.Length > 0)
                {
                    imagePath = await SaveImageAsync(model.Image, "polls");
                    savedFiles.Add(imagePath);
                }

                var choices = new List<Choice>();
                foreach (var row in rows)
                {
                    string? choicePath = null;
                    if (isAwards && row.Image != null && row.Image.Length > 0)
                    {
                        choicePath = await SaveImageAsync(row.Image, "choices");
                        savedFiles.Add(choicePath);
                    }
                    choices.Add(new Choice { Text = row.Text, ImagePath = choicePath });
                }

                var poll = new Poll
                {
                    Title = model.Title.Trim(),
                    Description = model.Description?.Trim() ?? "",
                    Type = model.Type!.Value,
                    StartDate = startUtc!.Value,
                    EndDate = endUtc!.Value,
                    ImagePath = imagePath,
                    CreatorId = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    Choices = choices,
                };

                _db.Polls.Add(poll);
                await _db.SaveChangesAsync(); // saves the poll AND its choices together

                TempData["Success"] = $"Poll \"{poll.Title}\" was created.";
                return poll.Type == PollType.Awards
                    ? RedirectToAction(nameof(Awards))
                    : RedirectToAction(nameof(Community));
            }
            catch
            {
                foreach (var path in savedFiles) // do not leave orphan files if something failed
                    DeleteImage(path);
                throw;
            }
        }

        // =====================================================
        // Edit poll: admin only, and only polls created by this admin
        // =====================================================

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var poll = await _db.Polls.Include(p => p.Choices).FirstOrDefaultAsync(p => p.Id == id);
            if (poll == null)
                return NotFound();

            if (poll.CreatorId != User.FindFirstValue(ClaimTypes.NameIdentifier))
                return Forbid(); // goes to the Access denied page

            var model = new EditPollViewModel
            {
                Id = poll.Id,
                Title = poll.Title,
                Description = poll.Description,
            };
            FillDisplay(model, poll, await _db.Votes.CountAsync(v => v.PollId == id));
            return View(model);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(3_000_000)]
        public async Task<IActionResult> Edit(EditPollViewModel model)
        {
            // Everything important is reloaded from the database, never trusted from the form
            var poll = await _db
                .Polls.Include(p => p.Choices)
                .FirstOrDefaultAsync(p => p.Id == model.Id);
            if (poll == null)
                return NotFound();

            if (poll.CreatorId != User.FindFirstValue(ClaimTypes.NameIdentifier))
                return Forbid();

            var now = DateTime.UtcNow;
            var started = poll.StartDate <= now;
            var offset = Math.Clamp(model.TimezoneOffset, -840, 840);

            // The start date can change only before the poll starts
            var startUtc =
                (!started && model.StartDate.HasValue)
                    ? ToUtc(model.StartDate.Value, offset)
                    : poll.StartDate;
            DateTime? endUtc = model.EndDate.HasValue ? ToUtc(model.EndDate.Value, offset) : null;

            if (endUtc.HasValue && endUtc <= now.AddMinutes(1))
                ModelState.AddModelError(
                    nameof(model.EndDate),
                    "The end date must be in the future."
                );
            else if (endUtc.HasValue && endUtc <= startUtc)
                ModelState.AddModelError(
                    nameof(model.EndDate),
                    "The end date must be after the start date."
                );

            if (model.Image != null && model.Image.Length > 0)
            {
                if (model.Image.Length > MaxImageBytes)
                    ModelState.AddModelError(
                        nameof(model.Image),
                        "The photo must be 2 MB or smaller."
                    );
                else if (!await IsValidImageAsync(model.Image))
                    ModelState.AddModelError(
                        nameof(model.Image),
                        "Please upload a JPG, PNG or WEBP image."
                    );
            }

            if (!ModelState.IsValid)
            {
                FillDisplay(model, poll, await _db.Votes.CountAsync(v => v.PollId == poll.Id));
                return View(model);
            }

            var oldImage = poll.ImagePath;
            string? newImage = null;
            try
            {
                if (model.Image != null && model.Image.Length > 0)
                {
                    newImage = await SaveImageAsync(model.Image, "polls"); // see the note below
                    poll.ImagePath = newImage;
                }
                else if (model.RemoveImage)
                {
                    poll.ImagePath = null;
                }

                poll.Title = model.Title.Trim();
                poll.Description = model.Description?.Trim() ?? "";
                poll.StartDate = startUtc;
                poll.EndDate = endUtc!.Value;

                await _db.SaveChangesAsync();
            }
            catch
            {
                DeleteImage(newImage); // do not leave an orphan file if the save failed
                throw;
            }

            // The old photo is deleted only AFTER the save worked
            if (oldImage != null && oldImage != poll.ImagePath)
                DeleteImage(oldImage);

            TempData["Success"] = $"Poll \"{poll.Title}\" was updated.";
            return poll.Type == PollType.Awards
                ? RedirectToAction(nameof(Awards))
                : RedirectToAction(nameof(Community));
        }

        // =====================================================
        // Helpers
        // =====================================================

        // The browser's getTimezoneOffset() = UTC minus local, in minutes
        private static DateTime ToUtc(DateTime local, int offsetMinutes) =>
            DateTime.SpecifyKind(local.AddMinutes(offsetMinutes), DateTimeKind.Utc);

        private static void FillDisplay(EditPollViewModel m, Poll poll, int votes)
        {
            m.Type = poll.Type;
            m.CurrentImagePath = poll.ImagePath;
            m.StartUtc = poll.StartDate.ToString("o");
            m.EndUtc = poll.EndDate.ToString("o");
            m.Started = poll.StartDate <= DateTime.UtcNow;
            m.ChoiceCount = poll.Choices.Count;
            m.VoteCount = votes;
        }

        // Checks the extension AND the first bytes of the file (the real format)
        private static async Task<bool> IsValidImageAsync(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                return false;

            var h = new byte[12];
            await using var stream = file.OpenReadStream();
            if (await stream.ReadAsync(h, 0, h.Length) < h.Length)
                return false;

            var jpeg = h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF;
            var png = h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47;
            var webp =
                h[0] == 'R'
                && h[1] == 'I'
                && h[2] == 'F'
                && h[3] == 'F'
                && h[8] == 'W'
                && h[9] == 'E'
                && h[10] == 'B'
                && h[11] == 'P';
            return jpeg || png || webp;
        }

        private async Task<string> SaveImageAsync(IFormFile file, string folderName)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{ext}"; // never trust the original name
            var folder = Path.Combine(_env.WebRootPath, "uploads", folderName);
            Directory.CreateDirectory(folder);

            await using var stream = new FileStream(
                Path.Combine(folder, fileName),
                FileMode.Create
            );
            await file.CopyToAsync(stream);

            return $"/uploads/{folderName}/{fileName}";
        }

        private void DeleteImage(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            var full = Path.Combine(
                _env.WebRootPath,
                path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
            );
            if (System.IO.File.Exists(full))
                System.IO.File.Delete(full);
        }
    }
}

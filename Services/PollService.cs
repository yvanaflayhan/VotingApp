using Microsoft.EntityFrameworkCore;
using Voting_app.Common;
using Voting_app.Data;
using Voting_app.Models;

namespace Voting_app.Services
{
    public interface IPollService
    {
        Task<DataTableResponse<PollListItem>> GetListAsync(
            PollTableRequest request,
            string? userId
        );
        Task<PollDetailsViewModel?> GetDetailsAsync(int id, string? userId, bool isAdmin);

        Task<ServiceResult> CreateAsync(CreatePollViewModel model, string userId, bool isAdmin);

        Task<(ServiceResult Result, EditPollViewModel? Model)> GetForEditAsync(
            int id,
            string userId
        );
        Task<ServiceResult> UpdateAsync(EditPollViewModel model, string userId, bool formIsValid);

        Task<ServiceResult> DeleteAsync(int id, string userId, bool isAdmin);
    }

    public class PollService : IPollService
    {
        private const string PollsFolder = "polls";
        private const string ChoicesFolder = "choices";
        private const int MinChoices = 2;
        private const int MaxChoices = 10;
        private const int MaxChoiceLength = 200;

        private const string ChoicesField = nameof(CreatePollViewModel.Choices);
        private const string EndDateField = nameof(CreatePollViewModel.EndDate);
        private const string ImageField = nameof(CreatePollViewModel.Image);

        private readonly ApplicationDbContext _db;
        private readonly IImageService _images;

        public PollService(ApplicationDbContext db, IImageService images)
        {
            _db = db;
            _images = images;
        }
        private record ChoiceRow(string Text, IFormFile? Image);

        private record ChoiceChange(Choice Target, IFormFile? Image, bool Remove);

        #region Reading

        public async Task<DataTableResponse<PollListItem>> GetListAsync(
            PollTableRequest request,
            string? userId
        )
        {
            var now = DateTime.UtcNow;

            var query = _db.Polls.AsNoTracking().Where(p => p.IsActive && p.Type == request.Type);

            // "My polls": the user id comes from the cookie, never from the request
            if (request.Mine)
                query = query.Where(p => p.CreatorId == userId);

            var total = await query.CountAsync();

            if (request.SearchPattern is { } term)
                query = query.Where(p =>
                    EF.Functions.ILike(p.Title, term) || EF.Functions.ILike(p.Description, term)
                );

            var filtered = await query.CountAsync();

            var polls = await query
                .Include(p => p.Choices)
                .OrderByDescending(p => p.CreatedAt)
                .ThenBy(p => p.Id) // a stable order, needed for paging
                .Skip(request.SafeStart)
                .Take(request.SafeLength)
                .ToListAsync();

            // Two queries for the whole page, never one query per poll
            var pollIds = polls.Select(p => p.Id).ToList();
            var votesByChoice = await _db.Votes.CountByChoiceAsync(pollIds);
            var myVotes =
                userId == null
                    ? new Dictionary<int, int>()
                    : await _db
                        .Votes.Where(v => v.UserId == userId && pollIds.Contains(v.PollId))
                        .ToDictionaryAsync(v => v.PollId, v => v.ChoiceId);

            return new DataTableResponse<PollListItem>
            {
                Draw = request.Draw,
                RecordsTotal = total,
                RecordsFiltered = filtered,
                Data = polls
                    .Select(p => ToListItem(p, now, userId, votesByChoice, myVotes))
                    .ToList(),
            };
        }

        // Everybody can open the details. Only the admin can see inactive polls.
        public async Task<PollDetailsViewModel?> GetDetailsAsync(
            int id,
            string? userId,
            bool isAdmin
        )
        {
            var poll = await _db
                .Polls.AsNoTracking()
                .Include(p => p.Choices)
                .Include(p => p.Creator)
                .FirstOrDefaultAsync(p => p.Id == id && (p.IsActive || isAdmin));
            if (poll == null)
                return null;

            var options = PollResults.Build(
                poll.Choices,
                await _db.Votes.CountByChoiceAsync(new List<int> { id })
            );

            // The choice this user voted for (null for guests and for users who have not voted)
            int? myChoiceId =
                userId == null
                    ? null
                    : await _db
                        .Votes.Where(v => v.PollId == id && v.UserId == userId)
                        .Select(v => (int?)v.ChoiceId)
                        .FirstOrDefaultAsync();

            return new PollDetailsViewModel
            {
                Id = poll.Id,
                Title = poll.Title,
                Description = poll.Description,
                Type = poll.Type,
                ImagePath = poll.ImagePath,
                StartDate = poll.StartDate,
                EndDate = poll.EndDate,
                Status = PollStatus.Of(poll, DateTime.UtcNow),
                CreatorName = poll.Creator?.UserName ?? "",
                TotalVotes = options.Sum(o => o.Votes),
                IsAuthenticated = userId != null,
                MyChoiceId = myChoiceId,
                Choices = options,
            };
        }

        public async Task<(ServiceResult Result, EditPollViewModel? Model)> GetForEditAsync(
            int id,
            string userId
        )
        {
            var poll = await _db.Polls.Include(p => p.Choices).FirstOrDefaultAsync(p => p.Id == id);
            if (poll == null)
                return (ServiceResult.NotFound(), null);

            // Ownership: you can edit only what you created
            if (poll.CreatorId != userId)
                return (ServiceResult.Forbidden(), null);

            var model = new EditPollViewModel
            {
                Id = poll.Id,
                Title = poll.Title,
                Description = poll.Description,
            };
            await FillEditDisplayAsync(model, poll);
            return (ServiceResult.Ok(), model);
        }

        #endregion

        #region Writing

        public async Task<ServiceResult> CreateAsync(
            CreatePollViewModel model,
            string userId,
            bool isAdmin
        )
        {
            // Users create Community polls only. The server decides, not the page.
            var type = isAdmin ? model.Type!.Value : PollType.Community;

            var rows = CleanChoices(model.Choices);
            var offset = ClampOffset(model.TimezoneOffset);
            var startUtc = ToUtc(model.StartDate!.Value, offset);
            var endUtc = ToUtc(model.EndDate!.Value, offset);

            var errors = ValidateDates(startUtc, endUtc, DateTime.UtcNow);
            errors.AddIfNotNull(ValidateChoiceCount(rows));

            // Choice photos exist only in Awards polls
            if (type == PollType.Awards)
                foreach (var row in rows)
                    errors.AddIfNotNull(
                        await ValidateImageAsync(
                            row.Image,
                            ChoicesField,
                            $"Choice \"{row.Text}\": "
                        )
                    );

            errors.AddIfNotNull(await ValidateImageAsync(model.Image, ImageField));

            if (errors.Count > 0)
                return ServiceResult.Invalid(errors);

            var uploads = new UploadedFiles(_images);
            try
            {
                var imagePath = model.Image.HasContent()
                    ? await uploads.SaveAsync(model.Image, PollsFolder)
                    : null;

                var choices = new List<Choice>();
                foreach (var row in rows)
                {
                    var photo =
                        type == PollType.Awards && row.Image.HasContent()
                            ? await uploads.SaveAsync(row.Image, ChoicesFolder)
                            : null;
                    choices.Add(new Choice { Text = row.Text, ImagePath = photo });
                }

                var poll = new Poll
                {
                    Title = model.Title.Trim(),
                    Description = model.Description?.Trim() ?? "",
                    Type = type,
                    StartDate = startUtc,
                    EndDate = endUtc,
                    ImagePath = imagePath,
                    CreatorId = userId,
                    Choices = choices,
                };

                _db.Polls.Add(poll);
                await _db.SaveChangesAsync(); // saves the poll AND its choices together

                return ServiceResult.Ok($"Poll \"{poll.Title}\" was created.");
            }
            catch
            {
                uploads.DeleteAll(); // no orphan files if something failed
                throw;
            }
        }

        public async Task<ServiceResult> UpdateAsync(
            EditPollViewModel model,
            string userId,
            bool formIsValid
        )
        {
            // Everything important is reloaded from the database, never trusted from the form
            var poll = await _db
                .Polls.Include(p => p.Choices)
                .FirstOrDefaultAsync(p => p.Id == model.Id);
            if (poll == null)
                return ServiceResult.NotFound();
            if (poll.CreatorId != userId)
                return ServiceResult.Forbidden();

            if (!formIsValid)
            {
                await FillEditDisplayAsync(model, poll);
                return ServiceResult.Invalid(new List<FieldError>());
            }

            var now = DateTime.UtcNow;
            var offset = ClampOffset(model.TimezoneOffset);

            // The start date can change only before the poll starts
            var startUtc =
                poll.StartDate > now && model.StartDate is { } start
                    ? ToUtc(start, offset)
                    : poll.StartDate;
            var endUtc = ToUtc(model.EndDate!.Value, offset);

            var errors = ValidateDates(startUtc, endUtc, now);
            errors.AddIfNotNull(await ValidateImageAsync(model.Image, ImageField));

            // Choice photos (Awards only). Each posted row is matched to a REAL choice of this poll, by id.
            var changes = new List<ChoiceChange>();
            if (poll.Type == PollType.Awards)
            {
                foreach (var posted in model.Choices)
                {
                    var choice = poll.Choices.FirstOrDefault(c => c.Id == posted.Id);
                    if (choice == null)
                        continue; // an id that is not in this poll: ignored

                    errors.AddIfNotNull(
                        await ValidateImageAsync(posted.Image, "", $"Choice \"{choice.Text}\": ")
                    );
                    changes.Add(new ChoiceChange(choice, posted.Image, posted.RemoveImage));
                }
            }

            if (errors.Count > 0)
            {
                await FillEditDisplayAsync(model, poll);
                return ServiceResult.Invalid(errors);
            }

            var uploads = new UploadedFiles(_images);
            var replacedFiles = new List<string?>(); // deleted only AFTER the save worked
            try
            {
                if (model.Image.HasContent())
                {
                    replacedFiles.Add(poll.ImagePath);
                    poll.ImagePath = await uploads.SaveAsync(model.Image, PollsFolder);
                }
                else if (model.RemoveImage)
                {
                    replacedFiles.Add(poll.ImagePath);
                    poll.ImagePath = null;
                }

                foreach (var (target, image, remove) in changes)
                {
                    if (image.HasContent())
                    {
                        replacedFiles.Add(target.ImagePath);
                        target.ImagePath = await uploads.SaveAsync(image, ChoicesFolder);
                    }
                    else if (remove)
                    {
                        replacedFiles.Add(target.ImagePath);
                        target.ImagePath = null;
                    }
                }

                poll.Title = model.Title.Trim();
                poll.Description = model.Description?.Trim() ?? "";
                poll.StartDate = startUtc;
                poll.EndDate = endUtc;

                await _db.SaveChangesAsync();
            }
            catch
            {
                uploads.DeleteAll();
                throw;
            }

            foreach (var file in replacedFiles)
                _images.Delete(file);

            model.Type = poll.Type; // the controller uses it to choose where to go
            return ServiceResult.Ok($"Poll \"{poll.Title}\" was updated.");
        }

        public async Task<ServiceResult> DeleteAsync(int id, string userId, bool isAdmin)
        {
            var poll = await _db.Polls.Include(p => p.Choices).FirstOrDefaultAsync(p => p.Id == id);
            if (poll == null)
                return ServiceResult.NotFound("This poll no longer exists.");

            // The admin can delete any poll. A user can delete only their own.
            if (!isAdmin && poll.CreatorId != userId)
                return ServiceResult.Forbidden("You can only delete your own polls.");

            var title = poll.Title;
            var files = poll.Choices.Select(c => c.ImagePath).Append(poll.ImagePath).ToList();

            // A vote points to its choice with Restrict, so the votes go FIRST, then the poll
            // (its choices disappear with it, by cascade). All or nothing, in one transaction.
            await using var transaction = await _db.Database.BeginTransactionAsync();
            await _db.Votes.Where(v => v.PollId == id).ExecuteDeleteAsync();
            _db.Polls.Remove(poll);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            // The files are deleted only after the database work succeeded
            foreach (var file in files)
                _images.Delete(file);

            return ServiceResult.Ok($"Poll \"{title}\" was deleted.");
        }

        #endregion

        #region Private methods

        private static PollListItem ToListItem(
            Poll poll,
            DateTime now,
            string? userId,
            Dictionary<int, int> votesByChoice,
            Dictionary<int, int> myVotes
        )
        {
            var options = PollResults.Build(poll.Choices, votesByChoice);

            return new PollListItem
            {
                Id = poll.Id,
                IsMine = userId != null && poll.CreatorId == userId,
                Title = poll.Title,
                Description = poll.Description,
                ImageUrl = poll.ImagePath,
                Choices = options.Count,
                Votes = options.Sum(o => o.Votes),
                StartDate = poll.StartDate,
                EndDate = poll.EndDate,
                Status = PollStatus.Of(poll, now),
                MyChoiceId = myVotes.TryGetValue(poll.Id, out var mine) ? mine : null,
                Options = options,
            };
        }

        // Fills the fields the edit page only DISPLAYS, always from the database
        private async Task FillEditDisplayAsync(EditPollViewModel model, Poll poll)
        {
            model.Type = poll.Type;
            model.CurrentImagePath = poll.ImagePath;
            model.StartUtc = poll.StartDate.ToString("o");
            model.EndUtc = poll.EndDate.ToString("o");
            model.Started = poll.StartDate <= DateTime.UtcNow;
            model.ChoiceCount = poll.Choices.Count;
            model.VoteCount = await _db.Votes.CountAsync(v => v.PollId == poll.Id);
            model.Choices = poll
                .Choices.OrderBy(c => c.Id)
                .Select(c => new EditChoiceInput
                {
                    Id = c.Id,
                    Text = c.Text,
                    CurrentImagePath = c.ImagePath,
                })
                .ToList();
        }

        // Trim, drop empty rows, drop duplicates (ignoring case)
        private static List<ChoiceRow> CleanChoices(IEnumerable<ChoiceInput> inputs) =>
            inputs
                .Select(c => new ChoiceRow((c.Text ?? "").Trim(), c.Image))
                .Where(r => r.Text.Length > 0)
                .GroupBy(r => r.Text, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

        private static FieldError? ValidateChoiceCount(List<ChoiceRow> rows)
        {
            if (rows.Count < MinChoices)
                return new FieldError(
                    ChoicesField,
                    $"Enter at least {MinChoices} different choices."
                );
            if (rows.Count > MaxChoices)
                return new FieldError(
                    ChoicesField,
                    $"A poll can have at most {MaxChoices} choices."
                );
            if (rows.Any(r => r.Text.Length > MaxChoiceLength))
                return new FieldError(
                    ChoicesField,
                    $"Each choice can have at most {MaxChoiceLength} characters."
                );
            return null;
        }

        private static List<FieldError> ValidateDates(
            DateTime startUtc,
            DateTime endUtc,
            DateTime now
        )
        {
            var errors = new List<FieldError>();

            if (endUtc <= now.AddMinutes(1))
                errors.Add(new FieldError(EndDateField, "The end date must be in the future."));
            else if (endUtc <= startUtc)
                errors.Add(
                    new FieldError(EndDateField, "The end date must be after the start date.")
                );

            return errors;
        }

        private async Task<FieldError?> ValidateImageAsync(
            IFormFile? file,
            string field,
            string prefix = ""
        )
        {
            if (!file.HasContent())
                return null;

            var problem = await _images.ValidateAsync(file);
            return problem == null ? null : new FieldError(field, prefix + problem);
        }

        // The browser sends its local time and its offset. The database stores UTC.
        private static DateTime ToUtc(DateTime local, int offsetMinutes) =>
            DateTime.SpecifyKind(local.AddMinutes(offsetMinutes), DateTimeKind.Utc);

        private static int ClampOffset(int offsetMinutes) => Math.Clamp(offsetMinutes, -840, 840);

        // Remembers the files saved during one request, so a failure can delete them all
        private sealed class UploadedFiles
        {
            private readonly IImageService _images;
            private readonly List<string> _paths = new();

            public UploadedFiles(IImageService images)
            {
                _images = images;
            }

            public async Task<string> SaveAsync(IFormFile file, string folder)
            {
                var path = await _images.SaveAsync(file, folder);
                _paths.Add(path);
                return path;
            }

            public void DeleteAll()
            {
                foreach (var path in _paths)
                    _images.Delete(path);
            }
        }

        #endregion
    }
}

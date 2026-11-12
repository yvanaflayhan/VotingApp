using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Voting_app.Models;
using Voting_app.Services;

namespace Voting_app.Controllers
{
    public class PollsController : BaseController
    {
        private readonly IPollService _polls;
        private readonly IVoteService _votes;

        public PollsController(IPollService polls, IVoteService votes)
        {
            _polls = polls;
            _votes = votes;
        }

        #region Public actions


        public IActionResult Community() => ShowPolls(PollType.Community);

        public IActionResult Awards() => ShowPolls(PollType.Awards);

        // "My polls" is for users. The admin manages polls from the tables.
        [Authorize]
        public IActionResult Mine()
        {
            if (IsAdmin)
                return RedirectToAction(nameof(Community));

            ViewBag.Type = PollType.Community;
            ViewBag.Mine = true;
            return View("Cards");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> List([FromForm] PollTableRequest request)
        {
            if (request.Mine && CurrentUserId == null)
                return Forbid();

            return Json(await _polls.GetListAsync(request, CurrentUserId));
        }

        public async Task<IActionResult> Details(int id)
        {
            var model = await _polls.GetDetailsAsync(id, CurrentUserId, IsAdmin);
            return model == null ? NotFound() : View(model);
        }

        //Voting
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Vote(int pollId, int choiceId) =>
            ToJson(await _votes.VoteAsync(pollId, choiceId, CurrentUserId!, IsAdmin));


        //Create
        [Authorize]
        public IActionResult Create(PollType? type)
        {
            // Users always get Community. Only the admin can pick the type.
            var chosen = IsAdmin ? type ?? PollType.Community : PollType.Community;
            return View(new CreatePollViewModel { Type = chosen });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(25_000_000)] // the poll photo + up to 10 choice photos
        public async Task<IActionResult> Create(CreatePollViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var result = await _polls.CreateAsync(model, CurrentUserId!, IsAdmin);
            if (!result.Success)
            {
                AddErrors(result);
                return View(model);
            }

            TempData["Success"] = result.Message;
            return RedirectAfterSave(model.Type ?? PollType.Community);
        }

        //Edit: only the person who created the poll
        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var (result, model) = await _polls.GetForEditAsync(id, CurrentUserId!);

            return result.Status switch
            {
                ResultStatus.NotFound => NotFound(),
                ResultStatus.Forbidden => Forbid(),
                _ => View(model),
            };
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(25_000_000)]
        public async Task<IActionResult> Edit(EditPollViewModel model)
        {
            var result = await _polls.UpdateAsync(model, CurrentUserId!, ModelState.IsValid);

            if (result.Status == ResultStatus.NotFound)
                return NotFound();
            if (result.Status == ResultStatus.Forbidden)
                return Forbid();
            if (!result.Success)
            {
                AddErrors(result);
                return View(model);
            }

            TempData["Success"] = result.Message;
            return RedirectAfterSave(model.Type);
        }

        //Delete: the admin any poll, a user only their own

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id) =>
            ToJson(await _polls.DeleteAsync(id, CurrentUserId!, IsAdmin));

        #endregion

        #region Private methods

        // The admin sees the management table, everybody else sees cards
        private IActionResult ShowPolls(PollType type)
        {
            ViewBag.Type = type;
            return View(IsAdmin ? "Index" : "Cards");
        }

        // Users go to "My polls". The admin goes back to the list of the poll's type.
        private IActionResult RedirectAfterSave(PollType type) =>
            !IsAdmin
                ? RedirectToAction(nameof(Mine))
                : RedirectToAction(type == PollType.Awards ? nameof(Awards) : nameof(Community));

        #endregion
    }
}

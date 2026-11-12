using Microsoft.AspNetCore.Mvc;
using Voting_app.Common;
using Voting_app.Services;

namespace Voting_app.Controllers
{
    public abstract class BaseController : Controller
    {
        protected string? CurrentUserId => User.GetUserId();
        protected bool IsAdmin => User.IsAdmin();
        protected void AddErrors(ServiceResult result)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Field, error.Message);

            if (result.Errors.Count == 0 && result.Message.Length > 0)
                ModelState.AddModelError("", result.Message);
        }

        // The JSON answer for AJAX calls: the HTTP status follows the result
        protected IActionResult ToJson(ServiceResult result) =>
            result.Status switch
            {
                ResultStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result),
                ResultStatus.NotFound => NotFound(result),
                _ => Json(result),
            };
    }
}

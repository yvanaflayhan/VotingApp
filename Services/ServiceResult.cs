namespace Voting_app.Services
{
    public enum ResultStatus
    {
        Ok,
        Invalid,
        NotFound,
        Forbidden,
    }
    public record FieldError(string Field, string Message);

    public class ServiceResult
    {
        public ResultStatus Status { get; init; } = ResultStatus.Ok;
        public string Message { get; init; } = "";
        public List<FieldError> Errors { get; init; } = new();

        public bool Success => Status == ResultStatus.Ok;

        public static ServiceResult Ok(string message = "") => new() { Message = message };

        public static ServiceResult Invalid(string message) =>
            new() { Status = ResultStatus.Invalid, Message = message };

        public static ServiceResult Invalid(List<FieldError> errors) =>
            new() { Status = ResultStatus.Invalid, Errors = errors };

        public static ServiceResult NotFound(string message = "Not found.") =>
            new() { Status = ResultStatus.NotFound, Message = message };

        public static ServiceResult Forbidden(string message = "You are not allowed to do this.") =>
            new() { Status = ResultStatus.Forbidden, Message = message };
    }
}

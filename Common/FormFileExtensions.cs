using System.Diagnostics.CodeAnalysis;

namespace Voting_app.Common
{
    public static class FormFileExtensions//did the user really upload a photo?
    {
        public static bool HasContent([NotNullWhen(true)] this IFormFile? file) =>
            file is { Length: > 0 };
    }
}

namespace Voting_app.Common
{
    public static class ListExtensions //add to the list only if there is something
    {
        public static void AddIfNotNull<T>(this List<T> list, T? item)
            where T : class
        {
            if (item != null)
                list.Add(item);
        }
        //AddIfNotNull means “if this is an error, keep it, and if it’s nothing, ignore it”
    }
}

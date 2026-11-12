namespace Voting_app.Models
{
    // What DataTables sends for every page and every search
    public class DataTableRequest
    {
        public int Draw { get; set; }
        public int Start { get; set; } // how many rows to skip
        public int Length { get; set; } = 20; // page size
        public string? Search { get; set; }
        public int SafeStart => Math.Max(0, Start);
        public int SafeLength => Math.Clamp(Length, 1, 100);
        public string? SearchPattern =>
            string.IsNullOrWhiteSpace(Search) ? null : $"%{Search.Trim()}%";
    }
    public class DataTableResponse<T>
    {
        public int Draw { get; set; }
        public int RecordsTotal { get; set; }
        public int RecordsFiltered { get; set; }
        public List<T> Data { get; set; } = new();
    }
}

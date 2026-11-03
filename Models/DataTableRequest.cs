namespace Voting_app.Models
{
    public class DataTableRequest
    {
        public int Draw { get; set;}
        public int Start {get; set;} //how many rows to skip
        public int Length {get; set;} = 20; //page size 
        public string? Search {get; set;}
        public string? Column {get; set;} //which column to sort by 
        public string? Sort {get; set;} //"asc" or "desc"


    }
}
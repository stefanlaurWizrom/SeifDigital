using System.Collections.Generic;

namespace SeifDigital.Models
{
    public class InformatieSensibilaListViewModel
    {
        public List<InformatieSensibila> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }

        public int StartItemIndex => (TotalCount == 0) ? 0 : ((Page - 1) * PageSize + 1);
        public int EndItemIndex => (TotalCount == 0) ? 0 : System.Math.Min(Page * PageSize, TotalCount);
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http.Filters;
using AdminRole.Controllers;

namespace AdminRole.Models
{
    public class SearchParameters
    {
        public const int DefaultPageSize = 20;

        public SearchParameters()
        {
            GlavneSpremenljivke = new Dictionary<string, string>();
            PageSize = DefaultPageSize;
            PageIndex = 1;
        }

        public string Version { get; set; }
        public string Breadcrumbid { get; set; }
        public string Sequ { get; set; }
        public string ExtId { get; set; }
        public string MsgT { get; set; }
        public string Sender { get; set; }
        public string Destination { get; set; }
        public string Customer { get; set; }
        public string Agent { get; set; }
        public string Order { get; set; }
        public string Line { get; set; }
        public string Vessel { get; set; }
        public string Voyage { get; set; }

        public DateTime? Date { get; set; }
        public DateTime? Date2 { get; set; }
        public string Reference { get; set; }
        public DateTime? Exchangetimestamp { get; set; }
        public DateTime? Exchangetimestamp2 { get; set; }
        public string MsgS { get; set; }
        public string Internal { get; set; }

        public string Data { get; set; }

        public string FreeSearch { get; set; }

        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public IDictionary<string, string> GlavneSpremenljivke { get; set; }
        public string Sort { get; set; }

        public int FirstItemIndex
        {
            get { return (PageIndex - 1)*PageSize; }
        }

        public int LastItemIndex
        {
            get { return FirstItemIndex + PageSize; }
        }

    }
}
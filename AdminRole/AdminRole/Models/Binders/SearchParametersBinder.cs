using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Text.RegularExpressions;
using AdminRole.Helpers;

namespace AdminRole.Models.Binders
{
    public class SearchParametersBinder : IModelBinder
    {
        public const int DefaultPageSize = SearchParameters.DefaultPageSize;

        public IDictionary<string, string> NvToDict(NameValueCollection nv)
        {
            var d = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var k in nv.AllKeys)
                d[k] = nv[k];
            return d;
        }

        private static readonly Regex FacetRegex = new Regex("^f_", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public object BindModel(ControllerContext controllerContext, ModelBindingContext bindingContext)
        {
            var qs = controllerContext.HttpContext.Request.QueryString;
            var qsDict = NvToDict(qs);
            var sp = new SearchParameters
            {
                FreeSearch = StringHelper.EmptyToNull(qs["q"]),
                PageIndex = StringHelper.TryParse(qs["page"], 1),
                PageSize = StringHelper.TryParse(qs["pageSize"], DefaultPageSize),
                Sort = StringHelper.EmptyToNull(qs["sort"]),

                Sequ = StringHelper.EmptyToNull(qs["sequentialid"]),
                ExtId = StringHelper.EmptyToNull(qs["external_id"]),
                MsgT = StringHelper.EmptyToNull(qs["msgtype"]),
                Sender = StringHelper.EmptyToNull(qs["sender"]),
                Destination = StringHelper.EmptyToNull(qs["destination"]),
                Customer = StringHelper.EmptyToNull(qs["customer"]),
                Agent = StringHelper.EmptyToNull(qs["agent"]),
                Order = StringHelper.EmptyToNull(qs["order"]),
                Line = StringHelper.EmptyToNull(qs["line"]),
                Vessel = StringHelper.EmptyToNull(qs["vessel"]),
                Voyage = StringHelper.EmptyToNull(qs["voyage"]),
                Reference = StringHelper.EmptyToNull(qs["reference"]),
                //Exchangetimestamp = StringHelper.EmptyToNull(qs["exchangetimestamp"]),
                MsgS = StringHelper.EmptyToNull(qs["msg_state"]),
                Internal = StringHelper.EmptyToNull(qs["internal"]),

                GlavneSpremenljivke = qsDict.Where(k => FacetRegex.IsMatch(k.Key))
                .Select(k => k.WithKey(FacetRegex.Replace(k.Key, "")))
                .ToDictionary()
            };

            DateTime date1;
            if (DateTime.TryParse(qs["date"], out date1))
                sp.Date = date1;
            DateTime date2;
            if (DateTime.TryParse(qs["date2"], out date2))
                sp.Date2 = date2;
            DateTime exchangetimestamp1;
            if (DateTime.TryParse(qs["exchangetimestamp"], out exchangetimestamp1))
                sp.Exchangetimestamp = exchangetimestamp1;
            DateTime exchangetimestamp2;
            if (DateTime.TryParse(qs["exchangetimestamp2"], out exchangetimestamp2))
                sp.Exchangetimestamp2 = exchangetimestamp2;
            return sp;
        }
    }
}
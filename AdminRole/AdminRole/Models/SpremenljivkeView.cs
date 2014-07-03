using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace AdminRole.Models
{
    public class SpremenljivkeView
    {
        public SearchParameters Search { get; set; }

        #region Public properties

        public UsersTable User { get; set; }

        public List<CustomType> CustomTypes { get; set; }

        public List<SelectListItem> GetCustomTypes(int id)
        {
            return CustomTypes.Select(o => new SelectListItem
            {
                Text = o.CustomerType,
                Value = o.Id_NewCustomerType.ToString(CultureInfo.InvariantCulture),
                Selected = o.Id_NewCustomerType == id
            })
                .ToList();
        }

        #endregion

        public ICollection<SpremenljivkeSolr> Spremenljivke { get; set; }
        public int TotalCount { get; set; }
        public IDictionary<string, ICollection<KeyValuePair<string, int>>> GlavneSpremenljivke { get; set; } 
        public string AliSiMislil { get; set; }
        public bool QueryError { get; set; }

        public TimeZoneInfo TimeZone { get; set; }

        public SpremenljivkeView()
        {
            Search = new SearchParameters();
            Spremenljivke = new List<SpremenljivkeSolr>();
            GlavneSpremenljivke = new Dictionary<string, ICollection<KeyValuePair<string, int>>>();
            
        }

        
    }
}
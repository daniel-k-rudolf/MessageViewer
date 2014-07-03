using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace AdminRole.Models
{
    public class UserModel
    {
        #region Public properties

        public UsersTable User { get; set; }

        public List<CustomType> CustomTypes { get; set; }

        public string VlogaNaziv { get; set; }

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

    }
}
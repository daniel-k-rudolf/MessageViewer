//------------------------------------------------------------------------------
// <auto-generated>
//    This code was generated from a template.
//
//    Manual changes to this file may cause unexpected behavior in your application.
//    Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace AdminRole.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class UsersTable
    {
        public UsersTable()
        {
            this.CustomerTables = new List<CustomerTable>();
        }
    
        public int userID { get; set; }
        public string username { get; set; }
        public string password { get; set; }
        public int Roles { get; set; }
        public string TimeZoneId { get; set; }
        public string passwordSalt { get; set; }
    
        public virtual IList<CustomerTable> CustomerTables { get; set; }
        public virtual VlogaUporabnika VlogaUporabnika { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace AdminRole.Models
{
    public class AdminRoleDb : DbContext
    {
        public AdminRoleDb()
            : base("name=userDbEntities")
        { }
        public DbSet<Users> UsersEdit { get; set; }
    }
}
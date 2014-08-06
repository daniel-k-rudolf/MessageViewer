using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Web;

namespace AdminRole.Models
{
    public class Users
    {

        public int UserId { get; set; }
        
        [Required(ErrorMessage = "Vnesite uporabniško ime!")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Vnesite geslo!")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public string TimeZoneName { get; set; }
    }
}
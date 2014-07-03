using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace AdminRole.Models
{
    public class CustomerType
    {
        public int IdNewType { get; set; }

        [Required(ErrorMessage = "Customer is required!")]
        public string CustomType { get; set; }

    }
}
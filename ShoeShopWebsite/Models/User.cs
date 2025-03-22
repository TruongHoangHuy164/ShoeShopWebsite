using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ShoeShopWebsite.Models
{
    public class User : IdentityUser
    {
        [Required, StringLength(100)]
        public string FullName { get; set; }

        [StringLength(255)]
        public string Address { get; set; }
    }
}

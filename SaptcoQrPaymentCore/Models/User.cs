using System;
using System.ComponentModel.DataAnnotations;

namespace SaptcoQrPaymentCore.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Phone { get; set; }

        [StringLength(100)]
        public string Name { get; set; }

        [EmailAddress]
        [StringLength(150)]
        public string Email { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}

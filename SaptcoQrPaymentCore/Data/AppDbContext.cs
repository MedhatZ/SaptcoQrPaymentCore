using Microsoft.EntityFrameworkCore;
using SaptcoQrPaymentCore.Models;
using System.Collections.Generic;

namespace SaptcoQrPaymentCore.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
    }
}

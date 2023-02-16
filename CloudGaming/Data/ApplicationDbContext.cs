using CloudGaming.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CloudGaming.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public DbSet<VirtualM> VirtualMs{get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
}
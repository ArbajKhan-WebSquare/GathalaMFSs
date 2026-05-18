using GathalaMFS.Models;
using Microsoft.EntityFrameworkCore;

namespace GathalaMFS.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<StudentDetail> StudentDetails { get; set; }
        public DbSet<ExcelFile> ExcelFiles { get; set; }
        public DbSet<ExcelFileStudent> ExcelFileStudents { get; set; }
    }
}

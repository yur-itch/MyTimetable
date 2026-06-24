using Microsoft.EntityFrameworkCore;
using MyTimetable.Models;

namespace MyTimetable
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Lesson> Lessons { get; set; }
        public DbSet<LessonDeactivation> Deactivations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Lesson>().HasKey(l => new { l.Date, l.LessonNumber, l.isCustom });
            modelBuilder.Entity<LessonDeactivation>().HasKey(l => new { l.Date, l.LessonNumber });
        }
    }
}

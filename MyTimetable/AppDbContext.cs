using Microsoft.EntityFrameworkCore;
using MyTimetable.Entities;
using MyTimetable.Models;

namespace MyTimetable
{
    public sealed class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<DefaultLessonEntry> DefaultLessons { get; set; }
        public DbSet<CustomLessonEntry> CustomLessons { get; set; }
        public DbSet<LessonDeactivation> Deactivations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DefaultLessonEntry>().HasKey(l => new { l.Date, l.LessonNumber });
            modelBuilder.Entity<CustomLessonEntry>().HasKey(l => new { l.Date, l.LessonNumber });
            modelBuilder.Entity<LessonDeactivation>().HasKey(l => new { l.Date, l.Number });
        }
    }
}

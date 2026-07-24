using Microsoft.EntityFrameworkCore.Design;

namespace MyTimetable
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Port=5432;Database=schedule_db;Username=postgres;Password=yurich")
                .Options;
            return new AppDbContext(options);
        }
    }
}

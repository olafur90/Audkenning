using Microsoft.EntityFrameworkCore;

namespace Audkenning
{
    public class AudkenniDbContext(DbContextOptions<AudkenniDbContext> options) : DbContext(options)
    {

        // Define DbSet for each table
        public DbSet<Authentications> Authentication { get; set; }
    }

    public class Authentications
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsAuthenticated { get; set; }
    }
}

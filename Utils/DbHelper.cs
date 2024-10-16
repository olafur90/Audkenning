using Microsoft.EntityFrameworkCore;

namespace Audkenning.Utils
{
    public class DbHelper
    {
        private readonly AudkenniDbContext _context;

        public DbHelper(AudkenniDbContext context)
        {
            _context = context;
        }

        public async Task AddAuthenticationAsync(string name, bool authenticated)
        {
            Authentication auth = new Authentication()
            {
                Name = name,
                IsAuthenticated = authenticated,
                TimeOfAuth = DateTime.UtcNow
            };
            _context.Authentication.Add(auth);
            await _context.SaveChangesAsync();
        }
        public async Task<List<Authentication>> GetRecentAuthenticationsAsync(int count = 10)
        {
            return await _context.Authentication
                .OrderByDescending(a => a.TimeOfAuth) // Order by TimeOfAuth descending
                .Take(count) // Take the specified number of records
                .ToListAsync();
        }
        public async Task<Authentication> GetAuthenticationByIdAsync(int id)
        {
            try
            {
                return await _context.Authentication.FindAsync(id);
            }
            catch
            {
                throw;
            }
        }
        public async Task DeleteAuthenticationAsync(int id)
        {
            var authentication = await _context.Authentication.FindAsync(id);
            if (authentication != null)
            {
                _context.Authentication.Remove(authentication);
                await _context.SaveChangesAsync();
            }
        }
    }
}

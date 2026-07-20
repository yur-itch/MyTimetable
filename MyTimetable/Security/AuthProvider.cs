using MyTimetable.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace MyTimetable.Security
{
    public sealed class AuthProvider
    {
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly TimeProvider time;

        public AuthProvider(IPasswordHasher<User> passwordHasher, TimeProvider timeProvider)
        {
            _passwordHasher = passwordHasher;
            time = timeProvider;
        }

        private static async Task<User?> GetUserWithSession(AppDbContext db, string? session)
        {
            if (string.IsNullOrEmpty(session)) return null;
            Session? dbSess = await db.Sessions.FindAsync(session);
            if (dbSess == null) return null;
            return await db.Users.FindAsync(dbSess.Username);
        }

        private static async Task<User?> GetUserWithName(AppDbContext db, string name)
            => await db.Users.Where(x => x.Username == name).FirstOrDefaultAsync();

        public async Task<bool> IsViewer(AppDbContext db, string? session)
            => (await GetUserWithSession(db, session))?.IsViewer ?? false;

        public async Task<bool> IsEditor(AppDbContext db, string? session)
            => (await GetUserWithSession(db, session))?.IsEditor ?? false;

        public async Task<bool> CanLogIn(AppDbContext db, string username, string password)
        {
            User? user = await GetUserWithName(db, username);
            if (user == null) return false;

            var result = _passwordHasher.VerifyHashedPassword(user, user.Password, password);

            switch (result)
            {
                case PasswordVerificationResult.Success:
                    return true;

                case PasswordVerificationResult.SuccessRehashNeeded:
                    user.Password = _passwordHasher.HashPassword(user, password);
                    await db.SaveChangesAsync();
                    return true;

                case PasswordVerificationResult.Failed:
                default:
                    return false;
            }
        }

        private static bool IsValidPassword(string password, out string errorMessage)
        {
            errorMessage = "";

            if (string.IsNullOrEmpty(password))
            {
                errorMessage = "Password cannot be empty.";
                return false;
            }
            if (password.Length < 8)
            {
                errorMessage = "Password must be at least 8 characters.";
                return false;
            }
            if (!password.Any(char.IsUpper))
            {
                errorMessage = "Password must have at least one uppercase letter.";
                return false;
            }
            if (!password.Any(char.IsLower))
            {
                errorMessage = "Password must have at least one lowercase letter.";
                return false;
            }
            if (!password.Any(char.IsDigit))
            {
                errorMessage = "Password must have at least one number.";
                return false;
            }
            if (!password.Any(c => !char.IsLetterOrDigit(c)))
            {
                errorMessage = "Password must have at least one special character.";
                return false;
            }
            return true;
        }

        public async Task<User?> Register(AppDbContext db, string username, string password, bool isViewer = true, bool isEditor = false)
        {
            if (!IsValidPassword(password, out _))
                return null;
            if ((await GetUserWithName(db, username)) != null)
                return null;

            var user = new User
            {
                Username = username,
                CreatedAt = time.GetUtcNow().UtcDateTime,
                IsEditor = isEditor,
                IsViewer = isViewer,
                Password = ""
            };

            user.Password = _passwordHasher.HashPassword(user, password);

            await db.Users.AddAsync(user);
            await db.SaveChangesAsync();
            return user;
        }
    }
}

using MyTimetable.Models;
using MyTimetable.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace MyTimetable.Security
{
    public sealed class AuthProvider
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly TimeProvider time;

        public AuthProvider(AppDbContext db, IPasswordHasher<User> passwordHasher, TimeProvider timeProvider)
        {
            _db = db;
            _passwordHasher = passwordHasher;
            time = timeProvider;
        }

        private async Task<User?> GetUserWithSession(string? session)
        {
            if (string.IsNullOrEmpty(session)) return null;
            Session? dbSess = await _db.Sessions.FindAsync(session);
            if (dbSess == null)
            {
                return null;
            }
            User? dbUser = await _db.Users.FindAsync(dbSess.Username);
            return dbUser;
        }

        private async Task<User?> GetUserWithName(string name)
            => await _db.Users.Where(x => x.Username == name).FirstOrDefaultAsync();

        public async Task<bool> IsViewer(string? session)
            => (await GetUserWithSession(session))?.IsViewer ?? false;

        public async Task<bool> IsEditor(string? session)
            => (await GetUserWithSession(session))?.IsEditor ?? false;

        public async Task<bool> CanLogIn(string username, string password)
        {
            User? user = await GetUserWithName(username);
            if (user == null) return false;

            var result = _passwordHasher.VerifyHashedPassword(
                user,
                user.Password,
                password
            );

            switch (result)
            {
                case PasswordVerificationResult.Success:
                    return true;

                case PasswordVerificationResult.SuccessRehashNeeded:
                    user.Password = _passwordHasher.HashPassword(user, password);
                    await _db.SaveChangesAsync();
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

        public async Task<User?> Register(string username, string password, bool isViewer = true, bool isEditor = false)
        {
            if (!IsValidPassword(password, out string error))
            {
                return null;
            }
            if ((await GetUserWithName(username)) != null)
            {
                return null;
            }

            var user = new User
            {
                Username = username,
                CreatedAt = time.GetUtcNow().UtcDateTime,
                IsEditor = isEditor,
                IsViewer = isViewer,
                Password = ""
            };

            user.Password = _passwordHasher.HashPassword(user, password);

            await _db.Users.AddAsync(user);
            await _db.SaveChangesAsync();
            return user;
        }
    }
}

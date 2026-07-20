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

        public AuthProvider(AppDbContext db, IPasswordHasher<User> passwordHasher)
        {
            _db = db;
            _passwordHasher = passwordHasher;
        }

        private async Task<User?> GetUserWithSession(string? session)
        {
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

        public async Task<bool> IsViewer(string? session) {
            User? user = await GetUserWithSession(session);
            if (user == null) return false;
            return user.IsViewer;
        }

        public async Task<bool> IsEditor(string? session) {
            User? user = await GetUserWithSession(session);
            if (user == null) return false;
            return user.IsEditor;
        }

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
                    user.Password = _passwordHasher.HashPassword(user, plainPassword);
                    await _db.SaveChangesAsync();
                    return true;

                case PasswordVerificationResult.Failed:
                default:
                    return false;
            }
        }

        public Register(string username, string password) {
            var user = new User { Username = username };
            user.Password = _passwordHasher.HashPassword(user, password);

            // Save to database - salt is embedded in PasswordHash string
            await _db.Users.AddAsync(user);
            await _db.SaveChangesAsync();
        }
    }
}

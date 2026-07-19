using System.Security.Cryptography;

namespace MyTimetable.Security
{
    public sealed class SessionIdProvider
    {
        public string Generate() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
    }
}

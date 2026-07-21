using StudentInsights.Domain.Common;

namespace StudentInsights.Application.Common.Security;

/// <summary>
/// Single source of truth for the password strength rule enforced at
/// registration and password reset. This is an application-level policy
/// (it can change independently of the User entity's own invariants), so it
/// deliberately lives here rather than on the domain entity — same reasoning
/// as SecureTokenGenerator: no state, no config, no DI, just a static helper.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    public static void EnsureValid(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength)
            throw new DomainException($"Password must be at least {MinLength} characters long.");
    }
}
namespace MarwanQuTech.Application.Common.Interfaces;

/// <summary>Infrastructure: BCrypt.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// When <paramref name="passwordHash"/> is null (unknown e-mail) the implementation still
    /// performs a dummy verification and returns false, so response time doesn't reveal
    /// whether the account exists.
    /// </summary>
    bool Verify(string password, string? passwordHash);
}

namespace UserService.Application.Interfaces;

/// <summary>
/// Port for hashing/verifying passwords. The concrete algorithm lives
/// in Infrastructure so the application stays free of crypto details.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

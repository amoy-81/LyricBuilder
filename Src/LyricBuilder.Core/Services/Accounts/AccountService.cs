using LyricBuilder.Core.Models.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LyricBuilder.Core.Services.Accounts;

public interface IAccountService
{
    Task<MutationOperationResult> RegisterAsync(RegisterMutation mutation, CancellationToken ct);
    Task<StatefulResult<AccessTokenModel>> LoginAsync(LoginMutation mutation, CancellationToken ct);
}

/// <summary>
/// Sign-up and sign-in. Registration creates a user profile and its account together; login
/// checks the password and issues an access token.
/// </summary>
public sealed class AccountService(
    ILogger<AccountService> logger,
    IRepository<User> userRepository,
    IRepository<Account> accountRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher<Account> passwordHasher,
    ITokenService tokenService) : IAccountService
{
    public async Task<MutationOperationResult> RegisterAsync(RegisterMutation mutation, CancellationToken ct)
    {
        try
        {
            var username = Account.NormalizeUsername(mutation.Username);

            if (await accountRepository.AnyAsync(a => a.Username == username, ct))
                return MutationOperationResult.Failed(
                    InternalError.Conflict($"username {username} is taken", "this username is already taken"));

            var account = new Account
            {
                Username = username,
                Role = AccountRole.User   // server-assigned, never from the body
            };
            account.PasswordHash = passwordHasher.HashPassword(account, mutation.Password);

            var user = new User { Name = mutation.Name.Trim(), Account = account };

            // Adding the user adds its account with it, in the same save.
            await userRepository.AddAsync(user, ct);
            await unitOfWork.SaveChangesAsync(ct);

            return MutationOperationResult.Success(user.Id, "registered");
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while registering");
            return MutationOperationResult.Failed(
                InternalError.InternalServerError("error while registering"));
        }
    }

    public async Task<StatefulResult<AccessTokenModel>> LoginAsync(LoginMutation mutation, CancellationToken ct)
    {
        try
        {
            var username = Account.NormalizeUsername(mutation.Username);

            var account = await accountRepository.Query()
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Username == username, ct);

            // One message for both cases, so a caller cannot probe which usernames exist.
            if (account is null
                || passwordHasher.VerifyHashedPassword(account, account.PasswordHash, mutation.Password)
                    == PasswordVerificationResult.Failed)
                return StatefulResult<AccessTokenModel>.Failed(
                    InternalError.Unauthorized($"failed login for {username}", "invalid username or password"));

            return StatefulResult<AccessTokenModel>.Success(tokenService.CreateAccessToken(account));
        }
        catch (Exception e)
        {
            logger.LogError(e, "error while logging in");
            return StatefulResult<AccessTokenModel>.Failed(
                InternalError.InternalServerError("error while logging in"));
        }
    }
}

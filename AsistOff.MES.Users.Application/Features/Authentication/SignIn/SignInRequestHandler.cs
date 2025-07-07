using AsistOff.MES.Shared.Abstractions.Auth;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Repositories;
using ErrorOr;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace AsistOff.MES.Users.Application.Features.Authentication.SignIn;

public class SignInRequestHandler : IRequestHandler<SignInRequest, ErrorOr<JsonWebToken>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IPasswordHasher<User> _hasher;
    private readonly IAuthManager _authManager;

    public SignInRequestHandler(IUsersRepository usersRepository, IPasswordHasher<User> hasher, IAuthManager authManager)
    {
        _usersRepository = usersRepository;
        _hasher = hasher;
        _authManager = authManager;
    }

    public async Task<ErrorOr<JsonWebToken>> Handle(SignInRequest request, CancellationToken cancellationToken)
    {
        var user = await _usersRepository.GetAsync(request.Email);

        if (user is null)
        {
            return Error.NotFound("User not found");
        }

        var verificationResult = _hasher.VerifyHashedPassword(user, user.Password, request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return Error.Unauthorized("Invalid credentials");
        }

        var claims = new Dictionary<string, IEnumerable<string>>
        {
            ["permissions"] = ["users", "users.read"],
            ["tenantId"] = [user.TenantId.ToString()]
        };

        var token = _authManager.CreateToken(user.Id.ToString(), user.Email, claims: claims);
        return token;
    }
}
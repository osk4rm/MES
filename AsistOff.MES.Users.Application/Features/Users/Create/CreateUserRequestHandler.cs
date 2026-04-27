using AsistOff.MES.Multitenancy.Context;
using AsistOff.MES.Multitenancy.Contracts.Interfaces;
using AsistOff.MES.Shared.Abstractions.Providers;
using AsistOff.MES.Shared.Abstractions.Exceptions;
using AsistOff.MES.Users.Core.Entities;
using AsistOff.MES.Users.Core.Repositories;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace AsistOff.MES.Users.Application.Features.Users.Create;

internal sealed class CreateUserRequestHandler : IRequestHandler<CreateUserRequest, Unit>
{
    private readonly IUsersRepository _usersRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CreateUserRequestHandler> _logger;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IGuidProvider _guidProvider;

    public CreateUserRequestHandler(
        IUsersRepository usersRepository,
        ITenantContext tenantContext,
        ILogger<CreateUserRequestHandler> logger,
        IGuidProvider guidProvider,
        IPasswordHasher<User> passwordHasher)
    {
        _usersRepository = usersRepository;
        _tenantContext = tenantContext;
        _logger = logger;
        _guidProvider = guidProvider;
        _passwordHasher = passwordHasher;
    }

    public async Task<Unit> Handle(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var existingUser = await _usersRepository.GetAsync(request.Email);
        if (existingUser is not null)
        {
            _logger.LogWarning("Attempted to create user with email {Email} that already exists", request.Email);
            throw new ConflictException("User with this email already exists");
        }

        var tenantId = _tenantContext.TenantId;
        var user = new User
        {
            Id = _guidProvider.NewGuid(),
            Email = request.Email,
            TenantId = tenantId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Password = string.Empty
        };
        user.Password = _passwordHasher.HashPassword(user, request.Password);

        await _usersRepository.AddAsync(user);

        _logger.LogInformation("Created new user with ID {UserId} for tenant {TenantId}", user.Id, tenantId);

        return Unit.Value;
    }
}

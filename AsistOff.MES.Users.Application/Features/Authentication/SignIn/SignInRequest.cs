using AsistOff.MES.Shared.Abstractions.Auth;
using MediatR;

namespace AsistOff.MES.Users.Application.Features.Authentication.SignIn;

public record SignInRequest(string Email, string Password) : IRequest<JsonWebToken>;

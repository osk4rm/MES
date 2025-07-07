using FluentValidation;

namespace AsistOff.MES.Users.Application.Features.Authentication.SignIn;

public class SignInRequestValidator : AbstractValidator<SignInRequest>
{
    public SignInRequestValidator()
    {
        RuleFor(x => x.Email)
            .EmailAddress()
            .WithMessage("Invalid email address");
        
        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required");
    }
}
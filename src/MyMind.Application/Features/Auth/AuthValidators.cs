using FluentValidation;
using MyMind.Domain.Users;

namespace MyMind.Application.Features.Auth;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public const int PasswordMinLength = 10;
    public const int DisplayNameMaxLength = 60;

    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email kiritilishi shart.")
            .MaximumLength(256)
            .EmailAddress().WithMessage("Email noto'g'ri formatda.");

        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("Username kiritilishi shart.")
            .Must(name => UsernameRules.IsValidFormat(UsernameRules.Normalize(name)))
            .WithMessage($"Username {UsernameRules.MinLength}–{UsernameRules.MaxLength} belgidan iborat bo'lishi va faqat lotin harflari, raqamlar va _ dan tashkil topishi kerak.")
            .Must(name => !UsernameRules.IsReserved(name))
            .WithMessage("Bu username band qilingan.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Parol kiritilishi shart.")
            .MinimumLength(PasswordMinLength).WithMessage($"Parol kamida {PasswordMinLength} belgidan iborat bo'lishi kerak.")
            .MaximumLength(128);

        RuleFor(x => x.DisplayName)
            .MaximumLength(DisplayNameMaxLength);

        RuleFor(x => x.AcceptTerms)
            .Equal(true).WithMessage("Foydalanish shartlariga rozilik berilishi shart.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Login).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

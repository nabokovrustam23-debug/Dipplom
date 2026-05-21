namespace BarbershopCrm.Domain.Enums;

public static class UserTokenPurpose
{
    public const string PasswordReset = "PasswordReset";

    public static readonly string[] All = { PasswordReset };
}

namespace GameProbe.Core.Profiles;

public sealed class ProfileValidationException : Exception
{
    public ProfileValidationException(IReadOnlyList<string> errors)
        : base("Profile validation failed: " + string.Join("; ", errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}

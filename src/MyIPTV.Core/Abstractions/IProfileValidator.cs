using MyIPTV.Core.Models;

namespace MyIPTV.Core.Abstractions;

public interface IProfileValidator
{
    ProfileValidationResult Validate(ProfileDraft draft, bool requireCredentials);
}

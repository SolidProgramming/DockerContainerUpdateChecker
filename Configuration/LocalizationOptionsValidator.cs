using System.Globalization;
using Microsoft.Extensions.Options;

namespace DockerContainerUpdateChecker.Configuration;

public sealed class LocalizationOptionsValidator : IValidateOptions<LocalizationOptions>
{
    public ValidateOptionsResult Validate(string? name, LocalizationOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Culture))
        {
            return ValidateOptionsResult.Fail("Localization:Culture must be configured.");
        }

        try
        {
            _ = CultureInfo.GetCultureInfo(options.Culture);
            return ValidateOptionsResult.Success;
        }
        catch (CultureNotFoundException)
        {
            return ValidateOptionsResult.Fail($"Localization:Culture '{options.Culture}' is not valid.");
        }
    }
}

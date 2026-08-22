using Microsoft.Extensions.Options;

namespace DockerContainerUpdateChecker.Configuration;

public sealed class StorageOptionsValidator : IValidateOptions<StorageOptions>
{
    public ValidateOptionsResult Validate(string? name, StorageOptions options)
    {
        return string.IsNullOrWhiteSpace(options.DataDirectory)
            ? ValidateOptionsResult.Fail("Storage:DataDirectory must be configured.")
            : ValidateOptionsResult.Success;
    }
}

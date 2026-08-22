using Microsoft.Extensions.Options;

namespace DockerContainerUpdateChecker.Configuration;

public sealed class ServerOptionsValidator : IValidateOptions<ServerOptions>
{
    public ValidateOptionsResult Validate(string? name, ServerOptions options)
    {
        return options.Port is >= 1 and <= 65535
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Server:Port must be between 1 and 65535.");
    }
}

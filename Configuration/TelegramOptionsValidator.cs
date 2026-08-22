using Microsoft.Extensions.Options;

namespace DockerContainerUpdateChecker.Configuration;

public sealed class TelegramOptionsValidator : IValidateOptions<TelegramOptions>
{
    public ValidateOptionsResult Validate(string? name, TelegramOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BotToken) || options.BotToken == "replace-me")
        {
            return ValidateOptionsResult.Fail("Telegram:BotToken must be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.ChatId) || options.ChatId == "replace-me")
        {
            return ValidateOptionsResult.Fail("Telegram:ChatId must be configured.");
        }

        return ValidateOptionsResult.Success;
    }
}

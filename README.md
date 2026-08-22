## Image: [solidprogramming/dockercontainerupdatechecker](https://hub.docker.com/repository/docker/solidprogramming/dockercontainerupdatechecker/)

# DockerContainerUpdateChecker

Checks running Docker containers for updated image digests and sends a Telegram notification when the detected update state changes. Scheduled runs and manual triggers are handled through Hangfire.

## Configuration

The app reads `appsettings.json` and then a local override file.

Local override file resolution:

1. `/config/appsettings.Local.json` if `/config` exists
2. otherwise `appsettings.Local.json` in the application directory

If the local file does not exist yet, the app creates a starter file automatically at the resolved location on first launch.

```json
{
  "Server": {
    "Port": 8080
  },
  "Localization": {
    "Culture": "de-DE"
  },
  "Scheduler": {
    "Cron": "0 */6 * * *",
    "RunOnStartup": false
  },
  "Docker": {
    "Endpoint": ""
  },
  "Telegram": {
    "BotToken": "123456:replace-me",
    "ChatId": "123456789"
  },
  "Monitoring": {
    "ExcludedContainers": [ "unraid-db" ]
  },
  "Storage": {
    "DataDirectory": "/config/data"
  }
}
```

Set `Localization:Culture` to a .NET culture such as `de-DE` or `en-US` to control localized date and time formatting in logs and notifications.
Set `Scheduler:RunOnStartup` to `true` if the container should execute one immediate update check after startup instead of waiting for the next cron occurrence.

## Unraid / Docker notes

- Mount the Docker socket: `/var/run/docker.sock:/var/run/docker.sock`
- Mount a single parent folder from the host to `/config`, for example `/mnt/user/appdata/dockercontainerupdatechecker:/config`
- The generated `appsettings.Local.json` will then be created at `/config/appsettings.Local.json`
- Persistent state will be stored under `/config/data`
- Map a host port to the internal container port, default `8080`
- The Hangfire dashboard is available at `/hangfire`

## Docker / Podman endpoint selection

The app resolves the container API endpoint in this order:

1. `Docker:Endpoint` from configuration
2. `DOCKER_HOST` from the environment
3. Built-in default

Defaults:

- Windows: `npipe://./pipe/docker_engine`
- Linux: `unix:///var/run/docker.sock`

Examples:

- Docker on Linux / Unraid: `unix:///var/run/docker.sock`
- Podman socket on Linux: `unix:///run/user/1000/podman/podman.sock`
- Podman or Docker via TCP: `tcp://127.0.0.1:2375`

## Behavior

- By default, all running containers are checked
- Individual containers can be excluded through `Monitoring:ExcludedContainers`
- Hangfire uses in-memory storage, so dashboard history is cleared after a container restart
- The app sends a Telegram startup test notification after successful startup validation
- The last functional update state is stored separately in `/config/data/last-update-state.json` by default so notifications remain stable across restarts

## Telegram troubleshooting

- If Telegram returns `400 Bad Request`, a common reason is that the target user or group has not started a chat with the bot yet
- For direct messages, open the bot in Telegram and send `/start` once before testing notifications
- If the problem remains, verify that the configured `Telegram:ChatId` is correct

## Create a Telegram bot

1. Open Telegram and start a chat with `@BotFather`.
2. Run `/newbot` and follow the prompts to create a bot.
3. Copy the bot token from BotFather and set it as `Telegram:BotToken`.
4. Open your new bot in Telegram and send `/start` once so the bot can message you directly.
5. Send any additional message to the bot, then open `https://api.telegram.org/bot<your-bot-token>/getUpdates` in a browser.
6. Find the `chat` object in the JSON response and copy the numeric `id` value into `Telegram:ChatId`.

After both values are configured, restart the app and it will send a startup test notification after successful validation.

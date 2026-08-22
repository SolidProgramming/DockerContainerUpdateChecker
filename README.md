# DockerContainerUpdateChecker

Checks running Docker containers for updated image digests and sends a Telegram notification when the detected update state changes. Scheduled runs and manual triggers are handled through Hangfire.

## Configuration

The app reads `appsettings.json` and optionally `appsettings.Local.json`.

If `appsettings.Local.json` does not exist yet, the app creates a starter file automatically on first launch in the application directory. In a container setup, this works best when you mount a writable host path to `/app/appsettings.Local.json` or the whole `/app` config location you want to persist.

```json
{
  "Server": {
    "Port": 8080
  },
  "Scheduler": {
    "Cron": "0 6 * * *"
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
    "DataDirectory": "/data"
  }
}
```

## Unraid / Docker notes

- Mount the Docker socket: `/var/run/docker.sock:/var/run/docker.sock`
- Mount a persistent volume for state, for example `/mnt/user/appdata/docker-update-checker:/data`
- Mount `/app/appsettings.Local.json` from the host if you want to edit and persist the generated local configuration file outside the container
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
- A separate `telegram-test-notification` Hangfire job is available for manual Telegram tests from the dashboard
- The last functional update state is stored separately in `/data/last-update-state.json` so notifications remain stable across restarts

## Telegram troubleshooting

- If Telegram returns `400 Bad Request`, a common reason is that the target user or group has not started a chat with the bot yet
- For direct messages, open the bot in Telegram and send `/start` once before testing notifications
- If the problem remains, verify that the configured `Telegram:ChatId` is correct

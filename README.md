# DockerContainerUpdateChecker

Checks running Docker containers for updated image digests and sends a Telegram notification when the detected update state changes. Scheduled runs and manual triggers are handled through Hangfire.

## Configuration

The app reads `appsettings.json` and optionally `appsettings.Local.json`.

```json
{
  "Server": {
    "Port": 8080
  },
  "Scheduler": {
    "Cron": "0 6 * * *"
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
- Map a host port to the internal container port, default `8080`
- The Hangfire dashboard is available at `/hangfire`

## Behavior

- By default, all running containers are checked
- Individual containers can be excluded through `Monitoring:ExcludedContainers`
- Hangfire uses in-memory storage, so dashboard history is cleared after a container restart
- The last functional update state is stored separately in `/data/last-update-state.json` so notifications remain stable across restarts

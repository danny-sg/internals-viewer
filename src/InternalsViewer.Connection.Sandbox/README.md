# Sandbox Connection

(Work in progress/experimental)

- Use WSL Containers to provide a sandboxed SQL Server instance for running Internals Viewer in a non-production environment
- Connection manages lifecycle of the container
- Selection of SQL Server version
- Time consuming pull managed in the configuration page
  - Connection cannot be made until image is ready
- Path for initialization script
- Container tear-down managed by connection

See https://learn.microsoft.com/en-us/windows/wsl/wsl-container?tabs=csharp
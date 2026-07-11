# Docker database environments

The repository starts SQL Server, MySQL, PostgreSQL, and Oracle as one Linux-container Docker Compose project. Each environment has isolated containers, networks, named volumes, and database data.

Run scripts from the repository root:

```bash
./start-environment.sh
./start-environment.sh debug
./start-environment.sh test
./start-environment.sh ci
./start-environment.sh issue-142

./stop-environment.sh
./stop-environment.sh debug
./stop-environment.sh issue-142
./stop-environment.sh issue-142 --remove-data
```

PowerShell uses the equivalent `start-environment.ps1` and `stop-environment.ps1` commands.

## Environment names

`debug`, `test`, and `ci` are reserved fixed environments. Any other name is dynamic. When no name is supplied, the script derives it from the Git worktree directory.

Names are converted to lowercase, unsupported characters become `-`, repeated separators collapse, and leading/trailing separators are removed. The Compose project name is:

```text
neventstore-<normalized-environment-name>
```

Compose generates all container, network, and volume names. No service uses `container_name`, because fixed names would prevent parallel worktrees.

## Ports

| Database | Debug | Test | CI |
| --- | ---: | ---: | ---: |
| SQL Server | 50001 | 51001 | 52001 |
| MySQL | 50003 | 51003 | 52003 |
| PostgreSQL | 50004 | 51004 | 52004 |
| Oracle | 50005 | 51005 | 52005 |

Fixed ports make human debugging and CI configuration predictable. Dynamic environments bind to `127.0.0.1` with Docker-assigned host ports so parallel worktrees do not need a shared allocator and cannot collide under normal Docker behavior.

## Generated configuration

A successful start overwrites exactly one local file after every database is healthy and SQL Server's `NEventStore` database exists:

- `debug` writes `.env.debug`
- `test` writes `.env.test`
- `ci` writes `.env.ci`
- any dynamic name writes `.env.dynamic`

The files contain provider components such as host, port, database, username, and password. Connection strings are assembled by the test code so shell scripts do not own provider syntax.

The existing complete connection-string environment variables remain supported and take precedence:

- `NEventStore.MsSql`
- `NEventStore.MySql`
- `NEventStore.PostgreSql`
- `NEventStore.Oracle`

When those variables are absent, tests load one selected file. Set `NEVENTSTORE_ENVIRONMENT` to `dynamic`, `debug`, `test`, or `ci`; it defaults to `dynamic`.

```bash
NEVENTSTORE_ENVIRONMENT=dynamic dotnet test ./src/NEventStore.Persistence.Sql.Core.sln
NEVENTSTORE_ENVIRONMENT=debug dotnet test ./src/NEventStore.Persistence.Sql.Core.sln
```

```powershell
$env:NEVENTSTORE_ENVIRONMENT = 'test'
dotnet test ./src/NEventStore.Persistence.Sql.Core.sln
```

The generated files are ignored by Git and must not be committed.

## Data lifecycle and safety

Normal stop preserves named volumes and generated `.env` files. This makes repeated starts fast and keeps database state available for investigation.

`--remove-data` passes `--volumes` only to the selected Compose project. It does not remove another worktree's data. Global commands such as `docker system prune`, `docker volume prune`, and `docker network prune` are prohibited because they are not scoped to one environment.

The scripts are non-interactive and return a non-zero exit code when Docker, Compose, service health, SQL Server initialization, port discovery, or configuration-file generation fails.

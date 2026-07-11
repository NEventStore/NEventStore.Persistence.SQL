# Docker database environments

The scripts in this directory start and stop the complete Linux-container database stack used by the SQL persistence tests.

Each Git worktree owns one Docker Compose project:

```text
worktree -> environment name -> Compose project -> all database services
```

The Compose project name is `neventstore-sql-<environment-name>`. Containers, networks, and named volumes are isolated by that project name.

## Start an environment

Run the platform-specific script from any directory:

```powershell
./docker/start-environment.ps1
```

```bash
bash ./docker/start-environment.sh
```

Without an argument, the environment name is derived from the worktree directory. The name is normalized and dynamic host ports are assigned automatically.

An explicit dynamic environment can be started with:

```bash
bash ./docker/start-environment.sh issue-142
```

Three reserved environment names use predictable ports:

```bash
bash ./docker/start-environment.sh debug
bash ./docker/start-environment.sh test
bash ./docker/start-environment.sh ci
```

| Database | Debug | Test | CI |
| --- | ---: | ---: | ---: |
| SQL Server | 50001 | 51001 | 52001 |
| MySQL | 50003 | 51003 | 52003 |
| PostgreSQL | 50004 | 51004 | 52004 |
| Oracle | 50005 | 51005 | 52005 |

The start script waits for the services to become healthy, creates the SQL Server `NEventStore` database when necessary, retrieves the assigned ports, and writes the resulting connection strings to the repository-root `.env` file.

The generated `.env` file is excluded from Git. Test projects load it automatically when the corresponding process environment variable is not already set.

## Stop an environment

Data is preserved by default:

```bash
bash ./docker/stop-environment.sh issue-142
```

Remove only the selected environment's named volumes with:

```bash
bash ./docker/stop-environment.sh issue-142 --remove-data
```

The PowerShell scripts accept the same environment names and behavior:

```powershell
./docker/stop-environment.ps1 issue-142 --remove-data
```

## Agent and CI usage

Agents should normally use a dynamic environment derived from their worktree or task:

```bash
bash ./docker/start-environment.sh issue-142
dotnet test ./src/NEventStore.Persistence.Sql.Core.sln
bash ./docker/stop-environment.sh issue-142
```

Agents should not use `debug`, `test`, or `ci` unless explicitly instructed. `debug` and `test` reserve predictable ports for human workflows; `ci` is reserved for GitHub Actions.

GitHub Actions starts the same stack through:

```bash
bash ./docker/start-environment.sh ci
```

Each test-matrix job runs on its own hosted runner, so the fixed CI ports do not collide between jobs. The workflow stops the stack with `--remove-data` in an `always()` cleanup step.

## Compose files

- `docker-compose.yml` defines the Linux database services, health checks, and named volumes.
- `docker-compose.dynamic.yml` publishes each service on an automatically assigned local port.
- `docker-compose.debug.yml` publishes the fixed debug ports.
- `docker-compose.test.yml` publishes the fixed test ports.
- `docker-compose.ci.yml` publishes the fixed GitHub Actions CI ports.

The Compose files intentionally do not define `container_name`; Compose derives resource names from the project name.

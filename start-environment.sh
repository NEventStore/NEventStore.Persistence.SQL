#!/usr/bin/env bash
# Purpose: Start the complete isolated database stack for one worktree/environment.
# Usage: ./start-environment.sh [debug|test|ci|<dynamic-name>]
# Reserved environments use predictable ports; omitted names derive from the worktree directory
# and use Docker-assigned ports. Successful starts write .env.debug, .env.test, .env.ci, or
# .env.dynamic only after every service is healthy. Volumes are preserved by stop by default;
# use stop-environment.sh <name> --remove-data to remove only that environment's data.
# The script is non-interactive and exits non-zero on invalid input, Docker/Compose failure,
# unhealthy services, failed database initialization, unresolved ports, or file-write failure.
# Safety: no container_name values or global Docker cleanup commands are used. Fixed ports support
# predictable human/CI workflows; dynamic ports prevent worktree collisions. Compose owns names
# for containers, networks, and volumes. Component values, not complete connection strings, are
# written so provider-specific construction remains in test code.

set -euo pipefail

readonly ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly COMPOSE_DIR="$ROOT_DIR/docker"
readonly REPOSITORY_PREFIX="neventstore"
readonly DATABASE_NAME="NEventStore"
readonly DATABASE_USERNAME="sa"
readonly DATABASE_PASSWORD="Password1"

fail() {
  printf 'Error: %s\n' "$*" >&2
  exit 1
}

normalize_environment_name() {
  printf '%s' "$1" \
    | tr '[:upper:]' '[:lower:]' \
    | sed -E 's/[^a-z0-9]+/-/g; s/-+/-/g; s/^-//; s/-$//'
}

default_environment_name() {
  local worktree_root
  worktree_root="$(git -C "$ROOT_DIR" rev-parse --show-toplevel 2>/dev/null || printf '%s' "$ROOT_DIR")"
  basename "$worktree_root"
}

compose_override_for() {
  case "$1" in
    debug|test|ci) printf '%s/docker-compose.%s.yml' "$COMPOSE_DIR" "$1" ;;
    *) printf '%s/docker-compose.dynamic.yml' "$COMPOSE_DIR" ;;
  esac
}

env_file_for() {
  case "$1" in
    debug|test|ci) printf '%s/.env.%s' "$ROOT_DIR" "$1" ;;
    *) printf '%s/.env.dynamic' "$ROOT_DIR" ;;
  esac
}

wait_for_service() {
  local service="$1"
  local container_id status attempt
  container_id="$(${COMPOSE[@]} ps -q "$service")"
  [[ -n "$container_id" ]] || fail "Compose did not create service '$service'."

  for attempt in $(seq 1 120); do
    status="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$container_id" 2>/dev/null || true)"
    [[ "$status" == "healthy" ]] && return 0
    [[ "$status" == "exited" || "$status" == "dead" ]] && break
    sleep 2
  done

  docker logs --tail 100 "$container_id" >&2 || true
  fail "Service '$service' did not become healthy (last status: ${status:-unknown})."
}

published_port() {
  local service="$1"
  local container_port="$2"
  local binding port
  binding="$(${COMPOSE[@]} port "$service" "$container_port" | tail -n 1)"
  port="${binding##*:}"
  [[ "$port" =~ ^[0-9]+$ ]] || fail "Could not resolve the published port for '$service:$container_port'."
  printf '%s' "$port"
}

[[ $# -le 1 ]] || fail "Usage: ./start-environment.sh [debug|test|ci|<dynamic-name>]"
command -v docker >/dev/null 2>&1 || fail "Docker is not installed or not on PATH."
docker compose version >/dev/null 2>&1 || fail "Docker Compose v2 is unavailable."

raw_environment="${1:-$(default_environment_name)}"
environment_name="$(normalize_environment_name "$raw_environment")"
[[ -n "$environment_name" ]] || fail "The environment name becomes empty after normalization."

project_name="$REPOSITORY_PREFIX-$environment_name"
override_file="$(compose_override_for "$environment_name")"
env_file="$(env_file_for "$environment_name")"
readonly -a COMPOSE=(docker compose --project-name "$project_name" -f "$COMPOSE_DIR/docker-compose.yml" -f "$override_file")
readonly -a SERVICES=(sqlexpress mysql postgres oracle)

"${COMPOSE[@]}" up --detach
for service in "${SERVICES[@]}"; do
  wait_for_service "$service"
done

sql_container="$(${COMPOSE[@]} ps -q sqlexpress)"
docker exec "$sql_container" /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P "$DATABASE_PASSWORD" \
  -Q "IF DB_ID(N'$DATABASE_NAME') IS NULL CREATE DATABASE [$DATABASE_NAME];" >/dev/null

sqlserver_port="$(published_port sqlexpress 1433)"
mysql_port="$(published_port mysql 3306)"
postgres_port="$(published_port postgres 5432)"
oracle_port="$(published_port oracle 1521)"

temporary_file="$(mktemp "$ROOT_DIR/.env.tmp.XXXXXX")"
trap 'rm -f "$temporary_file"' EXIT
cat > "$temporary_file" <<EOF
SQLSERVER_HOST=127.0.0.1
SQLSERVER_PORT=$sqlserver_port
SQLSERVER_DATABASE=$DATABASE_NAME
SQLSERVER_USERNAME=$DATABASE_USERNAME
SQLSERVER_PASSWORD=$DATABASE_PASSWORD
MYSQL_HOST=127.0.0.1
MYSQL_PORT=$mysql_port
MYSQL_DATABASE=$DATABASE_NAME
MYSQL_USERNAME=$DATABASE_USERNAME
MYSQL_PASSWORD=$DATABASE_PASSWORD
POSTGRES_HOST=127.0.0.1
POSTGRES_PORT=$postgres_port
POSTGRES_DATABASE=$DATABASE_NAME
POSTGRES_USERNAME=$DATABASE_USERNAME
POSTGRES_PASSWORD=$DATABASE_PASSWORD
ORACLE_HOST=127.0.0.1
ORACLE_PORT=$oracle_port
ORACLE_SERVICE=XE
ORACLE_USERNAME=system
ORACLE_PASSWORD=$DATABASE_PASSWORD
EOF
mv -f "$temporary_file" "$env_file"
trap - EXIT

printf 'Environment: %s\nCompose project: %s\nConfiguration: %s\n' "$environment_name" "$project_name" "$env_file"
printf 'Ports: SQL Server=%s, MySQL=%s, PostgreSQL=%s, Oracle=%s\n' "$sqlserver_port" "$mysql_port" "$postgres_port" "$oracle_port"
printf 'Tests: NEVENTSTORE_ENVIRONMENT=%s dotnet test ./src/NEventStore.Persistence.Sql.Core.sln\n' "$([[ "$environment_name" =~ ^(debug|test|ci)$ ]] && printf '%s' "$environment_name" || printf 'dynamic')"

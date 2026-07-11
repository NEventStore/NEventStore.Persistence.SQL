#!/usr/bin/env bash
# Purpose: Stop only the Compose project belonging to one worktree/environment.
# Usage: ./stop-environment.sh [debug|test|ci|<dynamic-name>] [--remove-data]
# Reserved names match the fixed environments. With no name, the worktree directory is normalized
# and used as a dynamic environment. Generated .env files and named volumes are preserved by
# default, making repeated starts predictable. --remove-data adds Compose --volumes and affects
# only the selected project. The script is non-interactive and exits non-zero on invalid input or
# Compose failure. It never uses fixed container names or global Docker cleanup commands because
# one developer or agent must not be allowed to demolish everybody else's databases by enthusiasm.

set -euo pipefail

readonly ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly COMPOSE_DIR="$ROOT_DIR/docker"
readonly REPOSITORY_PREFIX="neventstore"

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

environment_argument=""
remove_data=false
for argument in "$@"; do
  case "$argument" in
    --remove-data) remove_data=true ;;
    --*) fail "Unknown option '$argument'." ;;
    *)
      [[ -z "$environment_argument" ]] || fail "Only one environment name may be supplied."
      environment_argument="$argument"
      ;;
  esac
done

command -v docker >/dev/null 2>&1 || fail "Docker is not installed or not on PATH."
docker compose version >/dev/null 2>&1 || fail "Docker Compose v2 is unavailable."

raw_environment="${environment_argument:-$(default_environment_name)}"
environment_name="$(normalize_environment_name "$raw_environment")"
[[ -n "$environment_name" ]] || fail "The environment name becomes empty after normalization."

project_name="$REPOSITORY_PREFIX-$environment_name"
override_file="$(compose_override_for "$environment_name")"
compose=(docker compose --project-name "$project_name" -f "$COMPOSE_DIR/docker-compose.yml" -f "$override_file")

if $remove_data; then
  "${compose[@]}" down --volumes
else
  "${compose[@]}" down
fi

printf 'Stopped environment %s (Compose project %s).\n' "$environment_name" "$project_name"
if $remove_data; then
  printf 'Removed only this environment\047s named volumes; generated .env files were preserved.\n'
else
  printf 'Named volumes and generated .env files were preserved.\n'
fi

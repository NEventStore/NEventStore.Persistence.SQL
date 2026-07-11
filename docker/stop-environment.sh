#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd "$script_dir/.." && pwd)"
project_prefix="neventstore-sql"
maximum_environment_name_length=48
remove_data=false
raw_environment_name=""

normalize_environment_name() {
  local value="$1"
  local normalized
  normalized="$(printf '%s' "$value" | tr '[:upper:]' '[:lower:]' | sed -E 's/[^a-z0-9]+/-/g; s/^-+//; s/-+$//')"
  if [[ -z "$normalized" ]]; then
    echo "Environment name '$value' does not contain any supported characters." >&2
    return 1
  fi
  normalized="${normalized:0:$maximum_environment_name_length}"
  printf '%s' "${normalized%-}"
}

for argument in "$@"; do
  case "$argument" in
    --remove-data) remove_data=true ;;
    -*) echo "Unknown option: $argument" >&2; exit 2 ;;
    *)
      if [[ -n "$raw_environment_name" ]]; then echo "Only one environment name may be provided." >&2; exit 2; fi
      raw_environment_name="$argument"
      ;;
  esac
done

if [[ -z "$raw_environment_name" ]]; then raw_environment_name="$(basename "$repository_root")"; fi
environment_name="$(normalize_environment_name "$raw_environment_name")"
case "$environment_name" in
  debug|test|ci) port_mode="$environment_name" ;;
  *) port_mode="dynamic" ;;
esac

project_name="$project_prefix-$environment_name"
compose_command=(docker compose --project-name "$project_name" --file "$script_dir/docker-compose.yml" --file "$script_dir/docker-compose.$port_mode.yml")
down_arguments=(down)
if [[ "$remove_data" == true ]]; then down_arguments+=(--volumes); fi

echo "Stopping '$project_name'..."
"${compose_command[@]}" "${down_arguments[@]}"

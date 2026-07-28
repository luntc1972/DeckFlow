#!/usr/bin/env bash
# D-01: the connection string is environment-only, fail-closed, never echoed, and never passed on
# argv. D-02: exit code 2 is a legitimate result, not a retry trigger or a reason to lower the
# threshold without developer authorization. D-03: when LIMIT is set, refuse any non-positive or
# non-integer value here because the CLI treats <=0 as no limit and would start the full multi-hour
# corpus run. Cheap criterion-3 smoke run:
# MIN_DECKS=999999 LIMIT=50 bash scripts/edhrec-brackets/run-role-floor-research.sh
set -euo pipefail

if [ "$#" -ne 0 ]; then
  echo "ERROR: this script takes no positional arguments. Provide the connection string via DECKFLOW_ROLE_FLOOR_CONNECTION_STRING." >&2
  echo "WARNING: ${#} positional argument(s) were supplied. If you just typed a connection string on the command line, it is now in your shell history; clear that history entry and consider rotating the credential." >&2
  exit 1
fi

if [ -z "${DECKFLOW_ROLE_FLOOR_CONNECTION_STRING:-}" ]; then
  echo "ERROR: export DECKFLOW_ROLE_FLOOR_CONNECTION_STRING before running (see 02-08-PLAN.md). It is never stored in this repo." >&2
  exit 1
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../.." && pwd)"
phase_dir="${repo_root}/.planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research"

cd "${repo_root}"

dotnet_path="/mnt/c/Program Files/dotnet/dotnet.exe"
min_decks="${MIN_DECKS:-40}"
mode="${MODE:-cedh}"
cards_cache="${CARDS_CACHE:-_role-floor-research/cards_full.json}"
limit="${LIMIT:-}"

if [ -n "${limit}" ]; then
  case "${limit}" in
    *[!0-9]*)
      echo "ERROR: LIMIT='${limit}' is invalid. This wrapper only accepts a positive integer LIMIT. The CLI treats LIMIT <= 0 as no limit, so allowing '${limit}' would start the full multi-hour corpus run. Omit LIMIT entirely for a full run." >&2
      exit 1
      ;;
  esac

  if [ "$((10#${limit}))" -le 0 ]; then
    echo "ERROR: LIMIT='${limit}' is invalid. This wrapper only accepts a positive integer LIMIT. The CLI treats LIMIT <= 0 as no limit, so allowing '${limit}' would start the full multi-hour corpus run. Omit LIMIT entirely for a full run." >&2
    exit 1
  fi
fi

if [ "${EDHREC_DATA+x}" = "x" ]; then
  edhrec_data="${EDHREC_DATA}"
else
  edhrec_data="_edhrec-brackets"
fi

if [ "${min_decks}" = "999999" ]; then
  # Why: a deliberately unreachable MIN_DECKS proves criterion 3 end to end (exit 2, no artifact),
  # and this is a RAISED threshold; the prohibition is on LOWERING one after seeing a null result.
  log_file="${phase_dir}/role-floor-research-smoke.log"
  exit_file="${phase_dir}/role-floor-research-smoke.exit"
else
  log_file="${phase_dir}/role-floor-research-run.log"
  exit_file="${phase_dir}/role-floor-research-run.exit"
fi

args=(
  run
  --project DeckFlow.CLI
  --
  role-floor-research
  --min-decks "${min_decks}"
  --mode "${mode}"
  --cards-cache "${cards_cache}"
)

if [ -n "${edhrec_data}" ]; then
  args+=(--edhrec-data "${edhrec_data}")
fi

if [ -n "${limit}" ]; then
  args+=(--limit "${limit}")
fi

if [[ "${dotnet_path}" == *.exe || "${dotnet_path}" == *"/mnt/c/"* ]]; then
  export WSLENV="${WSLENV:+${WSLENV}:}DECKFLOW_ROLE_FLOOR_CONNECTION_STRING"
fi

printf 'invocation:'
printf ' %q' "${dotnet_path}" "${args[@]}"
printf '\n'
echo "connection string: taken from DECKFLOW_ROLE_FLOOR_CONNECTION_STRING (not shown, not passed on the command line)"

set +e
# Why: do not pass --connection-string; argv is visible in the process list for the full multi-hour
# run, while the child process can read DECKFLOW_ROLE_FLOOR_CONNECTION_STRING directly (plan 02-04 D-07).
"${dotnet_path}" "${args[@]}" 2>&1 | tee "${log_file}"
harness_exit=${PIPESTATUS[0]}
set -e

printf '%s\n' "${harness_exit}" > "${exit_file}"

if [ "${harness_exit}" = "1" ] && grep -Fq "DECKFLOW_ROLE_FLOOR_CONNECTION_STRING environment variable is required." "${log_file}"; then
  echo "Harness rejected the environment-only path. Possible causes include the plan 02-04 D-07 environment-variable read being absent in the harness, or WSLENV failing to propagate DECKFLOW_ROLE_FLOOR_CONNECTION_STRING into the Windows dotnet process. Do not add the command-line connection-string flag in this wrapper; take the process-list exposure decision to the Task 2 checkpoint." >&2
fi

case "${harness_exit}" in
  0)
    echo "exit code 0: artifacts written."
    ;;
  1)
    echo "exit code 1: error."
    ;;
  2)
    echo "exit code 2: ran successfully but zero commanders qualified (a legitimate outcome; do NOT lower the threshold without developer authorization)."
    ;;
  *)
    echo "exit code ${harness_exit}: unexpected."
    ;;
esac

exit "${harness_exit}"

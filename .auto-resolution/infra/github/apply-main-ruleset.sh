#!/usr/bin/env bash
set -euo pipefail

# Reproducible application of the main-branch ruleset (Rulesets API).
# Idempotent: re-running with the same JSON converges to the same state by
#   - POSTing if no ruleset with the target name exists, OR
#   - PATCHing the existing ruleset by its numeric id.
# This script is REVIEWED + APPROVED by the human owner before each run.
#
# Flags:
#   --dry-run    Print the resolved payload + the gh command(s) that WOULD
#                run; do NOT call the GitHub API for any mutation. A read-only
#                "list rulesets" call IS made to determine create-vs-update.
#   --evaluate   Set enforcement=evaluate in the payload (overrides JSON).
#                Use this for the first apply to observe behavior on real PRs
#                without blocking merges. Promote to active by re-running
#                without --evaluate. NOTE: --evaluate is an Enterprise-plan-only
#                feature on github.com; on lower plans the API rejects it. See
#                README.md "Canary branch test" for the substitute pattern.
#   --delete     Look up the ruleset by name and DELETE it. One-command
#                rollback. Cannot be combined with --dry-run or --evaluate.
#                Exits 0 (with a "nothing to delete" message) if the ruleset
#                does not exist, so the flag is safe to re-run.
#
# Env overrides:
#   REPO         default: ctwoodwa/Sunfish
#   RULESET      default: infra/github/main-ruleset.json
#   RULE_NAME    default: read from $RULESET .name (falls back to "main-protection")

REPO="${REPO:-ctwoodwa/Sunfish}"
RULESET="${RULESET:-infra/github/main-ruleset.json}"

DRY_RUN=0
EVALUATE=0
DELETE=0
for arg in "$@"; do
  case "$arg" in
    --dry-run)  DRY_RUN=1 ;;
    --evaluate) EVALUATE=1 ;;
    --delete)   DELETE=1 ;;
    -h|--help)
      sed -n '3,28p' "$0"
      exit 0
      ;;
    *)
      echo "ERROR: unknown flag: $arg" >&2
      exit 64
      ;;
  esac
done

# --delete is a destructive rollback path that bypasses the JSON payload entirely.
# It cannot be combined with --dry-run (which has its own explicit dry-run gate)
# nor --evaluate (which is a payload-shaping flag, meaningless when deleting).
if [ "$DELETE" -eq 1 ] && [ "$DRY_RUN" -eq 1 ]; then
  echo "ERROR: Cannot combine --delete with --dry-run." >&2
  echo "       --delete is a one-shot rollback; if you want to inspect first, run without --delete." >&2
  exit 2
fi
if [ "$DELETE" -eq 1 ] && [ "$EVALUATE" -eq 1 ]; then
  echo "ERROR: Cannot combine --delete with --evaluate." >&2
  echo "       --evaluate is a payload-shaping flag and has no meaning when deleting." >&2
  exit 2
fi

if [ ! -f "$RULESET" ]; then
  echo "ERROR: ruleset file not found: $RULESET" >&2
  exit 2
fi

# JSON validator: prefer jq, fall back to python (the agent sandbox has no jq).
json_validate() {
  if command -v jq >/dev/null 2>&1; then
    jq . "$1" >/dev/null
  else
    python -c "import json,sys; json.load(open(sys.argv[1]))" "$1"
  fi
}

# JSON shaping (recursively strip _comment_* keys, optionally override enforcement).
# Comment-stripping is recursive so authors can place rationale comments inside
# nested objects (e.g. inside `parameters`) without the GitHub API rejecting them.
# Prefer jq when available; otherwise fall back to a tiny Python one-liner.
shape_payload() {
  local infile="$1"
  local enforcement_override=""
  if [ "$EVALUATE" -eq 1 ]; then
    enforcement_override="evaluate"
  fi
  if command -v jq >/dev/null 2>&1; then
    if [ -n "$enforcement_override" ]; then
      jq --arg enf "$enforcement_override" \
        'def strip_comments: if type == "object" then with_entries(select(.key | startswith("_comment_") | not) | .value |= strip_comments) elif type == "array" then map(strip_comments) else . end; strip_comments | .enforcement = $enf' \
        "$infile"
    else
      jq 'def strip_comments: if type == "object" then with_entries(select(.key | startswith("_comment_") | not) | .value |= strip_comments) elif type == "array" then map(strip_comments) else . end; strip_comments' \
        "$infile"
    fi
  else
    python - "$infile" "$enforcement_override" <<'PY'
import json, sys
infile, enf = sys.argv[1], sys.argv[2]
def strip_comments(x):
    if isinstance(x, dict):
        return {k: strip_comments(v) for k, v in x.items() if not k.startswith("_comment_")}
    if isinstance(x, list):
        return [strip_comments(i) for i in x]
    return x
with open(infile) as fh:
    obj = json.load(fh)
obj = strip_comments(obj)
if enf:
    obj["enforcement"] = enf
print(json.dumps(obj, indent=2))
PY
  fi
}

# Lookup numeric id of an existing ruleset by name (empty string if none).
lookup_ruleset_id() {
  local name="$1"
  if command -v jq >/dev/null 2>&1; then
    gh api "repos/$REPO/rulesets" \
      | jq -r --arg n "$name" '.[] | select(.name == $n) | .id' \
      | head -n1
  else
    gh api "repos/$REPO/rulesets" \
      | python -c "import json,sys; n=sys.argv[1]; print(next((str(r['id']) for r in json.load(sys.stdin) if r['name']==n), ''))" "$name"
  fi
}

echo "Validating $RULESET..."
json_validate "$RULESET"

# Resolve rule name from the JSON (post-shaping name is identical).
RULE_NAME="${RULE_NAME:-$(shape_payload "$RULESET" | python -c 'import json,sys;print(json.load(sys.stdin).get("name","main-protection"))')}"

# --delete short-circuit: look up the ruleset by name and DELETE it (or no-op).
# Runs BEFORE payload shaping because delete needs no payload.
if [ "$DELETE" -eq 1 ]; then
  echo ""
  echo "Repo:    $REPO"
  echo "Ruleset: $RULE_NAME"
  echo "Mode:    --delete (rollback)"
  echo ""
  echo "Looking up existing ruleset id by name..."
  DELETE_ID="$(lookup_ruleset_id "$RULE_NAME" || true)"
  if [ -z "$DELETE_ID" ]; then
    echo "No ruleset named '$RULE_NAME' to delete."
    exit 0
  fi
  echo "Found ruleset id=$DELETE_ID — issuing DELETE."
  gh api -X DELETE "repos/$REPO/rulesets/$DELETE_ID"
  echo "Deleted ruleset '$RULE_NAME' (id=$DELETE_ID)."
  exit 0
fi

PAYLOAD_FILE="$(mktemp -t sunfish-ruleset.XXXXXX.json)"
trap 'rm -f "$PAYLOAD_FILE"' EXIT
shape_payload "$RULESET" > "$PAYLOAD_FILE"

echo ""
echo "Repo:        $REPO"
echo "Ruleset:     $RULE_NAME"
echo "Source:      $RULESET"
if [ "$EVALUATE" -eq 1 ]; then
  echo "Enforcement: evaluate (overridden by --evaluate; PRs see the rule but are not blocked)"
else
  echo "Enforcement: $(python -c 'import json,sys;print(json.load(open(sys.argv[1]))["enforcement"])' "$PAYLOAD_FILE")"
fi
echo ""

echo "Looking up existing ruleset id by name..."
EXISTING_ID="$(lookup_ruleset_id "$RULE_NAME" || true)"

if [ -n "$EXISTING_ID" ]; then
  METHOD="PATCH"
  ENDPOINT="repos/$REPO/rulesets/$EXISTING_ID"
  echo "Found existing ruleset id=$EXISTING_ID — will PATCH."
else
  METHOD="POST"
  ENDPOINT="repos/$REPO/rulesets"
  echo "No existing ruleset named '$RULE_NAME' — will POST."
fi

echo ""
echo "----- resolved payload (with _comment_* stripped) -----"
cat "$PAYLOAD_FILE"
echo "----- end payload -----"
echo ""

if [ "$DRY_RUN" -eq 1 ]; then
  echo "DRY-RUN: would run:"
  echo "  gh api -X $METHOD $ENDPOINT --input <payload-tempfile>"
  echo ""
  echo "DRY-RUN: no API mutation performed. Re-run without --dry-run to apply."
  exit 0
fi

echo "Applying via gh api -X $METHOD $ENDPOINT ..."
gh api -X "$METHOD" "$ENDPOINT" --input "$PAYLOAD_FILE"

echo ""
echo "Verifying applied ruleset..."
APPLIED_ID="${EXISTING_ID:-$(lookup_ruleset_id "$RULE_NAME")}"
echo ""
echo "Ruleset id:    $APPLIED_ID"
echo "Name:          $(gh api "repos/$REPO/rulesets/$APPLIED_ID" -q '.name')"
echo "Enforcement:   $(gh api "repos/$REPO/rulesets/$APPLIED_ID" -q '.enforcement')"
echo "Target:        $(gh api "repos/$REPO/rulesets/$APPLIED_ID" -q '.target')"
echo "Conditions:    $(gh api "repos/$REPO/rulesets/$APPLIED_ID" -q '.conditions')"
echo "Rule types:    $(gh api "repos/$REPO/rulesets/$APPLIED_ID" -q '[.rules[].type] | join(", ")')"
echo "Required ctx:  $(gh api "repos/$REPO/rulesets/$APPLIED_ID" -q '[.rules[] | select(.type=="required_status_checks") | .parameters.required_status_checks[].context] | join(" | ")')"

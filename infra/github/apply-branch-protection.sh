#!/usr/bin/env bash
set -euo pipefail

# Reproducible application of the main branch-protection rule.
# Idempotent: re-running with the same JSON produces the same state.
# This script is REVIEWED + APPROVED by the human owner before each run.

REPO="${REPO:-ctwoodwa/Sunfish}"
BRANCH="${BRANCH:-main}"
RULE_FILE="${RULE_FILE:-infra/github/branch-protection-main.json}"

if [ ! -f "$RULE_FILE" ]; then
  echo "ERROR: rule file not found: $RULE_FILE" >&2
  exit 2
fi

# Validate JSON
jq . "$RULE_FILE" > /dev/null 2>&1 || { echo "ERROR: $RULE_FILE is not valid JSON" >&2; exit 3; }

echo "Applying branch protection from $RULE_FILE to $REPO@$BRANCH"
gh api -X PUT "repos/$REPO/branches/$BRANCH/protection" --input "$RULE_FILE"

echo ""
echo "Verifying applied rule..."
gh api "repos/$REPO/branches/$BRANCH/protection" -q '.required_status_checks.contexts'

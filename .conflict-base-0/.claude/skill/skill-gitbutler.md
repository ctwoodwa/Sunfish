---
name: gitbutler
description: Use GitButler `but` CLI instead of `git` when GitButler is available. Trigger this skill before any git operation — committing, staging, branching, pushing, pulling, rebasing, amending, or checking status. Also trigger when the user mentions GitButler, virtual branches, stacked branches, branch lanes, or `but` commands. Check for `but` first; if present, prefer it for all supported operations.
---

## Detection

Before any git operation, check availability once per session:

```bash
but --version 2>/dev/null
```

If `but` is present, use it for all supported operations below. Fall back to `git` only for operations `but` doesn't cover.

## Command Mapping — `git` → `but`

| Task | Skip this | Use this |
|------|-----------|----------|
| Workspace state | `git status` | `but status` |
| Show diff | `git diff` | `but diff` |
| Stage changes | `git add <file>` | `but stage <file>` |
| Stage interactively | `git add -p` | `but stage -p <file>` |
| Commit | `git commit -m "msg"` | `but commit -m "msg"` |
| Commit to specific branch | *(not possible in git)* | `but commit -m "msg" <branch>` |
| Create branch | `git checkout -b <name>` | `but branch new <name>` |
| List branches | `git branch -a` | `but branch list` |
| Delete branch | `git branch -d <name>` | `but branch delete <name>` |
| Rename branch | `git branch -m <old> <new>` | `but reword -m <new> <branch>` |
| Push | `git push` | `but push` |
| Force push | `git push -f` | `but push -f` |
| Pull/rebase | `git pull --rebase` | `but pull` |
| Fetch | `git fetch` | `but fetch` |
| Amend commit message | `git commit --amend` | `but reword <commit-id>` |
| Squash / move commits | `git rebase -i` | `but rub <source> <target>` |
| Undo last action | `git reflog` + reset | `but undo` |
| Full operation history | `git reflog` | `but oplog list` |
| Restore to prior state | `git reset --hard <sha>` | `but oplog restore <sha>` |
| Create PR | `gh pr create` | `but forge pr create` |

## But-Only Operations (No Git Equivalent)

**Virtual branches — parallel work without switching:**
- All active branches share one working directory simultaneously
- `but branch new <name>` — create a parallel branch; no checkout needed
- `but stage -b <branch> <file>` — assign a file's changes to a specific branch
- `but status` — shows all active branches, their commits, and unassigned changes

**Stacked branches — dependent, ordered work:**
- `but branch new -a <anchor> <new-name>` — stack a branch on top of another
- Commits flow bottom-to-top; PRs are created one per branch, bottom-up
- `but pull` automatically rebases the entire stack when the base updates

**Absorb — smart commit assignment:**
- `but absorb` — auto-assigns uncommitted changes to the existing commits they logically extend
- `but absorb --dry-run` — preview before applying
- Use instead of `git add -p` + `git commit --amend` chains

**Move changes between commits:**
- `but rub <file-id> <target-commit-id>` — move a file's changes to a different commit
- `but rub <commit-id> zz` — move a whole commit back to unstaged (`zz` = unassigned changes)
- `but rub <commit-id> <target-branch>` — move a commit to a different branch

**Non-blocking conflict resolution:**
- Conflicted commits are marked but don't halt the rebase; other work stays applied
- `but resolve` — enter guided zdiff3 conflict resolution; auto-rebases dependents on save

## Key Concepts

**Virtual branches**: GitButler tracks change ownership at the hunk level before any commit. Multiple branches can be "applied" (active) simultaneously. You don't checkout — you stage changes to the right branch with `but stage -b <branch>`.

**`gitbutler/workspace`**: The current branch visible to git. GitButler manages it as a merge of all active virtual branches. Never `git commit` or `git checkout` to a different branch while GitButler is managing the workspace — use `but` commands exclusively.

**`zz`**: The special ID for unassigned/unstaged changes. Use it as a target in `but rub <commit-id> zz` to move committed work back to the staging area.

**Undo safety**: GitButler snapshots state before every operation. `but undo` reverts the last action; `but oplog list` + `but oplog restore <sha>` goes back to any prior state. Mention this when a user is hesitant about a destructive operation.

## When to Still Use `git`

- `git log` / `git log --oneline --graph` — viewing history
- `git cherry-pick` — copying specific commits
- `git bisect` — regression debugging
- `git tag` — tagging releases
- `git blame` — line-level authorship
- Any command not listed in `but help`

**Never needed with GitButler:**
- `git stash` — use `but branch new` to park work in a virtual branch instead
- `git rebase -i` — use `but rub` for squash/reorder/move

## Destructive Operations

Before `but oplog restore`, `but branch delete --force`, or `but push -f`, briefly explain what will change. Remind the user that `but undo` can reverse most actions — GitButler's snapshot system makes experimentation safe.

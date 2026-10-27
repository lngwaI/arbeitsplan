#!/bin/bash
# Wartet auf den Workflow-Lauf zu einem Commit und zeigt Ergebnis + Annotationen; laedt Testbilder.
# Aufruf: bash scripts/ci-ergebnis.sh <sha> <branch> <zielordner>   (Logs/Artefakte selbst brauchen ein GitHub-Konto)
# Privates Repo: Token aus GITHUB_TOKEN, sonst aus der Git-Anmeldung (wie bei git push); oeffentlich geht es auch ohne.
tok=${GITHUB_TOKEN:-$(printf 'protocol=https\nhost=github.com\n\n' | GIT_TERMINAL_PROMPT=0 GCM_INTERACTIVE=never git credential fill 2>/dev/null | sed -n 's/^password=//p')}
auth=(); [ -n "$tok" ] && auth=(-H "Authorization: Bearer $tok")
sha=$1; br=$2; out=$3; repo=lngwaI/arbeitsplan
case "$out" in /*|?:*) ;; *) out="$PWD/$out";; esac
sleep 100
for i in $(seq 1 30); do
  r=$(curl -s "${auth[@]}" "https://api.github.com/repos/$repo/actions/runs?head_sha=$sha&per_page=1")
  st=$(echo "$r" | grep -m1 '"status"' | cut -d'"' -f4)
  [ "$st" = "completed" ] && break
  sleep 40
done
echo "status=$st conclusion=$(echo "$r" | grep -m1 '"conclusion"' | cut -d'"' -f4)"
run=$(echo "$r" | grep -m1 '"id"' | tr -dc 0-9)
job=$(curl -s "${auth[@]}" "https://api.github.com/repos/$repo/actions/runs/$run/jobs" | grep -m1 '"id"' | tr -dc 0-9)
curl -s "${auth[@]}" "https://api.github.com/repos/$repo/check-runs/$job/annotations" | grep '"message"' | grep -v Node.js
mkdir -p "$out"; rm -f "$out"/*
cd "$(dirname "$0")/.."
git fetch -q -f origin "refs/heads/bilder/$br:refs/remotes/origin/bilder-ci"
for f in $(git ls-tree --name-only origin/bilder-ci); do git show "origin/bilder-ci:$f" > "$out/$f"; done
git log -1 --format=%s origin/bilder-ci
ls "$out"

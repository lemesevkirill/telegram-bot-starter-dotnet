## Repository policy:

- NEVER run git commit, git push, git add, git merge, git rebase or any other git command.
- The assistant is allowed to MODIFY FILES in the working tree only.
- The user performs all commits manually.
- If changes are required, leave them as uncommitted modifications.

Violating this rule is not allowed.

## README Policy

README must always describe the **current state of the system**.

README is a snapshot of how the system works today.  
It must not contain development history, iteration notes, or change descriptions.

Historical information belongs only in the iteration documents:

docs/codex/ITERATION_*.md

Do not write phrases such as:

- "Iteration X introduced..."
- "Previously the system..."
- "This iteration adds..."

README should read as if the system has always worked this way. 
If historical context is required, reference the iteration documents instead of embedding history in README.
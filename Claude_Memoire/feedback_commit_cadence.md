---
name: feedback-commit-cadence
description: "Don't offer to commit after every small batch of work — wait for more substantial progress or an explicit ask."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 40fe5eb3-6995-4ff7-8b66-5c6c1eab823b
  modified: 2026-09-17T17:14:36.163Z
---

Stop proactively asking "tu veux que je commit ça ?" after every small fix. The user said explicitly: "Arrete de vouloir commit à chaque fois alors qu'on a presque rien fait" (2026-09-17).

**Why:** offering to commit after a couple of small tweaks feels premature/annoying when the amount of actual work done is minor. Commits should track meaningful, self-contained batches of work, not every individual fix.

**How to apply:** Only bring up committing when (a) the user explicitly asks, or (b) a genuinely substantial, self-contained chunk of work has just wrapped up (e.g., a full feature pass, not a couple of one-line bug fixes). When in doubt, don't offer — let the user ask. This project also already has an established rule (from earlier sessions) to only commit when explicitly asked; this feedback reinforces that the threshold for even *offering* should be higher too.

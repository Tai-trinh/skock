---
status: accepted
---
# Interface-first client architecture

Every stateful client class with a plausible online counterpart is coded against an interface from day one. The offline adapter is the only implementation now; the online adapter slots in with a one-line swap in RunState._Ready(). Alternative considered: build concrete classes first, extract interfaces when needed. Rejected: retrofitting interfaces after multiple concrete classes exist is consistently deferred indefinitely; the upfront cost is one extra file per seam, which is cheap compared to the migration cost later.

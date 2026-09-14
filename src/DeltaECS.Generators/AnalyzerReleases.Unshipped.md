# Release tracking

## Unshipped

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
DECSGEN001 | ForEach | Error | Reports unsupported demand-driven ForEach call shapes
DECSGEN003 | ForEach | Error | Rejects functors with multiple supported Invoke overloads
DECSGEN004 | ForEach | Error | Rejects functors inaccessible to generated callback code
DECSGEN005 | ForEach | Info | Explains why an opt-in Roslyn interception site used the delegate fallback
DECSGEN006 | Where | Error | Rejects writable component parameters in query predicates
DECSGEN007 | Where | Error | Requires `WhereEntity` for entity parameters in query predicates

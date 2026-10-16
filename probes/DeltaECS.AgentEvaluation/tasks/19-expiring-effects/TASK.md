# Tick and remove configured expiring effects

Each entity may carry several effect components. Their CLR types are not known to the solution in advance. The task-local effect interface defines a remaining-turn counter and a per-turn tick. The registry has several configured registrations for multiple effect types, including two registrations backed by the same CLR type. Every call must tick each currently present configured effect once, then remove each effect whose counter reached zero. Do not destroy entities or remove other effects or components.

The registration list is fixed before `Solution` construction; entity membership and population remain live. The configured effect types implement a task-local interface, and their interface relationships are established before the solution is constructed. Prepare reusable processing during construction. Do not hardcode IDs or inspect component values through per-entity type-erased getters.

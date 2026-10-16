# Change membership from updated values

For every current enemy with `Health`, first increase health by 5. After all value updates finish, transition memberships from the updated values: entities with health at least 10 must gain `Marked` and lose `Paused`; entities below 10 must lose `Marked` and retain their existing `Paused` membership. No other membership or value may change.

Construct the solution once. Component membership and entity population can change after construction; each call must select from current world state. Complete the value stage before making any structural change, then base membership decisions on the resulting values. Preserve non-enemy entities and unrelated components.

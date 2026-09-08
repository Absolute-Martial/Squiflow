# Workflow Design and Continuation

**Version:** v0.0.15

Design workflows from the user's continuation journey, not from a diagram alone.

For every non-terminal state answer:

1. Who can continue it?
2. Where do they discover it?
3. What action continues it?
4. What data is required?
5. What if nobody acts?
6. Is there a deadline/escalation?
7. Can it be reassigned/delegated?
8. Can it be cancelled, or must a compensating action be created?
9. What if two actors act simultaneously?
10. What if permission, underlying entity, rule version or workflow version changes?
11. How can the user/support understand why the item is here?

Human work should be discoverable through an appropriate work inbox rather than requiring users to rediscover the original record.

Use configurable tenant workflow stages alongside protected canonical business states. Do not let a custom stage redefine payment/stock/security truth.

Active workflow instances remain pinned to their definition version unless an explicit migration exists.

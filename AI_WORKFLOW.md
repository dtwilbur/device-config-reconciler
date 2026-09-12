# AI Workflow Note

Used Claude Code for most of this: scaffolding the projects, the reconciliation engine, tests,
and these docs.

Kept the structure and retry logic mostly as generated, it matched what I'd have built by hand
and the test cases lined up with the failure modes in the assignment. Didn't throw anything
substantial away, no competing draft to pick between.

Verified by actually running things rather than trusting it compiled: `dotnet build` / `dotnet
test` after each chunk, then manually curled the running API (POST /events, GET /devices, replayed
a stale version) and read the console logs to confirm retry/drop/give-up actually happened, not
just that the tests were green.

One thing I changed after looking at it: switched the device status field to serialize as a
string instead of a raw enum int, after seeing what a GET /devices/{id} response actually looked
like and deciding a number wasn't good enough for "can someone on-call tell what's wrong."

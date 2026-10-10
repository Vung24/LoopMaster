# Persistence and serialization

## Choose checkpoints and preserve transactions

- Inspect callers before changing flush frequency. Skip unchanged values; mark ordinary settings dirty and coalesce writes at appropriate checkpoints. Set a bounded delay according to acceptable data loss rather than saving continuously or using an arbitrary universal interval.
- PlayerPrefs is suitable for small settings, flags and scalar progress. Its getters are not proof of disk reads on each call. PlayerPrefs.Save is a synchronous flush; keep Unity API access on the main thread. Do not move PlayerPrefs calls into Task.Run.
- Persist important changes during the session and on appropriate pause transitions; quit callbacks alone are insufficient on mobile. A coroutine, UniTask or async method does not itself move synchronous serialization/I/O off the main thread. Starting unawaited work during shutdown is not a durability guarantee.
- LoopMaster RewardTransactions.cs implements journal, balances, receipt and journal-cleanup flush boundaries. Preserve their order, replay behavior, claim IDs and idempotency. Do not merge them into the settings debounce path without proving crash recovery at each boundary. PlayerPrefs is not a general transactional database.
- Check BaseUserDataManager, BaseHeartManager and GameProgressStore for redundant saves, while retaining economy and level-completion durability. Reinspect current implementations instead of assuming earlier audit findings still hold.

## Async and file saves

- Take a consistent snapshot of plain C# data on the main thread. Serialize/compress and perform file I/O on a worker only when the platform/runtime supports it; do not access Unity objects there. Apply loaded data to Unity on the main thread. Verify WebGL and IL2CPP behavior rather than assuming desktop threading support.
- Serialize writes through one owner/queue. Use revisions to prevent older snapshots overwriting newer ones; clear dirty state only after the relevant revision commits. Bound the queue and coalesce replaceable pending settings snapshots without dropping transactions.
- During reset or profile changes, invalidate pending work using a generation and serialize reset with commits so a late task cannot recreate deleted data.
- For file-backed saves, write a temporary file beside the destination, close/flush as appropriate, validate and replace using platform-supported semantics. Maintain a known-good backup when warranted. Do not call arbitrary delete-then-move sequences atomic or assume flush guarantees survival of power loss on every device.
- Validate file size, schema and domain ranges before applying loaded data. Preserve corrupt or unsupported-newer saves for recovery; avoid silently overwriting them with defaults. Keep error handling visible and retries bounded.

## Format and data layout

- Separate authored configuration from player state. ScriptableObject is for shared configuration, not a deployed-build persistence mechanism. Partition config/save by ownership and lifecycle; preserve consistency when a transaction spans modules.
- For small state, a full snapshot is often simpler than incremental patches. Use deltas or streaming/chunks when workload measurements justify them, with explicit reconstruction, compaction and recovery rules. Never read or rewrite large files in Update.
- Prefer current serializers until size, parse time, allocation or interoperability measurements justify change. Benchmark representative data and deployed builds. Compression saves bytes but costs CPU and temporary buffers; do not enable it universally.
- Newtonsoft supports DefaultValueHandling.Ignore and NullValueHandling.Ignore. JsonUtility has no equivalent configurable default omission. Define missing-field semantics before omission: distinguish false/zero/null, absent fields, application defaults and explicit cleared values. Test round-trip and migration; avoid applying sparse JSON onto an old object unless patch semantics are intended.
- Protobuf is binary serialization, not compression. If adopting it, maintain stable field numbers, reserve removed fields, define presence/default semantics and validate generated code under IL2CPP/stripping. SQLite is for justified queries, indexes or transactional relational data, not a mandatory replacement for small saves.
- Include a schema version; migrate explicitly and idempotently. Keep stable IDs rather than runtime instance IDs or hierarchy names in saves.
- Encryption/obfuscation is separate from performance. Specify the threat model; use authenticated encryption when needed and supported. Checksums detect accidental damage, not malicious edits; client-held secrets do not establish trusted server economy state.
- Reset only owned save data, backups and temporary files. Account for platform backup/restore behavior; do not depend on an uninstall callback. Do not automatically prune claim receipts if old claims could become valid again.

## Verification

When changing persistence behavior, test unchanged-value writes, checkpoint flushes, save/load round-trip, legacy migration, missing fields, malformed saves, overlapping requests and reset during a pending save. For economy transactions inject interruption at each existing commit boundary and verify restart produces neither duplicated nor lost rewards. Test I/O failures and report the chosen durability limitations.

## Official references

- [Unity JSON serialization 2022.3](https://docs.unity3d.com/2022.3/Documentation/Manual/JSONSerialization.html)
- [PlayerPrefs.Save](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/PlayerPrefs.Save.html)
- [Newtonsoft default handling](https://www.newtonsoft.com/JSON/help/html/T_Newtonsoft_Json_DefaultValueHandling.htm)
- [Protobuf overview](https://protobuf.dev/overview/)

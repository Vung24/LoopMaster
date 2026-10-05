# LoopMaster Coding & Agent Instructions

## Hierarchy Resolution Rule: NEVER USE `Find`
- Never write `transform.Find(...)` or `GameObject.Find(...)` in C# scripts.
- Use serialized fields (`[SerializeField]`) or `GetComponent` / `GetComponentInChildren`.
- When dynamic name-based lookup is needed, use `HierarchyReferenceCache` (`Child`, `Descendant`, `Path`, `ChildComponent`, `DescendantComponent`).
- Backend verification `ReverseEngineering/Verification/CoreRulesVerification.csproj` enforces `VerifyNoFindPolicy()`.

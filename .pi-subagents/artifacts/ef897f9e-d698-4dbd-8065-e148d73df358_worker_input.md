# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
You are the sole implementation writer. Implement the approved P0 browser authentication vertical slice from this exact plan: /home/adnan/Projects/Inventory-Flow/.pi-subagents/artifacts/outputs/65ebbd79-8d00-420c-9b17-ecb25ac38889/plan.md. Scope is open self-service registration, login, me, logout, short-lived JWT access tokens, HttpOnly refresh-cookie rotation with family replay revocation and concurrency protection, protected SPA routes, memory-only session restoration/401 refresh retry, real Topbar identity/signout, and focused tests. Do not expand into roles, recovery, tenancy, inventory APIs, or frontend test infrastructure. Preserve Clean Architecture, keep secrets out of tracked files, generate EF migration rather than hand-editing a previous migration, and follow the dotnet-best-practices skill where compatible with existing project conventions. Use a single writer worktree only (this one); do not launch subagents. Run all viable backend and frontend validation commands. If SQL-backed integration testing requires unavailable Docker/SQL Server, still implement tests/fixture cleanly and explicitly report that environmental limitation. Return changed files, validation commands/results, important design decisions, remaining limitations, and commit status.

## Acceptance Contract
Acceptance level: reviewed
Completion is not accepted from prose alone. End with a structured acceptance report.

Criteria:
- criterion-1: Implement the requested change without widening scope
- criterion-2: Return evidence sufficient for an independent acceptance review

Required evidence: changed-files, tests-added, commands-run, validation-output, residual-risks, no-staged-files

Review gate: required by reviewer.

Finish with a fenced JSON block tagged `acceptance-report` in this shape:
Use empty arrays when no items apply; array fields contain strings unless object entries are shown.
`criteriaSatisfied[].status` must be exactly one of: satisfied, not-satisfied, not-applicable.
`commandsRun[].result` must be exactly one of: passed, failed, not-run.
`manualNotes` and `notes` are optional strings; an empty string means no note and does not satisfy `manual-notes` evidence.
```acceptance-report
{
  "criteriaSatisfied": [
    {
      "id": "criterion-1",
      "status": "satisfied",
      "evidence": "specific proof"
    },
    {
      "id": "criterion-2",
      "status": "satisfied",
      "evidence": "specific proof"
    }
  ],
  "changedFiles": [
    "src/file.ts"
  ],
  "testsAddedOrUpdated": [
    "test/file.test.ts"
  ],
  "commandsRun": [
    {
      "command": "command",
      "result": "passed",
      "summary": "short result"
    }
  ],
  "validationOutput": [
    "validation output or concise summary"
  ],
  "residualRisks": [
    "none"
  ],
  "noStagedFiles": true,
  "diffSummary": "short description of the diff",
  "reviewFindings": [
    "blocker: file.ts:12 - issue found, or no blockers"
  ],
  "manualNotes": "anything else the parent should know"
}
```
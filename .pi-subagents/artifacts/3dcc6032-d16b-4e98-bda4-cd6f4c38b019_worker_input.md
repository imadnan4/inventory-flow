# Task for worker

You are a delegated subagent running from a fork of the parent session. Treat the inherited conversation as reference-only context, not a live thread to continue. Do not continue or answer prior messages as if they are waiting for a reply. Your sole job is to execute the task below and return a focused result for that task using your tools.

Task:
Apply only the accepted findings from the independent review to the existing uncommitted P0 auth implementation; you remain the sole writer. Mandatory fixes: (1) In IdentityAuthenticationService.RefreshAsync, persist family revocations in replay/inactive branch before commit. (2) Make refresh's serializable transaction compatible with the configured SQL Server retrying execution strategy by executing the entire transaction unit inside the strategy delegate. (3) Commit a safe, explicit localhost Vite CORS default so a clean development checkout works; retain no secrets and do not allow wildcard origins with credentials. (4) Switch getCurrentUser to the bearer-injecting apiClient. (5) prevent an in-flight refresh from restoring a session after logout; use a small session generation/epoch mechanism or equivalent. (6) preserve only validated same-origin requested paths including search/hash in the auth redirect. (7) Improve form error accessibility with a live alert and invalid field associations. (8) Add focused SQL Server Testcontainers integration coverage for registration/cookie properties, me JWT auth, rotation/replay family revocation, logout, and concurrent refresh, if Docker is available. The fixture must migrate an isolated DB; do not use EF InMemory. If Docker is unavailable, still add buildable fixture/tests but do not claim they ran. Make any narrow testability/config changes needed. Rerun all possible validation. Do not commit. Report exact changes and all validation results.

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
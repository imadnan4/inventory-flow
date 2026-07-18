# Task for planner

Create an implementation-ready plan for the approved P0 authentication slice in this repository. Scope: register, login, HttpOnly refresh-cookie rotation with replay-family revocation and concurrency safety, logout, me, JWT bearer auth, SPA login/register + session restoration/route guard/interceptor, focused tests. Inspect the actual project and the auth review artifacts under .pi-subagents/artifacts/outputs/ba19ad26-5a56-40c6-8e45-0db7843b77e2/parallel-0/. Do not edit project files. Choose concrete minimal architecture compatible with the current Clean Architecture and call out package/config/migration/test changes, secure defaults, and sequencing. Avoid scope creep such as role enforcement, recovery, tenant model, or a frontend test framework unless essential.

---
**Output:**
Write your findings to exactly this path: /home/adnan/Projects/Inventory-Flow/.pi-subagents/artifacts/outputs/65ebbd79-8d00-420c-9b17-ecb25ac38889/plan.md
This path is authoritative for this run.
Ignore any other output filename or output path mentioned elsewhere, including output destinations in the base agent prompt, system prompt, or task instructions.

## Acceptance Contract
Acceptance level: attested
Completion is not accepted from prose alone. End with a structured acceptance report.

Criteria:
- criterion-1: Return concrete findings with file paths and severity when applicable

Required evidence: review-findings, residual-risks

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
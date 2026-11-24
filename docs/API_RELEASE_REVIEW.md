# API release reconciliation — 2026-11-24

This is a read-only inventory against the running local API, not an access-rule
or DTO approval. `tools/deployment/audit_api_routes.py` reads an OpenAPI export
and `docs/api-register.json`; its private report is
`work/deployment/api-contract-audit-final-20261124.json`.

The complete register has 134 entries including later additions: 121 concrete
routes are present, two health probes are checked separately from OpenAPI,
and 11 shorthand entries require expansion. The API exposes 246 operations.
These counts must not be presented as the Georgian baseline's completion count.

## Exact management shorthand expansion

PDF page 12 names nine resources. Of their 45 literal method/path combinations,
28 exist exactly. A dash means that exact route is absent, not necessarily that
the underlying feature is absent. Nested operations are recorded below rather
than silently treated as an approved specification amendment.

| Resource | GET list | POST list | GET id | PUT id | DELETE id |
| --- | --- | --- | --- | --- | --- |
| courses | yes | — | — | yes | — |
| units | yes | yes | yes | yes | — |
| lessons | yes | yes | yes | yes | yes |
| exercises | — | — | yes | — | — |
| vocabulary | yes | — | — | — | — |
| skills | yes | yes | yes | yes | yes |
| dialogues | — | — | — | — | — |
| achievements | yes | yes | yes | yes | yes |
| challenges | yes | yes | yes | yes | yes |

- Course and unit archive actions use `PUT .../{id}/archive`. Course creation
  outside the existing seeded courses and a staff course detail contract remain
  unfinished; archive does not automatically establish every DELETE contract.
- Exercise creation/update/deletion are scoped beneath their parent lesson:
  `POST lessons/{id}/exercises`, `PUT/DELETE lessons/{id}/exercises/{exerciseId}`.
  This is implemented behavior, but its path difference needs reconciliation.
- Vocabulary edits use protected draft revisions beneath
  `/manage/vocabulary/revisions`. This does not establish all five literal
  `/manage/vocabulary` contracts.
- Learner branching dialogues exist separately; this export contains no
  `/manage/dialogues` authoring routes. Learner dialogue completion is not
  evidence of staff dialogue CRUD.
- Session, collection-word and administrator-report optional-path shorthand
  still needs method-specific list/detail reconciliation.

REQ-040/088/089/090 and API-072 through API-076 remain partial. Completion
requires the actual agreed behavior, DTOs, pagination/status checks and role,
language-scope and ownership denial tests, not aliases added only for a count.

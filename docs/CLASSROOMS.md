# Classrooms

The first successful `POST /api/v1/classes` returns `201 Created` with a
`Location` header pointing to the new class. A request retried with the
same key and details returns `200 OK` with the original class ID.

Assignment creation follows the same pattern. Its `201 Created`
response links to the assignment detail for the teacher and active
class members. An identical retry returns `200 OK`. Reusing the key
with changed details returns `409 Conflict`.

Teachers create a class for a published course, edit its name and description, and archive it. The teacher must own the class and use a current MFA-verified session. Learners join with a code, see their own classes, and can leave without losing course progress. A teacher can remove a member and restore a removal. Leaving voluntarily requires the learner to join again. Archiving revokes invitations and preserves historical membership.

Codes are generated from 20 random bytes, displayed once, and stored as SHA-256 hashes. Rotation revokes the previous code. The server enforces expiry and usage under a class row lock, after locking the acting account. A repeated join by an active member does not consume another use. Learners cannot see the roster, invitation metadata, or other classes. Mutations and audit records commit together.

| Method | Route |
| --- | --- |
| GET, POST | /api/v1/classes |
| GET, PATCH | /api/v1/classes/{id} |
| POST | /api/v1/classes/{id}/invitations |
| POST | /api/v1/classes/join |
| DELETE, PATCH | /api/v1/classes/{id}/members/{userId} |

Membership PATCH supports restoring a teacher-removed member. Classroom records are backed by PostgreSQL. The Angular class list and details live under `/classes`.

The solution build passed with zero warnings and errors. The full backend run passed 456 tests, the final classroom run passed 11 PostgreSQL tests, and 282 Angular tests passed. Local readiness returned 200 and Swagger listed all eight operations. Migration 20261103143610_AddClassrooms brought the local count to 29 with no EF model drift. The existing 1 XP transaction, 8 sessions and 27 answers were preserved. The local database has zero classroom rows. The offline source-secret scan passed.

Teacher behavior was tested with isolated accounts. A walkthrough in a live authorized teacher session is pending. Assignment submission, teacher results, feedback and related notices have their own acceptance checks.

## Assignments

From a class, select **View assignments**. Members can read the deadline, minimum score, and lesson list. **Open lesson** starts or resumes the standard course session, which enforces the existing prerequisite and heart rules. Course sessions and assignment submissions are separate records. Assignment submission is pending.

An owner teacher can select **New assignment**, pick up to 20 published lessons across units, set a deadline in local time, and enter a minimum score. The browser sends the deadline in UTC and keeps the same request key for a retry from the open form. The server verifies the course, lesson publication, class status, owner and MFA before saving.

Open an assignment and select **Edit assignment** to update its title, deadline, minimum score, or lesson selection. A changed deadline must be in the future. The form preserves an unchanged deadline at its stored precision. The server checks the displayed revision under the class lock and saves the edit, lesson links and audit event in one transaction. A conflicting edit requires reopening the assignment to see its latest values. Archived classrooms remain read-only.

### Learner submissions

Start the assigned lesson after joining the class and after the assignment is posted. After completing it, return to the assignment and choose **Submit attempt**. The panel shows the latest eligible unsubmitted attempt for each lesson and the latest receipt. Learners can explicitly submit independently completed practice for the same assigned lesson. An attempt must belong to the learner, be complete, and include a graded answer. The teacher receives assignment-linked attempts through the separate results workflow.

The server records the submission time and marks it late only after the deadline. A retry returns the same receipt. PostgreSQL enforces uniqueness per assignment, learner and session, and the session foreign key also enforces ownership. The first submission freezes the deadline, score threshold and lesson selection so later changes cannot alter the grading basis of work already shared. Teachers can still rename the assignment.

### Teacher results

The owner teacher can select **View class results**. Each roster page shows assignment status and, for each submitted lesson, the number of submitted attempts, best score, latest score, graded accuracy and late status. Completion requires a submitted score at or above the minimum for every assigned lesson. Past members with submissions remain on the report so their assignment history is available to the owner. The results endpoint requires the current MFA-verified owner and returns assignment-linked aggregates.

The teacher can leave one written note on the latest submission for each lesson of a current class member. The server saves the note and one in-app notification in the same transaction. Retrying the same text returns the saved note. Learners see their 20 most recent feedback notes for that assignment, including notes on earlier attempts. Archived classes preserve existing notes for members who retain access.

Current class members with active, confirmed accounts receive an inbox notice when the teacher creates an assignment. The deadline worker checks every minute for unfinished assignments due within the next 24 hours. It skips archived classes and uses the same passing-score rule as the teacher results. Each learner receives at most one deadline notice per assignment, even if the teacher changes the deadline. Assignment notices link to the Classes page.

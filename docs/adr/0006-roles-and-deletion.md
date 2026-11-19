# 0006 Roles, deleting and the audit log

Status: accepted

| Who | Can |
| --- | --- |
| Anyone | Use homes and the toys; add and edit there |
| Signed-in account | Create projects; open projects it belongs to |
| Project member | Add and edit items, sprints and comments in the project |
| Project editor | Also delete and restore in the project, manage members |
| Home page editor | Delete and restore entries on that home |
| Administrator | Everything, everywhere; grant roles; delete for good |

- The rules are one class, `AccessPolicy`, with tests. Pages ask it; services check it again.
- Deleting is soft: `deleted_at` and `deleted_by` are set and the record disappears everywhere. Editors restore from the project settings; administrators from the site trash.
- Only administrators delete for good, and only from the trash. A project's permanent deletion cascades to everything in it.
- Every soft delete, restore and permanent deletion writes an `audit_event` in the same transaction. So do role grants, member changes, registrations, setup, password changes and failed sign-ins.
- A project always has an editor and the site always has an active administrator; the last one cannot be demoted, removed or disabled.
- Comments are edited by their author or an editor. Anything else in a project can be edited by any member.
- Home pages have no owner, so their editors are granted by an administrator per home key (`home:zillow:29747113`). Any key of a merged home counts.
- The toys keep nothing past a restart and have no delete (ADR 0008).

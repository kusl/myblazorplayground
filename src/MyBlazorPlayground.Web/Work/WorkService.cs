using MyBlazorPlayground.Data.Trash;
using MyBlazorPlayground.Data.Work;
using MyBlazorPlayground.Domain.Access;
using MyBlazorPlayground.Domain.Text;
using MyBlazorPlayground.Domain.Work;
using MyBlazorPlayground.Web.Administration;

namespace MyBlazorPlayground.Web.Work;

internal sealed class WorkService(
    ProjectStore projects,
    SprintStore sprints,
    WorkItemStore items,
    CommentStore comments,
    TrashService trash)
{
    private const string NotAllowed = "You can't do that here.";

    public async Task<ProjectAccess?> OpenAsync(string? key, Actor? actor, CancellationToken cancellationToken = default)
    {
        if (actor is null || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var project = await projects.FindAsync(WorkRules.NormalizeProjectKey(key), actor.PersonId, cancellationToken);
        return project is null || project.DeletedAt is not null || !AccessPolicy.CanView(actor, project.Role)
            ? null
            : new ProjectAccess(project, actor);
    }

    public async Task<ProjectAccess?> OpenAsync(Guid projectId, Actor? actor, CancellationToken cancellationToken = default)
    {
        if (actor is null)
        {
            return null;
        }

        var project = await projects.FindByIdAsync(projectId, actor.PersonId, cancellationToken);
        return project is null || project.DeletedAt is not null || !AccessPolicy.CanView(actor, project.Role)
            ? null
            : new ProjectAccess(project, actor);
    }

    public Task<IReadOnlyList<ProjectRecord>> ProjectsAsync(Actor actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return projects.ListAsync(actor.PersonId, actor.IsAdministrator, cancellationToken);
    }

    public async Task<(string? Problem, string? Key)> CreateProjectAsync(Actor actor, ProjectForm form, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(form);
        var key = WorkRules.NormalizeProjectKey(form.Key);
        if (WorkRules.ProjectKeyProblem(key) is { } keyProblem)
        {
            return (keyProblem, null);
        }

        if (WorkRules.ProjectNameProblem(form.Name) is { } nameProblem)
        {
            return (nameProblem, null);
        }

        var name = PlainText.Line(form.Name, WorkRules.MaxProjectName);
        var (result, _) = await projects.CreateAsync(
            key,
            name,
            WorkRules.Description(form.Description),
            actor.PersonId,
            Audits.For(actor, "create", SubjectNames.Of(Subject.Project), string.Empty, $"{key} {name}"),
            cancellationToken);
        return result == CreateProjectResult.KeyTaken ? ("Another project already uses that key.", null) : (null, key);
    }

    public async Task<string?> UpdateProjectAsync(ProjectAccess access, ProjectForm form, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(form);
        if (!access.CanManage)
        {
            return NotAllowed;
        }

        if (WorkRules.ProjectNameProblem(form.Name) is { } problem)
        {
            return problem;
        }

        await projects.UpdateAsync(access.Project.Id, PlainText.Line(form.Name, WorkRules.MaxProjectName), WorkRules.Description(form.Description), cancellationToken);
        return null;
    }

    public async Task<string?> SetMemberAsync(ProjectAccess access, string? userName, string? role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (!access.CanManage)
        {
            return NotAllowed;
        }

        if (!PageRoles.IsKnown(role))
        {
            return "Pick a role.";
        }

        var name = (userName ?? string.Empty).Trim().ToLowerInvariant();
        var change = await projects.SetMemberAsync(
            access.Project.Id,
            name,
            role!,
            access.Actor.PersonId,
            Audits.For(access.Actor, "member-set", SubjectNames.Of(Subject.Project), access.Project.Id.ToString(), $"{name} is now {PageRoles.Label(role)} of {access.Key}"),
            cancellationToken);
        return change switch
        {
            MemberChange.NotFound => "There is no active account with that user name.",
            MemberChange.LastEditor => "A project needs at least one editor.",
            _ => null,
        };
    }

    public async Task<string?> RemoveMemberAsync(ProjectAccess access, Guid personId, string userName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (!access.CanManage && personId != access.Actor.PersonId)
        {
            return NotAllowed;
        }

        var change = await projects.RemoveMemberAsync(
            access.Project.Id,
            personId,
            Audits.For(access.Actor, "member-removed", SubjectNames.Of(Subject.Project), access.Project.Id.ToString(), $"{userName} left {access.Key}"),
            cancellationToken);
        return change switch
        {
            MemberChange.LastEditor => "A project needs at least one editor.",
            MemberChange.NotFound => "That person is not a member.",
            _ => null,
        };
    }

    public async Task<string?> DeleteProjectAsync(ProjectAccess access, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (!access.CanDelete)
        {
            return NotAllowed;
        }

        await trash.SoftDeleteAsync(access.Actor, Subject.Project, access.Project.Id, $"{access.Key} {access.Project.Name}", cancellationToken);
        return null;
    }

    public async Task<(string? Problem, int Number)> CreateItemAsync(ProjectAccess access, ItemForm form, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(form);
        if (!access.CanEdit)
        {
            return (NotAllowed, 0);
        }

        if (form.Problem() is { } problem)
        {
            return (problem, 0);
        }

        var (_, number) = await items.CreateAsync(access.Project.Id, form.ToFields(), access.Actor.PersonId, cancellationToken);
        return (null, number);
    }

    public async Task<string?> UpdateItemAsync(ProjectAccess access, int number, ItemForm form, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(form);
        if (!access.CanEdit)
        {
            return NotAllowed;
        }

        if (form.Problem() is { } problem)
        {
            return problem;
        }

        var fields = form.ToFields();
        return await items.UpdateAsync(access.Project.Id, number, _ => fields, access.Actor.PersonId, cancellationToken)
            ? null
            : "That item no longer exists.";
    }

    public async Task<string?> SetStatusAsync(ProjectAccess access, int number, string? status, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (!access.CanEdit)
        {
            return NotAllowed;
        }

        if (!WorkItemStatuses.IsKnown(status))
        {
            return "Pick a status.";
        }

        return await items.UpdateAsync(access.Project.Id, number, current => current with { Status = status! }, access.Actor.PersonId, cancellationToken)
            ? null
            : "That item no longer exists.";
    }

    public async Task<string?> SetSprintAsync(ProjectAccess access, int number, Guid? sprintId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (!access.CanEdit)
        {
            return NotAllowed;
        }

        return await items.UpdateAsync(
            access.Project.Id,
            number,
            current => current with
            {
                SprintId = sprintId,
                Status = sprintId is not null && current.Status == WorkItemStatuses.Backlog ? WorkItemStatuses.ToDo : current.Status,
            },
            access.Actor.PersonId,
            cancellationToken)
            ? null
            : "That item no longer exists.";
    }

    public async Task<string?> AssignAsync(ProjectAccess access, int number, Guid? assigneeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (!access.CanEdit)
        {
            return NotAllowed;
        }

        return await items.UpdateAsync(access.Project.Id, number, current => current with { AssigneeId = assigneeId }, access.Actor.PersonId, cancellationToken)
            ? null
            : "That item no longer exists.";
    }

    public async Task<string?> MoveAsync(ProjectAccess access, int number, bool up, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (!access.CanEdit)
        {
            return NotAllowed;
        }

        await items.MoveAsync(access.Project.Id, number, up, cancellationToken);
        return null;
    }

    public async Task<string?> DeleteItemAsync(ProjectAccess access, WorkItemRecord item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(item);
        if (!access.CanDelete)
        {
            return "Only editors of this project and administrators can delete items.";
        }

        await trash.SoftDeleteAsync(access.Actor, Subject.WorkItem, item.Id, $"{access.Key}-{item.Number} {item.Title}", cancellationToken);
        return null;
    }

    public async Task<string?> AddCommentAsync(ProjectAccess access, WorkItemRecord item, string? body, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(item);
        if (!access.CanEdit)
        {
            return NotAllowed;
        }

        var text = WorkRules.Comment(body);
        if (text.Length == 0)
        {
            return "Write something first.";
        }

        await comments.AddAsync(item.Id, access.Actor.PersonId, text, cancellationToken);
        return null;
    }

    public async Task<string?> EditCommentAsync(ProjectAccess access, WorkItemRecord item, Guid commentId, string? body, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(item);
        var comment = await comments.FindAsync(item.Id, commentId, cancellationToken);
        if (comment is null)
        {
            return "That comment no longer exists.";
        }

        if (!AccessPolicy.CanEditComment(access.Actor, access.Role, comment.AuthorId ?? Guid.Empty))
        {
            return NotAllowed;
        }

        var text = WorkRules.Comment(body);
        if (text.Length == 0)
        {
            return "A comment cannot be empty. Delete it instead.";
        }

        await comments.EditAsync(item.Id, commentId, text, cancellationToken);
        return null;
    }

    public async Task<string?> DeleteCommentAsync(ProjectAccess access, WorkItemRecord item, Guid commentId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(item);
        if (!access.CanDelete)
        {
            return "Only editors of this project and administrators can delete comments.";
        }

        var comment = await comments.FindAsync(item.Id, commentId, cancellationToken);
        if (comment is null)
        {
            return null;
        }

        var preview = comment.Body.Length > 60 ? comment.Body[..60] : comment.Body;
        await trash.SoftDeleteAsync(access.Actor, Subject.Comment, commentId, $"{access.Key}-{item.Number}: {preview}", cancellationToken);
        return null;
    }

    public async Task<string?> DeleteAttachmentAsync(ProjectAccess access, AttachmentRecord attachment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(attachment);
        if (!access.CanDelete || attachment.ProjectId != access.Project.Id)
        {
            return "Only editors of this project and administrators can delete attachments.";
        }

        await trash.SoftDeleteAsync(access.Actor, Subject.Attachment, attachment.Id, $"{access.Key}: {attachment.FileName}", cancellationToken);
        return null;
    }

    public async Task<string?> CreateSprintAsync(ProjectAccess access, SprintForm form, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(form);
        if (!access.CanEdit)
        {
            return NotAllowed;
        }

        if (form.Problem() is { } problem)
        {
            return problem;
        }

        await sprints.CreateAsync(access.Project.Id, form.ToDraft(), cancellationToken);
        return null;
    }

    public async Task<string?> UpdateSprintAsync(ProjectAccess access, Guid sprintId, SprintForm form, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(form);
        if (!access.CanEdit)
        {
            return NotAllowed;
        }

        if (form.Problem() is { } problem)
        {
            return problem;
        }

        return await sprints.UpdateAsync(access.Project.Id, sprintId, form.ToDraft(), cancellationToken) ? null : "That sprint no longer exists.";
    }

    public async Task<string?> StartSprintAsync(ProjectAccess access, Guid sprintId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (!access.CanEdit)
        {
            return NotAllowed;
        }

        return await sprints.StartAsync(access.Project.Id, sprintId, cancellationToken) switch
        {
            SprintChange.AnotherIsActive => "Another sprint is already active. Complete it first.",
            SprintChange.WrongState => "Only planned sprints can be started.",
            _ => null,
        };
    }

    public async Task<string?> CompleteSprintAsync(ProjectAccess access, Guid sprintId, Guid? moveOpenTo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        if (!access.CanEdit)
        {
            return NotAllowed;
        }

        return await sprints.CompleteAsync(access.Project.Id, sprintId, moveOpenTo, cancellationToken) == SprintChange.Changed
            ? null
            : "Only the active sprint can be completed.";
    }

    public async Task<string?> DeleteSprintAsync(ProjectAccess access, SprintRecord sprint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(sprint);
        if (!access.CanDelete)
        {
            return "Only editors of this project and administrators can delete sprints.";
        }

        await trash.SoftDeleteAsync(access.Actor, Subject.Sprint, sprint.Id, $"{access.Key}: {sprint.Name}", cancellationToken);
        return null;
    }

    public async Task<string?> RestoreAsync(ProjectAccess access, TrashRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(record);
        if (!AccessPolicy.CanRestore(access.Actor, access.Role) || record.Subject == Subject.Project || record.ProjectId != access.Project.Id)
        {
            return NotAllowed;
        }

        return await trash.RestoreAsync(access.Actor, record.Subject, record.Id, cancellationToken) ? null : "That is no longer in the trash.";
    }
}

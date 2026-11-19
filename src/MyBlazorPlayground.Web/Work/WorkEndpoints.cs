using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using MyBlazorPlayground.Data.Work;
using MyBlazorPlayground.Domain.Files;
using MyBlazorPlayground.Domain.Text;
using MyBlazorPlayground.Web.Identity;

namespace MyBlazorPlayground.Web.Work;

internal static class WorkEndpoints
{
    public const string FileNameHeader = "X-File-Name";
    private const int MaxPreviewBytes = 512 * 1024;

    public static IEndpointRouteBuilder MapWorkEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/work").RequireAuthorization();
        group.MapPost("/{key}/attachments", UploadAsync).DisableAntiforgery();
        group.MapGet("/files/{id:guid}/{name?}", DownloadAsync);
        group.MapPost("/{key}/preview", PreviewAsync).DisableAntiforgery();
        return endpoints;
    }

    private static async Task<IResult> UploadAsync(
        string key,
        int? item,
        HttpContext context,
        IAntiforgery antiforgery,
        WorkService work,
        WorkItemStore items,
        AttachmentStore attachments,
        IOptions<WorkOptions> options)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.BadRequest(new { error = "The page is out of date. Reload it and try again." });
        }

        var access = await work.OpenAsync(key, context.User.ToActor(), context.RequestAborted);
        if (access is null || !access.CanEdit)
        {
            return Results.NotFound();
        }

        Guid? itemId = null;
        if (item is { } number)
        {
            var record = await items.FindAsync(access.Project.Id, number, cancellationToken: context.RequestAborted);
            if (record is null)
            {
                return Results.NotFound();
            }

            itemId = record.Id;
        }

        var max = options.Value.MaxAttachmentBytes;
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = max + 1;
        }

        if (context.Request.ContentLength is { } length && length > max)
        {
            return Results.Json(new { error = $"Files are at most {FileRules.Size(max)}." }, statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        var name = FileRules.SafeFileName(Uri.UnescapeDataString(context.Request.Headers[FileNameHeader].ToString()));
        UploadResult result;
        try
        {
            result = await attachments.UploadAsync(new AttachmentUpload(access.Project.Id, itemId, access.Actor.PersonId, name), context.Request.Body, max, context.RequestAborted);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            result = new UploadResult(UploadOutcome.TooLarge);
        }

        return result switch
        {
            { Outcome: UploadOutcome.Stored, Attachment: { } stored } => Results.Json(new
            {
                id = stored.Id,
                name = stored.FileName,
                url = FileUrl(stored),
                image = stored.IsImage,
                size = FileRules.Size(stored.Length),
            }),
            { Outcome: UploadOutcome.TooLarge } => Results.Json(new { error = $"Files are at most {FileRules.Size(max)}." }, statusCode: StatusCodes.Status413PayloadTooLarge),
            _ => Results.BadRequest(new { error = "That file is empty." }),
        };
    }

    private static async Task DownloadAsync(Guid id, HttpContext context, WorkService work, AttachmentStore attachments)
    {
        var attachment = await attachments.FindAsync(id, context.RequestAborted);
        var access = attachment is null ? null : await work.OpenAsync(attachment.ProjectId, context.User.ToActor(), context.RequestAborted);
        if (attachment is null || access is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var headers = context.Response.Headers;
        context.Response.ContentType = attachment.IsImage ? attachment.ContentType : "application/octet-stream";
        context.Response.ContentLength = attachment.Length;
        headers.CacheControl = "private, max-age=31536000, immutable";
        headers.ContentSecurityPolicy = "default-src 'none'; img-src 'self'; style-src 'unsafe-inline'; sandbox";
        headers.ContentDisposition = new ContentDispositionHeaderValue(attachment.IsImage ? "inline" : "attachment")
        {
            FileNameStar = attachment.FileName,
            FileName = Ascii(attachment.FileName),
        }.ToString();
        if (HttpMethods.IsHead(context.Request.Method))
        {
            return;
        }

        await attachments.CopyToAsync(attachment.Id, context.Response.Body, context.RequestAborted);
    }

    private static async Task<IResult> PreviewAsync(string key, HttpContext context, IAntiforgery antiforgery, WorkService work)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.BadRequest();
        }

        var access = await work.OpenAsync(key, context.User.ToActor(), context.RequestAborted);
        if (access is null)
        {
            return Results.NotFound();
        }

        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
        var buffer = new char[MaxPreviewBytes];
        var read = await reader.ReadBlockAsync(buffer.AsMemory(), context.RequestAborted);
        var html = access.RenderRich(new string(buffer, 0, read));
        return Results.Text(html.Length == 0 ? "<p class=\"muted\">Nothing to preview.</p>" : html, "text/html; charset=utf-8");
    }

    public static string FileUrl(AttachmentRecord attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        return $"/work/files/{attachment.Id:D}/{Uri.EscapeDataString(attachment.FileName)}";
    }

    private static string Ascii(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            builder.Append(c is >= ' ' and <= '~' and not '"' and not '\\' ? c : '_');
        }

        return builder.ToString();
    }
}

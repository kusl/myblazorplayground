using System.Buffers;
using System.Security.Cryptography;
using Dapper;
using MyBlazorPlayground.Domain.Files;

namespace MyBlazorPlayground.Data.Work;

public sealed class AttachmentRecord
{
    public Guid Id { get; init; }

    public Guid ProjectId { get; init; }

    public Guid? WorkItemId { get; init; }

    public string FileName { get; init; } = string.Empty;

    public string ContentType { get; init; } = "application/octet-stream";

    public long Length { get; init; }

    public string? UploadedByName { get; init; }

    public DateTime CreatedAt { get; init; }

    public bool IsImage => ContentType.StartsWith("image/", StringComparison.Ordinal);
}

public sealed record AttachmentUpload(Guid ProjectId, Guid? WorkItemId, Guid UploadedBy, string FileName);

public enum UploadOutcome
{
    Stored,
    Empty,
    TooLarge,
}

public sealed record UploadResult(UploadOutcome Outcome, AttachmentRecord? Attachment = null);

public sealed class AttachmentStore(Database database)
{
    public const int ChunkSize = 1024 * 1024;

    private const string Select =
        """
        SELECT f.id, f.project_id, f.work_item_id, f.file_name, f.content_type, f.length, p.display_name AS uploaded_by_name, f.created_at
        FROM attachment f LEFT JOIN person p ON p.id = f.uploaded_by
        """;

    public async Task<UploadResult> UploadAsync(AttachmentUpload upload, Stream content, long maxBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(content);
        var id = Guid.CreateVersion7();
        var buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
        try
        {
            await using var connection = await database.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO attachment (id, project_id, work_item_id, uploaded_by, file_name, content_type)
                VALUES (@id, @ProjectId, @WorkItemId, @UploadedBy, @FileName, 'application/octet-stream')
                """,
                new { id, upload.ProjectId, upload.WorkItemId, upload.UploadedBy, upload.FileName },
                transaction,
                cancellationToken: cancellationToken));

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long total = 0;
            var sequence = 0;
            string? sniffed = null;
            while (true)
            {
                var filled = await FillAsync(content, buffer, ChunkSize, cancellationToken);
                if (filled == 0)
                {
                    break;
                }

                total += filled;
                if (total > maxBytes)
                {
                    return new UploadResult(UploadOutcome.TooLarge);
                }

                if (sequence == 0)
                {
                    sniffed = FileRules.SniffImage(buffer.AsSpan(0, Math.Min(filled, 16)));
                }

                hash.AppendData(buffer, 0, filled);
                await connection.ExecuteAsync(new CommandDefinition(
                    "INSERT INTO attachment_chunk (attachment_id, sequence, data) VALUES (@id, @sequence, @data)",
                    new { id, sequence, data = buffer.AsSpan(0, filled).ToArray() },
                    transaction,
                    cancellationToken: cancellationToken));
                sequence++;
                if (filled < ChunkSize)
                {
                    break;
                }
            }

            if (total == 0)
            {
                return new UploadResult(UploadOutcome.Empty);
            }

            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE attachment SET length = @total, sha256 = @digest, content_type = @contentType, complete = true WHERE id = @id",
                new { id, total, digest = hash.GetHashAndReset(), contentType = sniffed ?? "application/octet-stream" },
                transaction,
                cancellationToken: cancellationToken));
            var record = await connection.QuerySingleAsync<AttachmentRecord>(new CommandDefinition(
                Select + " WHERE f.id = @id", new { id }, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return new UploadResult(UploadOutcome.Stored, record);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async Task<AttachmentRecord?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<AttachmentRecord>(new CommandDefinition(
            Select + " WHERE f.id = @id AND f.complete AND f.deleted_at IS NULL AND (f.work_item_id IS NULL OR EXISTS (SELECT 1 FROM work_item w WHERE w.id = f.work_item_id AND w.deleted_at IS NULL))",
            new { id },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AttachmentRecord>> ListAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<AttachmentRecord>(new CommandDefinition(
            Select + " WHERE f.work_item_id = @itemId AND f.complete AND f.deleted_at IS NULL ORDER BY f.created_at",
            new { itemId },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task CopyToAsync(Guid id, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        await using var connection = await database.OpenAsync(cancellationToken);
        for (var sequence = 0; ; sequence++)
        {
            var data = await connection.ExecuteScalarAsync<byte[]?>(new CommandDefinition(
                "SELECT data FROM attachment_chunk WHERE attachment_id = @id AND sequence = @sequence",
                new { id, sequence },
                cancellationToken: cancellationToken));
            if (data is null)
            {
                return;
            }

            await destination.WriteAsync(data, cancellationToken);
        }
    }

    private static async Task<int> FillAsync(Stream content, byte[] buffer, int size, CancellationToken cancellationToken)
    {
        var filled = 0;
        while (filled < size)
        {
            var read = await content.ReadAsync(buffer.AsMemory(filled, size - filled), cancellationToken);
            if (read == 0)
            {
                break;
            }

            filled += read;
        }

        return filled;
    }
}

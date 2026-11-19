namespace MyBlazorPlayground.Web.Work;

internal sealed class WorkOptions
{
    public const string SectionName = "Work";

    public long MaxAttachmentBytes { get; set; } = 95L * 1024 * 1024;

    public bool IsValid() => MaxAttachmentBytes is >= 1024 * 1024 and <= 8L * 1024 * 1024 * 1024;
}

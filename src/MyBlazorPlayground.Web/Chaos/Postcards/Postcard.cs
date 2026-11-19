using System.Text.Json.Serialization;
using MyBlazorPlayground.Web.Chaos.Messages;

namespace MyBlazorPlayground.Web.Chaos.Postcards;

internal sealed record Postcard(
    Guid Id,
    string Text,
    IReadOnlyList<PostcardImage> Images,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<MetaEntry>? Meta = null);

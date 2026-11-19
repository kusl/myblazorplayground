using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using MyBlazorPlayground.Data.Protection;

namespace MyBlazorPlayground.Web.Persistence;

internal sealed class DatabaseXmlRepository(DataProtectionKeyStore store) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements() => store.All().Select(XElement.Parse).ToList();

    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);
        store.Store(friendlyName, element.ToString(SaveOptions.DisableFormatting));
    }
}

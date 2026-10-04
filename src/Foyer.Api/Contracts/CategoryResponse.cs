using Foyer.Core.Entities;

namespace Foyer.Api.Contracts;

public sealed record CategoryResponse(int Id, string Name, int SortOrder, bool IsSystem)
{
    public static CategoryResponse From(Category c) => new(c.Id, c.Name, c.SortOrder, c.IsSystem);
}

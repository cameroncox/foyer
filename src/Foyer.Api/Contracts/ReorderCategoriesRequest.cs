namespace Foyer.Api.Contracts;

/// <summary>Saves the drawer order: every category except Uncategorized, which stays last.</summary>
public sealed record ReorderCategoriesRequest(IReadOnlyList<int> CategoryIds);

namespace Foyer.Api.Contracts;

public sealed record DashboardResponse(IReadOnlyList<DashboardCategoryResponse> Categories);

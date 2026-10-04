using Foyer.Api.Contracts;
using Foyer.Core.Services;

namespace Foyer.Api.Endpoints;

internal static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboard(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboard", async (DashboardService dashboard, CancellationToken ct) =>
                new DashboardResponse((await dashboard.GetAsync(ct)).Select(DashboardCategoryResponse.From).ToList()))
            .WithName("GetDashboard")
            .WithSummary("Categories in drawer order, each with its bookmarks on the page in order");

        return app;
    }
}

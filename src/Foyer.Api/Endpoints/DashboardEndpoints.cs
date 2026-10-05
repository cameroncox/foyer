using Foyer.Api.Contracts;
using Foyer.Core.Profiles;
using Foyer.Core.Services;

namespace Foyer.Api.Endpoints;

internal static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboard(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboard", async (DashboardService dashboard, ProfileContext profile, CancellationToken ct) =>
                new DashboardResponse((await dashboard.GetAsync(ct)).Select(c => DashboardCategoryResponse.From(c, profile)).ToList()))
            .WithName("GetDashboard")
            .WithSummary("The profile's categories in drawer order, each with its own and other profiles' shared bookmarks on the page, in order");

        return app;
    }
}

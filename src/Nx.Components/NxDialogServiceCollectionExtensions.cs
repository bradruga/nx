using Microsoft.Extensions.DependencyInjection;

namespace Nx.Components;

/// <summary>Registers <see cref="NxDialogService"/>.</summary>
public static class NxDialogServiceCollectionExtensions
{
    /// <summary>Adds <see cref="NxDialogService"/> as a scoped service (one per circuit). Put one <see cref="NxDialogHost"/> in the layout.</summary>
    public static IServiceCollection AddNxDialogs(this IServiceCollection services) => services.AddScoped<NxDialogService>();
}

using Microsoft.Extensions.DependencyInjection;

namespace Nx.Components;

/// <summary>Registers <see cref="NxDialogService"/> and what <see cref="NxWindowHost"/> needs.</summary>
public static class NxDialogServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="NxDialogService"/> as a scoped service (one per circuit); put one <see cref="NxDialogHost"/> in the layout.
    /// Also lets the layout hold an optional <see cref="NxWindowHost"/>.
    /// </summary>
    public static IServiceCollection AddNxDialogs(this IServiceCollection services) =>
        services.AddScoped<NxDialogService>().AddScoped<NxWindowHostState>();
}

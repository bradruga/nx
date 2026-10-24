namespace Nx.Components;

/// <summary>
/// What an <c>NxComboBox</c> asks its <c>DataProvider</c> for: the items matching
/// <paramref name="Filter"/>. A newer request cancels <paramref name="CancellationToken"/>.
/// </summary>
/// <param name="Filter">The typed text; empty when the list is opened without typing.</param>
/// <param name="CancellationToken">Cancelled when the user types again or the component is disposed.</param>
public sealed record NxComboBoxRequest(string Filter, CancellationToken CancellationToken);

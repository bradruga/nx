namespace Nx.Components;

/// <summary>The HTML <c>type</c> of an <see cref="NxTextBox"/>. Set via <see cref="NxTextBox.Type"/>.</summary>
public enum NxTextBoxType
{
    /// <summary><c>type="text"</c>. Default.</summary>
    Text,

    /// <summary><c>type="password"</c>. Pairs with <see cref="NxTextBox.ShowPasswordToggle"/>.</summary>
    Password,

    /// <summary><c>type="email"</c>.</summary>
    Email,

    /// <summary><c>type="search"</c>.</summary>
    Search,

    /// <summary><c>type="tel"</c>.</summary>
    Tel,

    /// <summary><c>type="url"</c>.</summary>
    Url
}

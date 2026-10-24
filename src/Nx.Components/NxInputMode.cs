namespace Nx.Components;

/// <summary>
/// The HTML <c>inputmode</c> hint: which on-screen keyboard a touch device shows.
/// Set via <see cref="NxTextBox.InputMode"/>; <c>null</c> omits the attribute.
/// </summary>
public enum NxInputMode
{
    /// <summary><c>inputmode="text"</c>. The standard keyboard.</summary>
    Text,

    /// <summary><c>inputmode="decimal"</c>. Digits and a decimal separator.</summary>
    Decimal,

    /// <summary><c>inputmode="numeric"</c>. Digits only.</summary>
    Numeric,

    /// <summary><c>inputmode="tel"</c>. A telephone keypad.</summary>
    Tel,

    /// <summary><c>inputmode="search"</c>. The Enter key reads "Search".</summary>
    Search,

    /// <summary><c>inputmode="email"</c>. Adds @ and . keys.</summary>
    Email,

    /// <summary><c>inputmode="url"</c>. Adds / and . keys.</summary>
    Url,

    /// <summary><c>inputmode="none"</c>. No on-screen keyboard.</summary>
    None
}

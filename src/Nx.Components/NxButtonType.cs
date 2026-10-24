namespace Nx.Components;

/// <summary>The HTML <c>type</c> of an <see cref="NxButton"/>. Set via <see cref="NxButton.Type"/>.</summary>
public enum NxButtonType
{
    /// <summary><c>type="button"</c>. Default — clicking never submits an enclosing form.</summary>
    Button,

    /// <summary><c>type="submit"</c>. Submits the enclosing form or <c>EditForm</c>.</summary>
    Submit,

    /// <summary><c>type="reset"</c>. Resets the enclosing native form's inputs.</summary>
    Reset
}

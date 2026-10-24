using System.Text;

namespace Nx.Components;

/// <summary>
/// An <see cref="NxMaskedTextBox"/> mask: rule characters (<c>0 9 # L ? A a &amp; C</c>) are slots for typed characters,
/// anything else is a literal, and <c>\</c> makes the next character a literal. nx-input.js's <c>editMask</c> reads the
/// same rules; keep the two in step.
/// </summary>
internal sealed class MaskPattern
{
    private readonly List<(bool IsSlot, char Char)> parts = [];

    public MaskPattern(string mask)
    {
        for (var i = 0; i < mask.Length; i++)
        {
            var c = mask[i];
            if (c == '\\' && i + 1 < mask.Length)
                parts.Add((false, mask[++i]));
            else
                parts.Add((IsRule(c), c));
        }
    }

    /// <summary>The length of the masked text: one character per slot or literal.</summary>
    public int Length => parts.Count;

    public static bool IsRule(char c) => c is '0' or '9' or '#' or 'L' or '?' or 'A' or 'a' or '&' or 'C';

    /// <summary>Whether <paramref name="c"/> may go in a slot of <paramref name="rule"/>.</summary>
    public static bool Accepts(char rule, char c) => rule switch
    {
        '0' => char.IsDigit(c),
        '9' => char.IsDigit(c) || c == ' ',
        '#' => char.IsDigit(c) || c is ' ' or '+' or '-',
        'L' => char.IsLetter(c),
        '?' => char.IsLetter(c) || c == ' ',
        'A' => char.IsLetterOrDigit(c),
        'a' => char.IsLetterOrDigit(c) || c == ' ',
        '&' => !char.IsWhiteSpace(c),
        'C' => true,
        _   => false
    };

    /// <summary>The empty masked text: the prompt in every slot, the literals between.</summary>
    public string Template(char prompt) => string.Concat(parts.Select(p => p.IsSlot ? prompt : p.Char));

    /// <summary>
    /// The masked text for a value. Characters fill the slots in order (one that does not fit its slot is skipped);
    /// with <paramref name="includeLiterals"/> the value's own literals are matched and stepped over; the prompt
    /// character leaves a slot empty.
    /// </summary>
    public string Format(string? value, bool includeLiterals, char prompt)
    {
        var text = new StringBuilder(Length);
        var source = value ?? "";
        var at = 0;
        foreach (var (isSlot, c) in parts)
        {
            if (!isSlot)
            {
                text.Append(c);
                if (includeLiterals && at < source.Length && source[at] == c)
                    at++;
                continue;
            }
            var placed = false;
            while (at < source.Length && !placed)
            {
                var next = source[at++];
                if (next == prompt)
                    break;
                if (Accepts(c, next))
                {
                    text.Append(next);
                    placed = true;
                }
            }
            if (!placed)
                text.Append(prompt);
        }
        return text.ToString();
    }

    /// <summary>
    /// The value in masked text: the filled slots (and the literals, with <paramref name="includeLiterals"/>), empty
    /// slots as the prompt, trailing empty slots and literals left off; <c>null</c> when nothing is filled.
    /// </summary>
    public string? Extract(string? text, bool includeLiterals, char prompt)
    {
        if (string.IsNullOrEmpty(text))
            return null;
        var masked = text.Length == Length ? text : Format(text, includeLiterals: true, prompt);
        var value = new StringBuilder(Length);
        var lastFilled = -1;
        for (var i = 0; i < parts.Count; i++)
        {
            var (isSlot, c) = parts[i];
            if (!isSlot)
            {
                if (includeLiterals)
                    value.Append(c);
                continue;
            }
            var typed = masked[i];
            var filled = typed != prompt && Accepts(c, typed);
            value.Append(filled ? typed : prompt);
            if (filled)
                lastFilled = value.Length - 1;
        }
        return lastFilled < 0 ? null : value.ToString(0, lastFilled + 1);
    }

    /// <summary>Every slot is filled.</summary>
    public bool IsComplete(string? text, char prompt)
    {
        if (string.IsNullOrEmpty(text) || text.Length != Length)
            return false;
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i].IsSlot && (text[i] == prompt || !Accepts(parts[i].Char, text[i])))
                return false;
        }
        return true;
    }
}

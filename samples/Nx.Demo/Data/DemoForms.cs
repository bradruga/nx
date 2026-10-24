using System.ComponentModel.DataAnnotations;

namespace Nx.Demo.Data;

public enum PourStatus { Planned, Scheduled, OnHold, Poured, Cancelled }

/// <summary>A pour, for the NxForm demos: one property of each type a form item generates an editor for.</summary>
public sealed class PourForm
{
    [Required(ErrorMessage = "Give the pour a name.")]
    [StringLength(40, ErrorMessage = "40 characters at most.")]
    public string? Name { get; set; } = "North wall, lift 2";

    [Display(Name = "Volume (yd³)")]
    [Range(1, 400, ErrorMessage = "Between 1 and 400 yd³.")]
    public decimal Volume { get; set; } = 14.5m;

    [Range(1, 12, ErrorMessage = "1 to 12 trucks.")]
    public int Trucks { get; set; } = 3;

    [Required(ErrorMessage = "Pick the pour date.")]
    public DateOnly? PourDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(3));

    public TimeOnly? StartTime { get; set; } = new(6, 30);

    public PourStatus Status { get; set; } = PourStatus.Scheduled;

    [Display(Name = "Pump truck needed")]
    public bool PumpTruck { get; set; } = true;

    [StringLength(200, ErrorMessage = "200 characters at most.")]
    public string? Notes { get; set; }
}

/// <summary>An outside contact, for the column and group demos.</summary>
public sealed class ContactForm
{
    [Required(ErrorMessage = "Required.")]
    public string? FirstName { get; set; }

    public string? MiddleName { get; set; }

    [Required(ErrorMessage = "Required.")]
    public string? LastName { get; set; }

    public string? Title { get; set; }

    [Phone(ErrorMessage = "Not a phone number.")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Not an email address.")]
    public string? Email { get; set; }

    public string? Company { get; set; }

    [Display(Name = "PO number")]
    public string? PONumber { get; set; }

    public string? SiteContactName { get; set; }

    public string? SiteContactPhone { get; set; }

    public string? OtherContactName { get; set; }

    public string? OtherContactPhone { get; set; }

    public int? CustomerId { get; set; }

    public bool SendUpdates { get; set; }
}

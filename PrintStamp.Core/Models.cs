namespace PrintStamp.Core;

public class Entity   { public int Id { get; init; } public string Name { get; init; } = ""; public bool IsActive { get; init; } public override string ToString() => Name;}

public class PrintJob
{
    public long Id { get; init; }
    public string Code { get; init; } = "";
    public int IssuerId { get; init; }
    public int ReceiverId { get; init; }
    public DateTime CreatedAt { get; init; }
    public int PageCount { get; init; }
}

// what the admin sees after a scan
public class JobDetails
{
    public string Code { get; init; } = "";
    public string IssuerName { get; init; } = "";
    public string ReceiverName { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public int PageCount { get; init; }
}

// fractions of the page, 0..1 from the top-left
public record BarcodePos(double X, double Y);
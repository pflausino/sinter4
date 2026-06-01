namespace Domain.Entities;

using Domain.Enums;

public class FileRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public FileType FileType { get; set; }
    public int? FlopDiskNumber { get; set; }
    public DateTime? Date { get; set; }
    public string Client { get; set; } = string.Empty;
    public string? FileNumber { get; set; }
}

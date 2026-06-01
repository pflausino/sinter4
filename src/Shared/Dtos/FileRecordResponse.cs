namespace Shared.Dtos;

using Domain.Enums;

public record FileRecordResponse(
    Guid Id,
    string Name,
    FileType FileType,
    int? FlopDiskNumber,
    DateTime? Date,
    string Client,
    string? FileNumber
);

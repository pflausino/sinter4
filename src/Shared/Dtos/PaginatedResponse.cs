namespace Shared.Dtos;

public record PaginatedResponse<T>(
    List<T> Items,
    int TotalCount,
    bool HasMore
);

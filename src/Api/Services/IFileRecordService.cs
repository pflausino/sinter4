namespace Api.Services;

using Shared.Dtos;

public interface IFileRecordService
{
    Task<List<FileRecordResponse>> GetAllAsync();
    Task<PaginatedResponse<FileRecordResponse>> GetPagedAsync(int offset, int limit);
    Task<FileRecordResponse?> GetByIdAsync(Guid id);
    Task<FileRecordResponse> CreateAsync(CreateFileRecordRequest request);
    Task<FileRecordResponse?> UpdateAsync(Guid id, UpdateFileRecordRequest request);
    Task<bool> DeleteAsync(Guid id);
    Task<List<FileRecordResponse>> SearchAsync(string searchTerm);
    Task<PaginatedResponse<FileRecordResponse>> SearchPagedAsync(string searchTerm, int offset, int limit);
}

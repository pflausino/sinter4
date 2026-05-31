namespace Api.Services;

using Shared.Dtos;

public interface IFileRecordService
{
    Task<List<FileRecordResponse>> GetAllAsync();
    Task<FileRecordResponse?> GetByIdAsync(Guid id);
    Task<FileRecordResponse> CreateAsync(CreateFileRecordRequest request);
    Task<FileRecordResponse?> UpdateAsync(Guid id, UpdateFileRecordRequest request);
    Task<bool> DeleteAsync(Guid id);
}

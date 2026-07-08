namespace MailArchive.Application.Contracts.Admin;

public record AdminDashboardStorageUsageResponse(
    int TotalAttachmentRecords,
    long TotalAttachmentBytesFromDatabase,
    int AttachmentFilesFound,
    int MissingAttachmentFiles,
    long AttachmentStorageBytesOnDisk,
    int ImportBatchesWithPstFile,
    int PstFilesFound,
    int MissingPstFiles,
    long PstStorageBytesOnDisk,
    long TotalStorageBytesOnDisk,
    string StorageHealthStatus,
    DateTime GeneratedAtUtc
);
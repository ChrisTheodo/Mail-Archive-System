namespace MailArchive.Domain.Enums;

public enum ImportBatchStatus
{
    Pending = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
    CompletedWithErrors = 5
}
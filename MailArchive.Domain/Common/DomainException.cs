using MailArchive.Domain.Enums;

namespace MailArchive.Domain.Common;

public class DomainException : Exception
{
    public DomainError Error { get; }

    public DomainException(DomainError error)
        : base(error.ToString())
    {
        Error = error;
    }
}
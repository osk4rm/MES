namespace AsistOff.MES.Shared.Abstractions.Exceptions;

public class RepositoryException : DomainException
{
    public RepositoryException(string message) : base(message) { }
    public RepositoryException(string message, Exception innerException) : base(message, innerException) { }
}

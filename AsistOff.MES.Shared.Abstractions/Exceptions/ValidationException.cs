namespace AsistOff.MES.Shared.Abstractions.Exceptions;

public class ValidationException : DomainException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(string message) : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IDictionary<string, string[]> errors) : base("One or more validation failures have occurred.")
    {
        Errors = errors;
    }

    public ValidationException(string property, string error) : base($"Validation failed for {property}: {error}")
    {
        Errors = new Dictionary<string, string[]>
        {
            { property, new[] { error } }
        };
    }
}

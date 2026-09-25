namespace AsistOff.MES.Shared.Abstractions.DAL;

public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
    Guid? CreatedBy { get; set; }
    Guid? ModifiedBy { get; set; }
}
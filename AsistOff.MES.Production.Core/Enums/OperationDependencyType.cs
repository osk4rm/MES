namespace AsistOff.MES.Production.Domain.Enums;

/// <summary>
/// Dependency type between two operations in the recipe graph (PDM/APS style).
/// </summary>
public enum OperationDependencyType : short
{
    FinishToStart = 1,
    StartToStart = 2,
    FinishToFinish = 3,
    StartToFinish = 4
}

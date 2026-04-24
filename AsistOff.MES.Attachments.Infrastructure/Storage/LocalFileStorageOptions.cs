namespace AsistOff.MES.Attachments.Infrastructure.Storage;

public class LocalFileStorageOptions
{
    public const string SectionName = "Attachments:LocalStorage";

    /// <summary>Absolute or content-root-relative root directory for blobs.</summary>
    public string RootPath { get; set; } = "App_Data/attachments";
}

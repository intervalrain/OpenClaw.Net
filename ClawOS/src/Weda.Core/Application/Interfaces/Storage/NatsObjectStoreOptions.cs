namespace Weda.Core.Application.Interfaces.Storage;

public class NatsObjectStoreOptions
{
    public string BucketName { get; set; } = "blobs";
    public string? ConnectionName { get; set; }
}
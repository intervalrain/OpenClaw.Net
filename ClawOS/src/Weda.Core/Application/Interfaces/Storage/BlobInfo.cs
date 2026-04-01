namespace Weda.Core.Application.Interfaces.Storage;

public record BlobInfo(string Key, ulong Size, DateTime ModifiedAt);
using Azure.Storage.Blobs;
using ChatUiT2.Interfaces;

namespace ChatUiT2.Services;

public class StorageService
{
    private readonly BlobServiceClient _blobServiceClient;

    public StorageService(IConfiguration configuration)
    {
        var connectionString = configuration["ConnectionStrings:Storage"];
        if (connectionString == null)
        {
            throw new Exception("Storage connection string not found");
        }
        _blobServiceClient = new BlobServiceClient(connectionString);
    }

    public async Task<byte[]?> GetFileBytes(string username, string filename)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(username);
            var blobClient = containerClient.GetBlobClient(filename);
            var response = await blobClient.DownloadAsync();
            var bytes = new byte[response.Value.ContentLength];
            await response.Value.Content.ReadAsync(bytes);
            return bytes;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception during GetFileBytes: " + ex.Message);
            return null;
        }
    }

    public async Task UploadFile(string username, string filename, byte[] bytes)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(username);
        await containerClient.CreateIfNotExistsAsync();
        var blobClient = containerClient.GetBlobClient(filename);
        await blobClient.UploadAsync(new MemoryStream(bytes));
    }


}

using Azure.Storage.Blobs;
using ChatUiT2.Interfaces;
using ChatUiT2.Models;

namespace ChatUiT2.Services;

public class StorageService : IStorageService
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

        //Console.WriteLine("StorageService created");
    }

    private async Task<byte[]?> GetFileBytes(string container, string filename)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(container);
            var blobClient = containerClient.GetBlobClient(filename);
            var response = await blobClient.DownloadAsync();
            using (var memoryStream = new MemoryStream())
            {
                await response.Value.Content.CopyToAsync(memoryStream);
                return memoryStream.ToArray();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception during GetFileBytes: " + ex.Message);
            return null;
        }
    }

    public async Task<ChatFile> GetFile(string workItemId, string filename)
    {
        ChatFile file = new()
        {
            FileName = filename,
            Bytes = await GetFileBytes(workItemId, filename)
        };

        if (file.Bytes == null)
        {
            throw new Exception("File not found");
        }

        return file;
    }

    private async Task UploadFile(string container, string filename, byte[] bytes)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(container);
        await containerClient.CreateIfNotExistsAsync();
        var blobClient = containerClient.GetBlobClient(filename);
        if (await blobClient.ExistsAsync())
        {
            return;
        }

        await blobClient.UploadAsync(new MemoryStream(bytes));
    }

    public async Task UploadFile(IWorkItem workItem, ChatFile file)
    {
        if (file.Bytes == null)
        {
            throw new Exception("File bytes are null");
        }

        await UploadFile(workItem.Id, file.FileName, file.Bytes);
    }

    public async Task DeleteFile(IWorkItem workItem, string filename)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(workItem.Id);
        var blobClient = containerClient.GetBlobClient(filename);
        await blobClient.DeleteIfExistsAsync();

        // If no files are left, delete the container
        if (!containerClient.GetBlobs().Any())
        {
            await containerClient.DeleteIfExistsAsync();
        }
    }

    public async Task<IEnumerable<string>> ListChatFiles(IWorkItem workItem)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(workItem.Id);
        var files = new List<string>();
        await foreach (var blobItem in containerClient.GetBlobsAsync())
        {
            files.Add(blobItem.Name);
        }
        return files;
    }

    public async Task DeleteContainer(IWorkItem workItem)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(workItem.Id);
        await containerClient.DeleteIfExistsAsync();
    }

    

    

    


}

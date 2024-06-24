using Amazon.SecurityToken.Model;
using Azure.Storage.Blobs;
using ChatUiT2.Interfaces;
using ChatUiT2.Models;

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

    private async Task<byte[]?> GetFileBytes(string username, string filename)
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

    private async Task UploadFile(string username, string filename, byte[] bytes)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(username);
        await containerClient.CreateIfNotExistsAsync();
        var blobClient = containerClient.GetBlobClient(filename);
        await blobClient.UploadAsync(new MemoryStream(bytes));
    }

    public async Task DeleteFile(string username, string filename)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(username);
        var blobClient = containerClient.GetBlobClient(filename);
        await blobClient.DeleteIfExistsAsync();
    }

    public async Task<IEnumerable<string>> ListFiles(string username)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(username);
        var files = new List<string>();
        await foreach (var blobItem in containerClient.GetBlobsAsync())
        {
            files.Add(blobItem.Name);
        }
        return files;
    }

    public async Task DeleteContainer(string username)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(username);
        await containerClient.DeleteIfExistsAsync();
    }

    

    public async Task<ChatFile> GetFile(string username, string filename)
    {
        ChatFile file = new()
        {
            FileName = filename,
            FileType = ChatFile.GetFileTypeFromName(filename),
            Bytes = await GetFileBytes(username, filename)
        };

        if (file.Bytes == null)
        {
            throw new Exception("File not found");
        }

        return file;
    }

    public async Task UploadFile(string username, ChatFile file)
    {
        if (file.Bytes == null)
        {
            throw new Exception("File bytes are null");
        }
        await UploadFile(username, file.FileName, file.Bytes);
    }


}

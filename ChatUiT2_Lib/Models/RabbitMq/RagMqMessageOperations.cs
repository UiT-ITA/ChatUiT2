namespace ChatUiT2.Models.RabbitMq;
public enum RagMqMessageOperations
{
    // When you want to generate embeddings for a specific item    
    GenerateEmbeddings = 0,
    // Used to trigger scan for items missing embeddings and add them
    // to generate embeddings queue.
    ScanForItemsMissingEmbeddings = 1,
    // Used to cancel all embeddings processing in queue.
    CancelAllEmbeddingsProcessing = 2,
    // Used to regenerate all item embeddings in a project.
    GenerateEmbeddingsForUpdatedItems = 3,
}

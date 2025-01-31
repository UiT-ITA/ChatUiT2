namespace ChatUiT2_Classlib.Model.RabbitMq;
public enum RagMqMessageOperations
{
    // When you want to generate embeddings for a specific item
    // by sending text to LLM and ask for possible questions and
    // then generating embeddings for those questions.
    GenerateQuestionEmbeddings = 0,
    // Used to trigger scan for items missing embeddings and add them
    // to generate embeddings queue.
    ScanForItemsMissingEmbeddings = 1,
    // Used to cancel all embeddings processing in queue.
    CancelAllEmbeddingsProcessing = 2,
}

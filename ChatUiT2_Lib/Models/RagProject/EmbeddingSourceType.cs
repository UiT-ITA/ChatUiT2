namespace ChatUiT2.Models.RagProject;

public enum EmbeddingSourceType
{
    // When embeding is generated from a question that is
    // answered by source text.
    Question = 0,
    // When embedding is generated from a paragraph of text.
    // Usually by splitting the text into paragraphs and embedding
    Paragraph = 1
}

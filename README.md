## Project description

Codebase for chat.uit.no.

## Project structure

The main solution is ChatUiT2.sln. It contains the following projects:

ChatUiT2 is a blazor server project. It is the webapp that is https://chat.uit.no.

ChatUiT2_Lib is a class library that contains shared code for the webapp and the azure function project.

The azure function project is called ChatUiT2_Maintenance. It contains the azure functions that are used by the webapp.
See the repo for details about that project:

[Github repo ChatUiT2_Maintenance_](https://github.com/UiT-ITA/ChatUiT2_Maintenance)

Maintenance is a set of function for database cleanup and generating rag embeddings.

## Rag

Uses a azure cosmos db nosql serverless database to store embeddings for various UiT
documents. The database is setup with vector index for the embeddings. 

There is one top level database that contains information about the rag projects that exist.
It is the database RagProjects and it has a container named RagProjectDefinitions. See example
rag project definition below:

```json
{
    "id": "0d84b1c0-14c8-4872-bb4e-a847850f707d",
    "Name": "TopdeskKnowledgeItems",
    "Description": "Index all open knowledge items in topdesk using embeddings. This enables search in helpdesk articles",
    "RequestorDepartments": [
        {
            "Name": "IT-avdelingen",
            "Stedkode": "262600"
        }
    ],
    "Participants": [
        {
            "Displayname": "Espen Rivedal",
            "UitUsername": "eje003@uit.no",
            "Role": "Developer"
        }
    ],
    "Configuration": {
        "MinNumberOfQuestionsPerItem": 5,
        "MaxNumberOfQuestionsPerItem": 20,
        "ModelForQuestionGeneration": "GPT-4o-Mini",
        "ModelForEmbeddings": "text-embedding-ada-002",
        "DbName": "RagTopdesk",
        "ItemCollectionName": "KnowledgeItems",
        "EmbeddingCollectioName": "KnowledgeItemEmbeddings",
        "EmbeddingTypes": [
            0
        ],
        "EmbeddingEventCollectioName": "KnowledgeItemEmbeddingEvents"
    },
    "Created": "2025-03-12T12:58:21.0228217+00:00",
    "Updated": "2025-03-13T09:39:19.5833428+00:00"
 }
```

the document contains info about the project and the following properties point to the
specific database and collections that are used for the project:

- DbName
- ItemCollectionName
- EmbeddingCollectioName
- EmbeddingEventCollectioName

To look at data in the database goto the database in azure portal and use data explorer.

Each RagProject has its own database with three collections:

- Collection for content items. This stores articles/documents etc with content
- Collection for embeddings. This stores the embeddings for the content items. Embeddings are used to search for articles
- Collection for embedding events. A task list that is used to coordinate embedding the content items.

Webapp sends a message to RabbitMq when it wants embeddings to be generated for a 
rag project.

A worker azure function is listening to the queue and generates embedding events for
each article that is part of the rag project. It sends a RabbitMq message for each article
that is to be embedded.

A second worker azure function is listening to the queue and generates embeddings for
each article. 

It is possible to see status of the embeddings in the webapp.

Example of a content item

```json
{
    "id": "xxxxx",
    "SystemName": "Topdesk",
    "DataType": "KnowledgeItem",
    "ContentType": "INLINE",
    "Title": "TOPdesk: Mal for kunnskapsartikler",
    "Description": "Har dere en mal for kunnskapsartikler i TOPdesk?",
    "ContentText": "<h5 class=\"rt-level-1\">Mal for kunnskapsartikler</h5>I artikkel KI 0047 vises hvordan du oppretter kunnskapsartikler<br/>",
    "ContentUrl": "https://uit.topdesk.net//solutions/open-knowledge-items/item/KI%200002/no/",
    "ViewUrl": "https://uit.topdesk.net//solutions/open-knowledge-items/item/KI%200002/no/",
    "Language": "no",
    "SourceSystemId": "xxxxx",
    "SourceSystemAltId": "KI 0002",
    "Created": "2025-03-12T12:58:21.3594245+00:00",
    "Updated": "2025-03-13T09:39:19.6379667+00:00",
    "RagProjectId": "0d84b1c0-14c8-4872-bb4e-a847850f707d",
}
```

C# class for content item is ContentItem

Example embedding 

```json
{
    "id": "xxxxx",
    "Embedding": [
        0.029876913875341415,
        -0.049496788531541824,
        -0.026655152440071106,
        -0.006962951738387346,
        0.017463266849517822,
        ...
    ],
    "Originaltext": "Hvordan oppretter jeg kunnskapsartikler?",
    "SourceItemId": "xxxxx",
    "Model": "text-3-large",
    "ModelProvider": "Azure OpenAI",
    "Created": "2025-03-12T13:03:45.1628727+00:00",
    "Updated": "2025-03-13T14:10:28.2415716+00:00",
    "RagProjectId": "xxxxx",
    "TextType": 0,
    "ContentItem": null
}
```

C# class for embedding is RagTextEmbedding
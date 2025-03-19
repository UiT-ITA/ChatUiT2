## Project description

Codebase for chat.uit.no.

## Rag

Uses a azure cosmos db nosql serverless database to store embeddings for various UiT
documents.

Uses a custom json document format to specify a rag project with content items.

Webapp sends a message to RabbitMq when it wants embeddings to be generated for a 
rag project.

A worker azure function is listening to the queue and generates embedding events for
each article that is part of the rag project. It sends a RabbitMq message for each article
that is to be embedded.

A second worker azure function is listening to the queue and generates embeddings for
each article. 

It is possible to see status of the embeddings in the webapp.
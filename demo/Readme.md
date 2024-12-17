# Quick-start deployment of Azure infrastrucure for ChatUiT

This quick-start guide describes the process of setting up an environment to run ChatUiT in your own Azure tennant.

This template creates an Azure Cosmos DB account and Azure Web App, then automatically deploys the ChatUiT web app hosted on GitHub and injects the Cosmos DB endpoint and auth key into the Web App's Application Settings allowing it to connect automatically upon first run.

This sample is useful where you want to deploy these resources and have the web app automatically connect to Cosmos DB in a single operation without having to manually add connection information to Application Settings in the portal.

## Prerequisites

1. An Azure account with an active subscription. [Create an account for free](https://azure.microsoft.com/free/?ref=microsoft.com&utm_source=microsoft.com&utm_medium=docs&utm_campaign=visualstudio)
2. Open AI endpoint with enabled models. [Create and deploy an Azure OpenAI Service resource](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/create-resource?pivots=web-portal)
3. Entra ID app registration to use for login. [Register an application with the Microsoft identity platform](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app?tabs=certificate)

## Deploy sample application

Below are the parameters which can be user configured in the parameters file including:

| Parameter         | Description                                |
|-------------------|--------------------------------------------|
| `azureAdClientId` | The Object ID of the App registration      |
| `endpoints`       | The URL for the OpenAI endpoint            |
| `models`          | List of the models available               |
| `deployment1`     | Deployment configuration (details needed)  |

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FUiT-ITA%2FChatUiT2%2Fmaster%2Fdemo%2Fazuredeploy.json)

[![Visualize](https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/1-CONTRIBUTION-GUIDE/images/visualizebutton.svg?sanitize=true)](http://armviz.io/#/?load=https%3A%2F%2Fraw.githubusercontent.com%2FUiT-ITA%2FChatUiT2%2Fmaster%2Fdemo%2Fazuredeploy.json)
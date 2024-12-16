# Quick-start deployment of Azure infrastrucure for ChatUiT

This quick-start guide describes the process of setting up an environment to run ChatUiT in your own Azure tennant.

## Prerequisites

1. An Azure account with an active subscription. [Create an account for free](https://azure.microsoft.com/free/?ref=microsoft.com&utm_source=microsoft.com&utm_medium=docs&utm_campaign=visualstudio)
2. Open AI endpoint with enabled models. [Create and deploy an Azure OpenAI Service resource](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/create-resource?pivots=web-portal)
3. Entra ID app registration to use for login. [Register an application with the Microsoft identity platform](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app?tabs=certificate)

## Deploy sample application

Set parameters for Entra ID Client ID, OpenAI endpoint, models and deployment:

`azureAdClientId` - The Object ID of the App registration
`endpoints` - The URL for the OpenAI endpoint
`models` - List of the models available
`deployment1` - xxx?

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FUiT-ITA%2FChatUiT2%2Frefs%2Fheads%2Fdemo%2Fazuredeploy.json)

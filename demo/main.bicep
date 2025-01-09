// Parameters
@description('The Entra ID client ID for the application.')
param azureAdClientId string = ''

@description('The endpoints for the deployment.')
param endpoints string = ''

@description('The models available for the deployment.')
param models string = '[{"Name":"GPT-4o","Description":"Best model","DeploymentName":"gpt-4o","MaxContext":128000,"MaxTokens":4096,"DeploymentType":"AzureOpenAI","Deployment":"Deployment1"},{"Name":"GPT-4o-Mini","Description":"Good and fast","DeploymentName":"gpt-4o-mini","MaxContext":128000,"MaxTokens":16000,"DeploymentType":"AzureOpenAI","Deployment":"Deployment1"}]'

@description('The key for the deployment to use.')
param deployment1 string = ''

@description('The URL for the GitHub repository that contains the project to deploy.')
param chatUitGitUrl string = 'https://github.com/UiT-ITA/ChatUiT2'

@description('The branch of the GitHub repository to use.')
param chatUitGitBranch string = 'master'

// Variables
var location = resourceGroup().location
var resourceGroupName = toLower(resourceGroup().name)
var uniquePrefix = toLower(substring(uniqueString(resourceGroup().id), 0, 6))

// Create an App Service Plan
module asp 'br/public:avm/res/web/serverfarm:0.3.0' = {
  name: 'appServicePlanModule'
  params: {
    name: '${resourceGroupName}${uniquePrefix}-asp'
    reserved: true
    skuName: 'S1'
    location: location
  }
}

// Create a Web App
module webapp 'br/public:avm/res/web/site:0.11.1' = {
  name: 'webAppModule'
  params: {
    name: '${resourceGroupName}${uniquePrefix}-webapp'
    location: location
    kind: 'app,linux'
    serverFarmResourceId: asp.outputs.resourceId
    managedIdentities: {
      systemAssigned: true
    }
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
    }
  }
}

var webAppPrincipalId = webapp.outputs.systemAssignedMIPrincipalId

// Create a Key Vault
module keyVault 'br/public:avm/res/key-vault/vault:0.10.2' = {
  name: 'keyVaultModule'
  params: {
    name: '${resourceGroupName}${uniquePrefix}-kv'
    location: location
    sku: 'standard'
    enableRbacAuthorization: true
    roleAssignments: [
      {
        roleDefinitionIdOrName: 'Key Vault Secrets Officer'
        principalId: webAppPrincipalId
        principalType: 'ServicePrincipal'
      }
    ]
    secrets: [
      {
        name: 'deployment1'
        value: deployment1
      }
    ]
  }
}

// Create a Cosmos DB account for MongoDB
module cosmos 'br/public:avm/res/document-db/database-account:0.8.1' = {
  name: 'cosmosModule'
  params: {
    name: '${resourceGroupName}${uniquePrefix}-db'
    secretsExportConfiguration: {
      keyVaultResourceId: keyVault.outputs.resourceId
      primaryWriteConnectionStringSecretName: 'primaryWriteConnectionString'
    }
    mongodbDatabases:[
      {
        name: 'Users'
        collections: [
          {
            name: 'ChatMessages'
            shardKey: {
              Username: 'Hash'
            }
            indexes: [
              {
                key: {
                  keys: [
                    '_id'
                  ]
                }
              }
              {
                key: {
                  keys: [
                    '$**'
                  ]
                }
              }
            ]
          }
          {
            name: 'Chats'
            shardKey: {
              Username: 'Hash'
            }
            indexes: [
              {
                key: {
                  keys: [
                    '_id'
                  ]
                }
              }
              {
                key: {
                  keys: [
                    '$**'
                  ]
                }
              }
            ]
          }
          {
            name: 'Files'
            shardKey: {
              Username: 'Hash'
            }
            indexes: [
              {
                key: {
                  keys: [
                    '_id'
                  ]
                }
              }
              {
                key: {
                  keys: [
                    '$**'
                  ]
                }
              }
            ]
          }
          {
            name: 'Instructions'
            shardKey: {
            }
            indexes: [
              {
                key: {
                  keys: [
                    '_id'
                  ]
                }
              }
              {
                key: {
                  keys: [
                    '$**'
                  ]
                }
              }
            ]
          }
          {
            name: 'Users'
            shardKey: {
              Username: 'Hash'
            }
            indexes: [
              {
                key: {
                  keys: [
                    '_id'
                  ]
                }
              }
              {
                key: {
                  keys: [
                    '$**'
                  ]
                }
              }
            ]
          }
        ]
      }
    ]
    location: location
    networkRestrictions:{
      ipRules: []
      virtualNetworkRules: []
      publicNetworkAccess: 'Enabled'
    }
    capabilitiesToAdd: [
      'EnableMongo'
      'EnableServerless'
    ]
    backupPolicyContinuousTier: 'Continuous7Days'
  }
}

// Import the existing Web App to reference it in the app settings
resource webappExisting 'Microsoft.Web/sites@2024-04-01' existing = {
  name: '${resourceGroupName}${uniquePrefix}-webapp'
  dependsOn: [
    webapp
  ]
} 

// Assign the connection string to the Web App
resource connectionStrings 'Microsoft.Web/sites/config@2024-04-01' = {
  name: 'connectionstrings'
  parent: webappExisting
  properties: {
    MongoDB: {
      type: 'Custom'
      value: '@Microsoft.KeyVault(SecretUri=${cosmos.outputs.exportedSecrets.primaryWriteConnectionString.secretUri})'
    }
    KeyVault: {
      type: 'Custom'
      value: keyVault.outputs.uri
    }
  }
}

resource appSettings 'Microsoft.Web/sites/config@2024-04-01' = {
  name: 'appsettings'
  parent: webappExisting
  properties: {
    AzureAd__ClientId: azureAdClientId
    Endpoints: endpoints
    Models: models
    Deployment1: '@Microsoft.KeyVault(SecretUri=${keyVault.outputs.uri}secrets/deployment1)'
  }
}

// Add a source control configuration to the Web App
resource webappSrc 'Microsoft.Web/sites/sourcecontrols@2024-04-01' = {
  parent: webappExisting
  name: 'web'
  properties: {
    repoUrl: chatUitGitUrl
    branch: chatUitGitBranch
    isManualIntegration: true
  }
}

# Fhir Service storage on Azure Kubernetes

**STATUS: Work In Progress.**

This document describes at a high-level the changes that will be needed to support storing, exporting and importing FHIR data using Managed Identities on AKS.
In terms of storage mechanisms, FHIR Service can:
1) **Persist FHIR data**. FHIR service is designed to support different data stores. For Gen1 offering Cosmos DB is used as the underlying persistent store, while for Gen2 is Azure SQL database. More details can be found [here](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=/doc/SQL%5Csql.md&_a=preview).
2) **Export/Import FHIR data** to/from a specified storage account.

[[_TOC_]]

## Business Justification

FHIR Services on AKS are going to use Managed Identities to connect to their underlying data stores. Managed identities allow us to build more secure services and simplify credential management for our customers.

To understand more about managed identity, [What is managed identities for Azure resources?](https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview) is a great place to start. From the link:

> A common challenge when building cloud applications is how to manage the credentials in your code for authenticating to cloud services. Keeping the credentials secure is an important task. Ideally, the credentials never appear on developer workstations and aren't checked into source control. Azure Key Vault provides a way to securely store credentials, secrets, and other keys, but your code has to authenticate to Key Vault to retrieve them.
>
>The managed identities for Azure resources feature in Azure Active Directory (Azure AD) solves this problem. The feature provides Azure services with an automatically managed identity in Azure AD. You can use the identity to authenticate to any service that supports Azure AD authentication, including Key Vault, without any credentials in your code.

## Design
Managed identities allow your application or service to automatically obtain an OAuth 2.0 token to authenticate to Azure resources, from an endpoint running locally on the virtual machine or service where your application is executed. There are two different types of managed identities in Azure: system-assigned identities, that you can enable directly on the Azure services that support it (a virtual machine or Azure App Service, for example) and user-assigned managed identities that are Azure resources created separately.
To understand more about, [How managed identities for Azure resources work with Azure virtual machines](https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/how-managed-identities-work-vm)

In Azure Kubernetes Service, Managed Identities concept has been brought with [**AAD Pod Identity**](https://docs.microsoft.com/en-us/azure/aks/use-azure-ad-pod-identity). Azure Active Directory pod-managed identities uses Kubernetes primitives to associate managed identities for Azure resources and identities in Azure Active Directory (AAD) with pods. Administrators create identities and bindings as Kubernetes primitives that allow pods to access Azure resources that rely on AAD as an identity provider. 

### Azure SQL Database
During the prototype phase we identified the following provisioning, configuration and code changes to be able to establish managed identities.
#### Azure Resource Provisioning
- User Managed Identity for the Fhir Service.
- AKS managed identity - should have `Managed Identity Operator` and `Virtual Machine Contributor` roles on the resource group where the Fhir Service managed identity is created.
- SQL - for the prototype, we used one of the dev SQL elastic pools. Other options are standalone Azure SQL Server, standalone Azure SQL Database, Elastic Pool, Hyperscale, SQL Server as a Kubernetes resource.
- Azure AD user account - to manage database access (such as grant managed identity access) from a service principal, we will need to set the AAD administrator account. We can only have one AAD administrator account per SQL server, we could create an AAD group and assign that group as the administrator.
#### Azure SQL Database configuration
- Enable Azure AD authentication for Azure SQL Server
- Set Azure AD user/group as SQL Server Active Directory admin
- Create contained database user to represent the Fhir Service managed identity.
```sql
CREATE USER [fhir-service-identity] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [fhir-service-identity];
ALTER ROLE db_datawriter ADD MEMBER [fhir-service-identity];
ALTER ROLE db_owner ADD MEMBER [fhir-service-identity];
```
- Grant permissions to the contained DB user to connect to the database.
```sql
GRANT CONNECT TO [fhir-service-identity];
```
#### Kubernetes Resource Provisioning
- `AzureIdentity` object, representing Fhir Service managed identity with clientId and resourceId.
- `AzureIdentityBinding` object, associating Fhir Service pod with the AzureIdentity.
- Label to Fhir Service pod which is connecting to Azure Sql Database - `aadpodidbinding`
- Fhir Server Container with environment variables `AuthenticationType` set to ManagedIdentity and `ManagedIdentityClientId` set to the `Client ID` of Fhir Service managed identity.

#### OSS
##### Setting a token
Currently [`fhir-server/src/Microsoft.Health.Fhir.SqlServer/Features/Storage/SqlServerFhirModel.cs`](https://github.com/microsoft/fhir-server/blob/main/src/Microsoft.Health.Fhir.SqlServer/Features/Storage/SqlServerFhirModel.cs) is initialized with [`healthcare-shared-components/src/Microsoft.Health.SqlServer/ISqlConnectionStringProvider.cs`](https://github.com/microsoft/healthcare-shared-components/blob/c07c85fcbd2dcfd4fecdbfc2dc176ee0379b86b1/src/Microsoft.Health.SqlServer/ISqlConnectionStringProvider.cs). While that is fine if `AuthenticationType` is `ConnectionString`, it will fail with the following error if it is `ManagedIdentity`:
```
      Microsoft.Data.SqlClient.SqlException (0x80131904): Login failed for user ''.
         at Microsoft.Data.ProviderBase.DbConnectionPool.CheckPoolBlockingPeriod(Exception e)
         at Microsoft.Data.ProviderBase.DbConnectionPool.CreateObject(DbConnection owningObject, DbConnectionOptions userOptions, DbConnectionInternal oldConnection)
         at Microsoft.Data.ProviderBase.DbConnectionPool.UserCreateRequest(DbConnection owningObject, DbConnectionOptions userOptions, DbConnectionInternal oldConnection)
         at Microsoft.Data.ProviderBase.DbConnectionPool.TryGetConnection(DbConnection owningObject, UInt32 waitForMultipleObjectsTimeout, Boolean allowCreate, Boolean onlyOneCheckConnection, DbConnectionOptions userOptions, DbConnectionInternal& connection)
         at Microsoft.Data.ProviderBase.DbConnectionPool.TryGetConnection(DbConnection owningObject, TaskCompletionSource`1 retry, DbConnectionOptions userOptions, DbConnectionInternal& connection)
         at Microsoft.Data.ProviderBase.DbConnectionFactory.TryGetConnection(DbConnection owningConnection, TaskCompletionSource`1 retry, DbConnectionOptions userOptions, DbConnectionInternal oldConnection, DbConnectionInternal& connection)
         at Microsoft.Data.ProviderBase.DbConnectionInternal.TryOpenConnectionInternal(DbConnection outerConnection, DbConnectionFactory connectionFactory, TaskCompletionSource`1 retry, DbConnectionOptions userOptions)
         at Microsoft.Data.ProviderBase.DbConnectionClosed.TryOpenConnection(DbConnection outerConnection, DbConnectionFactory connectionFactory, TaskCompletionSource`1 retry, DbConnectionOptions userOptions)
         at Microsoft.Data.SqlClient.SqlConnection.TryOpen(TaskCompletionSource`1 retry, SqlConnectionOverrides overrides)
         at Microsoft.Data.SqlClient.SqlConnection.Open(SqlConnectionOverrides overrides)
         at Microsoft.Health.Fhir.SqlServer.Features.Storage.SqlServerFhirModel.InitializeBase(CancellationToken cancellationToken)
         at Microsoft.Health.Fhir.SqlServer.Features.Storage.SqlServerFhirModel.Initialize(Int32 version, Boolean runAllInitialization, CancellationToken cancellationToken)
         at Microsoft.Health.Fhir.SqlServer.Features.Storage.SqlServerFhirModel.EnsureInitialized()
```
This behaviour is expected. We need to use an access token when opening a connection to SQL Database. The access token is obtained using the user-assigned managed identity. In ['healthcare-shared-components/src/Microsoft.Health.SqlServer/ManagedIdentitySqlConnectionFactory.cs '](https://github.com/microsoft/healthcare-shared-components/blob/c07c85fcbd2dcfd4fecdbfc2dc176ee0379b86b1/src/Microsoft.Health.SqlServer/ManagedIdentitySqlConnectionFactory.cs), the SQL connection is updated with an access token. If we initialize `SqlServerFhirModel` with `ISqlConnectionFactory`, we will be able to authenticate to the SQL Database using a token and safely open a connection.

##### Caching a token

By default, the access token is valid for 1 hour. The `Microsoft.Azure.Services.AppAuthentication` library caches the token internally and automatically triggers refresh in the background when less than 5 minutes remaining until expiration.

We might need to implement a similar caching mechanism for optimization if it hasn't been implemented.

##### Handling Unauthorized error

When opening a SQL connection fails with Unauthorized, we could try to refresh the access token on-demand and retry the connection.

We could implement on-demand refresh of the token if it is not there. The downside is that the request latency might be longer. We can measure how frequent is the credential rotation.

A high-level architecture of a Fhir Service on AKS, connecting to its SQL Database using user assigned managed identity:

![Fhir Server access to SQL Database using MI through aadpodidentity in AKS](imgs/fhir_sql_server_with_mi.png)

**nmi** (Node Managed Identity) - a daemonset, that hijacks all calls to Azure’s Instance Metadata API from each node, processing them by calling MIC instead.

**mic** (Managed Identity Controller) - a pod that invokes Azure’s Instance Metadata API, caching locally tokens and the mapping between identities and pods.
#### Next Steps
Moving out of the prototype phase.

To ease and automate provisioning and configuration set-up for new Fhir instances, we can follow Dicom approach - a combination of ARM templates, bash and PS scripts, .NET Console application, Fhir Service custom resource and controller in Kubernetes.

Identify RP changes
### Azure Cosmos DB
We need to identify provisioning, configuration and code changes to enable Fhir Server on AKS to connect to Cosmos DB using Managed Identities.

### Azure storage
#### Export
##### Use case:
The customer has a FHIR service provisioned in AKS and a storage account. The customer should be able to export their FHIR data to the storage account without having to provide us their storage key.

In this case, the customer can establish an identity for the FHIR service and grant write permission to their storage account using that identity. To support export operation, the following resources must be provisioned:
##### Azure Resource Provisioning
- Managed Identity in a resource group where the AKS cluster has access
- Azure Storage account
- Assign role 'Storage Blob Data Contributor' to the identity
##### Kubernetes Resource Provisioning
- Fhir Server with export enabled
- AAD Pod Identity deployed on the AKS cluster
- `AzureIdentity` object, representing Fhir Service managed identity with clientId and resourceId.

We have to evaluate if any additional OSS changes will be needed to support export operation for Fhir Server on AKS.
#### Import
This feature is currently being implemented under [work item](https://microsofthealth.visualstudio.com/Health/_workitems/edit/81672).
We have to evaluate if any additional OSS changes will be needed to support import operation for Fhir Server on AKS.
## Test Strategy
We will continue to use all of the tests that we have in the FHIR service for verifying all existing scenarios are working as expected.

We will need to add a new set of E2E tests for provisioning a new Fhir service using managed identity to connect to its SQL Database or Cosmos DB.

## Security

*Describe any special security implications or security testing needed.*

## Other

*Describe any impact to privacy, localization, globalization, deployment, back-compat, SOPs, ISMS, etc.*

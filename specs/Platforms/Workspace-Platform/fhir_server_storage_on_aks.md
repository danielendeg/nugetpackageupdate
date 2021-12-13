# Fhir Service storage on Azure Kubernetes

**STATUS: Work In Progress.**

The purpose of this document is to detail the design and changes for setting up Fhir Service on AKS to use Managed Identities to access its data store.


[[_TOC_]]

## Business Justification
FHIR Services on AKS are going to use Managed Identities to connect to their underlying data stores. Managed identities allow us to build more secure services and simplify credential management for our customers.

To understand more about managed identity, [What is managed identities for Azure resources?](https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview) is a great place to start. From the link:

> A common challenge when building cloud applications is how to manage the credentials in your code for authenticating to cloud services. Keeping the credentials secure is an important task. Ideally, the credentials never appear on developer workstations and aren't checked into source control. Azure Key Vault provides a way to securely store credentials, secrets, and other keys, but your code has to authenticate to Key Vault to retrieve them.
>
>The managed identities for Azure resources feature in Azure Active Directory (Azure AD) solves this problem. The feature provides Azure services with an automatically managed identity in Azure AD. You can use the identity to authenticate to any service that supports Azure AD authentication, including Key Vault, without any credentials in your code.

## Design
FHIR services in a Service Fabric cluster connect to their database in two ways: using a master key and using a resource token. The master key applies to the entire account, whereas resource tokens are generated using a master key, have a maximum duration of five hours, and can be scoped to collections, partitions, or even individual documents. FHIR service obtains a refresh resource token from the Account Routing Service. Since workspace-platform uses [**AAD Pod Identity**](https://docs.microsoft.com/en-us/azure/aks/use-azure-ad-pod-identity), we can now switch FHIR services to use Managed Identities to connect to their underlying data stores. Thus the setup entrypoint and refresh tokens will no longer be needed for FHIR service access to its database.

Managed identities allow your application or service to automatically obtain an OAuth 2.0 token to authenticate to Azure resources, from an endpoint running locally on the virtual machine or service where your application is executed. There are two different types of managed identities in Azure: system-assigned identities, that you can enable directly on the Azure services that support it (a virtual machine or Azure App Service, for example) and user-assigned managed identities that are Azure resources created separately.
To understand more about, [How managed identities for Azure resources work with Azure virtual machines](https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/how-managed-identities-work-vm)

In Azure Kubernetes Service, Managed Identities concept has been brought with AAD Pod Identity. Azure Active Directory pod-managed identities uses Kubernetes primitives to associate managed identities for Azure resources and identities in Azure Active Directory (AAD) with pods. Administrators create identities and bindings as Kubernetes primitives that allow pods to access Azure resources that rely on AAD as an identity provider. 
### Prototype Phase
FHIR service is designed to support different data stores. For Gen1 offering Cosmos DB is used as the underlying persistent store, while for Gen2 is Azure SQL database. We based our prototype on Azure SQL Database. The goal was to identify what changes will be needed in Fhir Service to use Managed Identity and not Account Routing Service to access SQL Database. The following sections describe that.
#### FHIR Service PAAS customizations in workspace-platform
We created a [Fhir web project](https://microsofthealth.visualstudio.com/Health/_git/workspace-platform?version=GBpersonal/petyag/migration&path=/fhir/fhirservice). It is based on Fhir Service implementation in health-paas repo. We excluded settings and services like:
  - Ifx and Shoebox Auditing
  - Authentication and Authorization
  - RBAC
  - Telemetry
  - Export
  - Reindex

We disabled all references to Account Rounting and Front End Services. 
#### Azure Resource Provisioning
We implemented temporary bash and PS scripts to provision the following Azure resources:
- User Managed Identity for the Fhir Service.
- AKS managed identity - should have `Managed Identity Operator` and `Virtual Machine Contributor` roles on the resource group where the Fhir Service managed identity is created.
- SQL - for the prototype, we used one of the dev SQL elastic pools. Other options that we explored: standalone Azure SQL Server, standalone Azure SQL Database, SQL Server as a Kubernetes resource. We chose SQL Elastic Pool since both Dicom and Fhir use it in their current implementations.
- Azure AD user account - to manage database access (such as grant managed identity access) from a service principal, we will need to set the AAD administrator account. We can only have one AAD administrator account per SQL server. We adopted the same approach as described in this design [document](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=/doc/SQL%5Csql.md&_a=preview&anchor=provisioning-sql-server-and-elastic-pool), AAD security group.
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
We created temporary YAML specs to provision the following Kubernetes objects:
- `AzureIdentity` object, representing Fhir Service managed identity with clientId and resourceId.
- `AzureIdentityBinding` object, associating Fhir Service pod with the AzureIdentity.
- FHIR service from container image with the following extensions:
  - Label to Fhir Service pod which is connecting to Azure Sql Database - `aadpodidbinding`
  - Fhir Server Container with environment variables `AuthenticationType` set to ManagedIdentity and `ManagedIdentityClientId` set to the `Client ID` of Fhir Service managed identity.

#### OSS Changes
##### Setting a token
Currently [`fhir-server/src/Microsoft.Health.Fhir.SqlServer/Features/Storage/SqlServerFhirModel.cs`](https://github.com/microsoft/fhir-server/blob/main/src/Microsoft.Health.Fhir.SqlServer/Features/Storage/SqlServerFhirModel.cs) is initialized with [`healthcare-shared-components/src/Microsoft.Health.SqlServer/ISqlConnectionStringProvider.cs`](https://github.com/microsoft/healthcare-shared-components/blob/c07c85fcbd2dcfd4fecdbfc2dc176ee0379b86b1/src/Microsoft.Health.SqlServer/ISqlConnectionStringProvider.cs). While that is fine if `AuthenticationType` is `ConnectionString`, it will fail if `AuthenticationType` is `ManagedIdentity`.
We need to use an access token when opening a connection to SQL Database. The access token is obtained for the user-assigned managed identity. In ['healthcare-shared-components/src/Microsoft.Health.SqlServer/ManagedIdentitySqlConnectionFactory.cs '](https://github.com/microsoft/healthcare-shared-components/blob/c07c85fcbd2dcfd4fecdbfc2dc176ee0379b86b1/src/Microsoft.Health.SqlServer/ManagedIdentitySqlConnectionFactory.cs), the SQL connection is updated with an access token. 

We initialized`SqlServerFhirModel` with `ISqlConnectionFactory`, to be able to authenticate to the SQL Database using a token and safely open a connection.

A high-level architecture of a Fhir Service on AKS, connecting to its SQL Database using user assigned managed identity:

![Fhir Server access to SQL Database using MI through aadpodidentity in AKS](imgs/fhir_sql_server_with_mi.png)

1. Fhir pod uses Azure Active Directory Authentication Library(ADAL) to acquire a token for the user-assigned managed identity. The call is picked up by Node Managed Identity(NMI). NMI is a pod that runs as a DaemonSet on each node in the AKS cluster. NMI intercepts security token requests to the Azure Instance Metadata Service identity endpoint(IMDS) on each node.
2. NMI queries Kubernetes API Server to find which identity is assigned to the Fhir Pod.
3. NMI calls node's IMDS , `http://169.254.169.254/metadata/identity/oauth2/token`, to request a token on behalf of the Fhir Pod.
4. IMDS returns the bearer token to NMI.
5. NMI return the access token to Fhir Pod.
6. Fhir Pod sets the access token on the SQL Connection and connects to its Azure SQL Database.
### Next Phase
To ease and automate provisioning of all Azure and Kubernetes resources, we identified during the prototype phase, we will need to implement:
- [Fhir custom resource, Fhir and Fhir release controllers](./fhir_server_on_aks.md).
- [Console App](./fhir_server_on_aks.md), this console tool will help provisioning FHIR services in workspace-platform only.
- Enable all settings, services and middleware, we excluded during the prototype phase, so we can achieve feature parity with Fhir Server implementation in health-paas repo. 
### Future wave of implementation
- RP Worker, we need to integrate some of the work from the Console App in the RP Worker. We will need to be able to do this in a manner that does not impact prod provisioning until we reach a relatively stable state. Our options are to either:
  - Work in a feature branch and merge in to main when the code is stable.
  - Actively work in main branches, with configurations to disable workspace-platform FHIR provisioning until we are ready to enable it for dual stack.
- SQL Schema init/upgrade for FHIR will need to be changed, to match how it is done with DICOM.
### Out-of-Scope
Cosmos DB is out-of-scope for now.
## Test Strategy
We will continue to use all of the tests that we have in the FHIR service for verifying all existing scenarios are working as expected.

We will need to add a new set of E2E tests for provisioning a new Fhir service using managed identity to connect to its SQL Database.

## Security

*Describe any special security implications or security testing needed.*

## Other

*Describe any impact to privacy, localization, globalization, deployment, back-compat, SOPs, ISMS, etc.*

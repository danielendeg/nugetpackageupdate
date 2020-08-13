# Using Managed Identity credentials to export data to an Azure Storage account

This document describes at a high-level the code changes that will be needed to support bulk export in Azure API for FHIR using Managed Identity to get access to the Azure Storage account. Customers will need to create an Azure Storage account and update their FHIR instance with the corresponding resource uri. They will also have to give the Managed Identity permissions to the storage account.

## Components

### ManagedIdentityResourceTokenProvider (Account Routing Service)

1. Retrieve `Account` and `ManagedIdentityMetadata` documents from global cosmos db (based on the service name) using `AccountReadRegistryRepository`.
2. Use the mi-secretName value from the document to retrieve the MI credentials from the global key vault.
3. Use the mi credentials to talk to AAD and get the access token for the appropriate resource (storage account in this scenario).
4. Return access token to the caller.

### ExternalResourceTokenClient (Fhir Service)

1. Use refresh token from `RefreshTokenRepository` to validate itself and talk to `AccountRoutingService`.
2. Request an access token for the resource from `AccountRoutingService`. We will pass the resource uri along with our request (this will be got from the caller).
3. Return access token to the caller along with expiration time.
4. Optimization - We can cache the refresh tokens and use them to get new access tokens.

Note:
There is a dependency on the refresh token being refreshed regularly (for the first step in the process) by the exisiting Account Specific Cosmos Db token refresh flow. We don't update/refresh this token in the current managed identity flow. This is because we don't expect to use the MI credentials on a regular cadence unlike the Account Specific Cosmos Db. If we do modify the mechanism by which the Account Specific Cosmos Db tokesn are refreshed, we have to make sure we add a different mechanism to support `ExternalResourceTokenClient` talking to `AccountRoutingService`

### Export Job Worker (Fhir Service)

1. When it picks up a job, it will read the `JobRecord` to see if connection settings information is present. If so, it will use that to connect to the appropriate destination and process the export.
2. If there is no connection settings information in the `JobRecord`, the job worker will read the storage account information from the config. It will then use the `ExternalResourceTokenClient` to get appropriate access token for the storage account and process the export. It will periodically call the `ExternalResourceTokenClient` for a new token once the current one expires.

### ExportJob configuration values in PaaS

There are a number of configuration settings related to Bulk Export that are present as part of the fhir-server core code. We would like to override those values on the PaaS side (and in the future allow customers to update some of them). In order to achieve this we will add `ExportJobConfiguration` parameters and values as part of the `FhirAccountProperties`. This will get added to the `Account`/`Operation` document as part of the provisioning process. These values will then be added to the Service Fabric `FhirApplication` as parameters that are part of the `ApplicationManifest` file. The core fhir-server will then use these values to set up the appropriate `ExportJobConfiguration` values during the startup phase.

### OSS Changes

1. Currently the export code in OSS needs the client to pass in the storage account details. We will add code to make this optional and allow users to set a default storage account in the config.
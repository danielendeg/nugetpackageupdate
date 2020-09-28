
[[TOC]]

# Business Justification

This document outlines the steps necessary to provision, deprovision, and configure an IoMT Connector for an Azure API for FHIR instance.

# Scenarios

1. As an **"Azure API for FHIR"** service user, I want to provision a new IoMT connector for my Azure API for FHIR.
1. As an **"Azure API for FHIR"** service user, I want to deprovision an existing IoMT connector on my Azure API for FHIR.
1. As an **"Azure API for FHIR"** service user, I want to create a connection for my IoMT connector.
1. As an **"Azure API for FHIR"** service user, I want to delete a connection for my IoMT connector.
1. As an **"Azure API for FHIR"** service user, I want to update the device mappings for my IoMT connector.
1. As an **"Azure API for FHIR"** service user, I want to update the FHIR mappings for my IoMT connector.
1. As an **"Azure API for FHIR"** service user, I want to retrieve the connection strings for my IoMT connection.
1. As an **"Azure API for FHIR"** service user, I want to regenerate the keys for my IoMT connection.

# Out of Scope

The Move operation is defined as a separate User Story.

# Design

## Assumptions

* IoMT components will be created on a separate subscription from the existing Azure API for FHIR production subscription.
* The Resource Worker principal will be granted contributor access to the IoMT subscription.
* The current design calls for one IoMT subscription but entities are modeled such that the infrastructure subscription id is saved with the resource incase more subscriptions are needed in the future.
* IoMT operations are serialized per Account like existing FHIR service operations. At any point there should be at most one outstanding operation for an Account.  If additional ones are submitted prior the current completing an exception will be thrown. Users can sequence operations through ARM using dependsOn.
* Scenarios 1 through 6 will be implemented through operations and commands executed by the Resource Worker.
* Scenarios 7 & 8 will be executed synchronously by the ResourceProvider (details below).

## Cluster

Add the following property to the the Cluster document stored for each cluster.

```c#

    public class ClusterMetadata
    {
        ...

        /// <summary>
        /// The IoMT service metadata for the cluster.
        /// </summary>
        public IomtServiceMetadata IomtServiceDefinition { get; set; }
    }

    public class ServiceTypeMetadata
    {
        /// <summary>
        /// Name of the service the metadata represents,
        /// </summary>
        public string ServiceTypeName { get; set; }

        /// <summary>
        /// The default infrastructure subscription id to use when allocationing new resources for the service.
        /// </summary>
        public string DefaultServiceSubscriptionId { get; set; }

        /// <summary>
        /// Dictionary of applications keyed by ApplicationTypeName the service uses and their last known good version to use.
        /// </summary>
        public IDictionary<string, ApplicationTypeMetadata> ApplicationTypeLkg { get; set; }
    }

    public class IomtServiceMetadata : ServiceTypeMetadata
    {
        public IomtServiceMetadata()
        {
            ServiceTypeName = nameof(IomtServiceMetadata);
        }
        
        /// <summary>
        /// The app service plan id that should be used when a connector is provisioned.
        /// </summary>
        public string DefaultAppServicePlanId { get; set; }
    }
```

A single new property, of ``IomtServiceMetadata``, will contain the necessary metadata for the IoMT service in a given cluster. If the service entry doesn't exist for the specific cluster then the corresponding service is considered not supported in that cluster and a proper exception will be thrown. ``IomtServiceMetadata`` will inherit from the class ``ServiceTypeMetadata`` which has common properties defining a default service subscription id and a dictionary of ApplicationTypeMeta.  The common class can be used by other services added in the future that need settings defined at the cluster level.  In the future if there are standard properties that all services should have access too they be present here.

Specific types for services are used over a generic collection of ServiceTypeMetadata to allow services to define specific properties they may need like the ``DefaultAppServicePlanId`` in the ``IomtServiceMetadata`` definition.

The default subscription id allows services to have different infrastructure subscriptions.  It also provides a straight forward way to increase capacity if needed (generate a new production subscription and assign as the default to the cluster).  Long term this will probably need to be replaced with a richer set of functionality that has a subscription pool and assigns based on available capacity.

The ApplicationTypeMetadata defines any specific applications and their versions that should be deployed as part of the service.  After those service versions are updated and validated as part of an upgrade this version stored here will be updated so new deployments can use the latest code.

The path to the current version of the IoMT Azure Function package to deploy will be stored as part of the ``ApplicationTypeEntry`` for the ``IoMTConverterApplicationType.R4``.  This package location will be referenced during provisioning to deploy the Azure Function code.

## ResourceProviderServiceEnvironment

Add the following property to the ``ResourceProviderServiceEnvironment`` for use during the IoMT Connector provisioning process.

```c#

        /// <summary>
        /// The Resource Provider service principal object id.  Use to grant the Resource Provider specific rights on deployed resources (key retrieval, key rotations, etc).
        /// </summary>
        public string ResourceProviderServicePrincipalObjectId { get; set; }
```

## Commands

### IomtConnectorProvisionCommand

Will deploy a new IoMT Connector or update an existing based on the supplied ``IomtConnectorOperationDocument`` document.

The steps for provisioning are:

1. Acquire Cosmos distributed lock on IoMT Connector Id.  Key rotation workers will share this lock.
1. Retrieve ServiceTypeMetadata for the IoMTServiceType for the cluster. Set default subscription id and LKG version of Azure Function package.
1. **If new** Generate infrastructure resource names and settings and save in the IoMT Connector metadata.
1. Call ``IIomtConnectorProvisioningProvider.ProvisionIomtConnectorAsync``

    * Load ARM template for IoMT Connector.
    * Generate read only SAS for accessing IoMT Connector Azure Function source code.
    * Update ARM template with necessary parameters from IoMT Connector, Operation document, and SAS for source code.
    * Deploy ARM template.
    * Return managed identity of Azure Function created.
1. Update IoMT Connector document with settings from the operation document.  Including the managed identity of the Azure Function deploy returned by the provisioning step.
1. Call ``IFhirServerSettingsUpdater.UpdateFhirServerSettings`` to enable access for the IoMT Connector.
1. Create metadata for key rotation workers.
1. Save updated IoMT Connector metadata with status set to Provisioned.
1. Release lock.

### IomtConnectorDeprovisionCommand

Will delete an existing IoMT Connector based on the supplied ``IomtConnectorOperationDocument`` document.

1. Acquire Cosmos distributed lock on IoMT Connector Id.  Key rotation workers will share this lock.
1. Delete the ResourceGroup for the IoMT Connector by calling ``IIomtConnectorProvisioningProvider.DeprovisionIomtConnectorAsync``
1. Call ``IFhirServerSettingsUpdater.UpdateFhirServerSettings`` to revoke access for the IoMT Connector deleted.
1. Delete key rotation worker metadata for the IoMT Connector.
1. Delete IoMT Connector metadata.
1. Release lock.

### IomtConnectorMappingCommand

Will update the mappings for the IoMT Connector based on the supplied ``IomtConnectorConnectionOperationDocument``.

1. Determine the mapping type from the operation.
1. Call either ``IIomtConnectorMappingProvider.SetDeviceMappingAsync`` or ``IIomtConnectorMappingProvider.SetFhirMappingAsync`` based on the mapping type.
1. Update the mapping value in the IoMT Connector metadata.

### IomtConnectorCreateConnectionCommand

Is used to create the a write only Authorization Rule to the ``devicedata`` Event Hub so the user can send data to the IoMT connector.  Uses supplied ``IomtConnectorConnectionOperationDocument``.

1. Call ``IIomtConnectorConnectionProvider.CreateConnectionAsync``
1. Add connection to list in IoMT Connector metadata.

### IomtConnectorDeleteConnectionCommand

Is used to delete an Authorization Rule to the ``devicedata`` Event Hub used send data to the IoMT connector. Uses supplied ``IomtConnectorConnectionOperationDocument``.

1. Call ``IIomtConnectorConnectionProvider.DeleteConnectionAsync``
1. Remove connection to list in IoMT Connector metadata.

## Providers

```c#
    public interface IIomtConnectorProvisioningProvider
    {
        /// <summary>
        /// Deploys an IoMT Connector.
        /// </summary>
        /// <param name="iomtConnector">IoMT Connector data.</param>
        /// <param name="operation">Operation data.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns><see cref="Task"/></returns>
        Task ProvisionIomtConnectorAsync(IomtConnector iomtConnector, IomtConnectorOperationDocument operation, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes an IoMT Connector.
        /// </summary>
        /// <param name="iomtConnector">IoMT Connector data.</param>
        /// <param name="operation">Operation data.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns><see cref="Task"/></returns>
        Task DeprovisionIomtConnectorAsync(IomtConnector iomtConnector, IomtConnectorOperationDocument operation, CancellationToken cancellationToken);
    }
```

```c#
    public interface IIomtConnectorConnectionProvider
    {
        /// <summary>
        /// Creates a write only Authorization Rule with the given name on the devicedata Event Hub.
        /// </summary>
        /// <param name="iomtConnector">IoMT Connector data.</param>
        /// <param name="operation">Operation data.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns><see cref="Task"/></returns>
        Task CreateConnectionAsync(IomtConnector iomtConnector, IomtConnectorConnectionOperationDocument operation, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes an Authorization Rule with the given name on the devicedata Event Hub.
        /// </summary>
        /// <param name="iomtConnector">IoMT Connector data.</param>
        /// <param name="operation">Operation data.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns><see cref="Task"/></returns>
        Task DeleteConnectionAsync(IomtConnector iomtConnector, IomtConnectorConnectionOperationDocument operation, CancellationToken cancellationToken);
    }
```

```c#
    public interface IIomtConnectorMappingProvider
    {
        /// <summary>
        /// Sets the device mappings an IoMT Connector
        /// </summary>
        /// <param name="iomtConnector">IoMT Connector data.</param>
        /// <param name="operation">Operation data.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns><see cref="Task"/></returns>
        Task SetDeviceMappingAsync(IomtConnector iomtConnector, IomtConnectorMappingOperationDocument operation, CancellationToken cancellationToken);

        /// <summary>
        /// Sets the FHIR mappings an IoMT Connector
        /// </summary>
        /// <param name="iomtConnector">IoMT Connector data.</param>
        /// <param name="operation">Operation data.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns><see cref="Task"/></returns>
        Task SetFhirMappingAsync(IomtConnector iomtConnector, IomtConnectorMappingOperationDocument operation, CancellationToken cancellationToken);
    }
```

## IAzureFacades

### IStorageAccountFacade

```c#
        /// <summary>
        /// Creates a read only SAS to access the specified blob.
        /// </summary>
        /// <param name="accountName">The blob account.</param>
        /// <param name="containerName">The blob container.</param>
        /// <param name="blobName">The blob to generated the SAS token for.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>Readonly SAS to specified blob.</returns>
        Task<string> GenerateReadOnlySasAsync(string accountName, string containerName, string blobName, CancellationToken cancellationToken);

        /// <summary>
        /// Writes a blob with the given content.
        /// </summary>
        /// <param name="accountName">The blob account.</param>
        /// <param name="containerName">The blob container.</param>
        /// <param name="blobPath">The blob to generated the SAS token for.</param>
        /// <param name="content">The string content to save into the blob.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns><see cref="Task"</returns>
        Task SaveBlobAsync(string accountName, string containerName, string blobPath, string content, CancellationToken cancellationToken);
```

IStorageAccountFacade adds two new methods.  The first allows the caller to specify a blob file and generate a read only SAS (Shared Access Signature).  The method will be used to create a short term read only SAS (~2 hours) to the ZIP file stored on our infrastructure blob that contains the IoMT connector code.  This SAS will be provided to the ARM template deploying the Azure Functions and use the MSDeploy extension to deploy the source code for the function that is provisioned as part of deployment.

The second method is used to set the mappings used by the IoMT Connector.

### IEventHubFacade

```c#
        /// <summary>
        /// Regenerates a key of the given type and returns the new keys for the Authorization Rule.
        /// </summary>
        /// <param name="ruleName">The Event Hub Authorization Rule.</param>
        /// <param name="keyType">The Event Hub Authorization Rule key type.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns><see cref="IEventHubAuthorizationKey"/></returns>
        Task<IEventHubAuthorizationKey> RegenerateKeysAsync(string ruleName, KeyType keyType, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the keys for a given Event Hub Authorization Rule.
        /// </summary>
        /// <param name="ruleName">The Event Hub Authorization Rule.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns><see cref="IEventHubAuthorizationKey"/></returns>
        Task<IEventHubAuthorizationKey> ListKeysAsync(string ruleName, CancellationToken cancellationToken);

        /// <summary>
        /// Creates an Authorization Rule on the Event Hub.
        /// </summary>
        /// <param name="ruleName">The Event Hub Authorization Rule name.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <param name="accessRights">The access rights (Read, Write, or Manage) to the Authorization rule.</param>
        /// <returns><see cref="Task"/></see></returns>
        Task CreateAuthorizationRule(string ruleName, CancellationToken cancellationToken, params AccessRights[] accessRights);

        /// <summary>
        /// Deletes an Authorization Rule on the Event Hub.
        /// </summary>
        /// <param name="ruleName">The Event Hub Authorization Rule name.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns><see cref="Task"/></see></returns>
        Task DeleteAuthorizationRule(string ruleName, CancellationToken cancellationToken);
```

The ``IEventHubFacade`` adds four new methods to support scenarios 7 & 8. These scenarios will be executed synchronously by the ``ResourceProvider`` not the ``ResourceWorker``.  These won't be issued as operations and completed as commands.  These calls return secrets to the caller (Event Hub authorization rule keys).  To execute them asynchronously would require the completed operation to store the keys.  The underlying EventHub APIs invoked for both operations are not long running.  This allows us to call them directly from the Resource Provider and send the response directly to the caller avoiding storing the keys in the Global Database.  Normally the principal running the ResourceProvider won't have the necessary rights on the Azure resources to support this operation.  To resolve this the principal running the Resource Provider will be granted the necessary role on the Event Hub to execute both commands as part of IoMT Connector deployment.  Further details available in the security section.

The first method is very similar the existing ``RegenerateKeyAsync``.  The difference is the new method ``RegenerateKeysAsync`` will return all keys instead of just the key rotated.  This is so the external API contract can be honored.  The second method added allows retrieval of the keys so the user can access their IoMT connector.

The last two methods will be used to create or delete Authorization Rules that serve as the connections to into the IoMT connector.  The rules created during the execution of the ``IomtConnectorCreateConnectionCommand`` will be write.

# Metrics

All commands listed in the design will be instrumented with the type, duration, and outcome.  If a particular command has several discrete steps, those will be instrumented as well.

# Test Strategy

Unit Tests will be developed for all components where possible.  Integration tests will be created for Providers where possible.  Full E2E tests for scenarios will be added a later date once all components are wired and accessible.

# Security

## Custom Role

```json
{
    "properties": {
        "roleName": "Event Hub Key Manager",
        "description": "Allows controller to read and regenerate authorization rule keys.",
        "assignableScopes": [
            "/subscriptions/d1ccd58b-15d5-40f0-a533-f996d447ee57"
        ],
        "permissions": [
            {
                "actions": [
                    "Microsoft.EventHub/namespaces/authorizationRules/listkeys/action",
                    "Microsoft.EventHub/namespaces/authorizationRules/regenerateKeys/action"
                ],
                "notActions": [],
                "dataActions": [],
                "notDataActions": []
            }
        ]
    }
}
```

For the Resource Provider to handle connection string and key rotation requests the principal running the service needs access to the underlying Event Hub actions.  Applying the principal of least privilege we can create a custom role like above to restrict access to just the actions the Resource Provider needs. One downside is it appears the custom role lives at the subscription level.  Not an issue currently but will become problematic once we introduce additional subscriptions.  The standard role that grants the access we need is Azure Event Hub Data Owner (f526a384-b230-433a-b45c-95f59c4a2dec) but it grants full control.

The Resource Worker will be configured as a contributor on the new IoMT subscription which mirrors the current configuration on the existing production subscription.  This will allow the Resource Worker to create and delete the resources as necessary.

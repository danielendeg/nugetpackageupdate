# Global Db Objects for IoMT Connector

To support the addition of the IoMT Connector as a feature of the Azure API for FHIR additional entities are needed in the Global Database to define the IoMT connector, its operations, and infrastructure related data sets.

[[_TOC_]]

## Scenarios

At a high level there are several scenarios we hope to enable.

1. Support child and related resources under with common functionality and patterns.
1. Define entity(s) for the IoMT Connector and its components.
1. Define entity(s) for the IoMT Connector operations.
1. Support processing of IoMT Connector operations.
1. Support and define IoMT Connector key rotation entities.
1. Support tracking necessary metadata on service entities for multiple infrastructure subscriptions.
1. Support infrastructure resource tracking and allocation.

Scenarios 1 through 6 will be covered in the first phase of the design.  Scenario 7 will be covered in a future iteration.  Some examples for scenario 7 include tracking and assigning the infrastructure subscription based on the service type and remaining capacity or allocation the App Service Plan for a given region based on available capacity.

## Design

### Entities

The `IChildResource` interface defines properties to associate a ChildResource with its parent. For example mapping an IoMT Connector service with parent FHIR API service.

```c#
    public interface IChildResource
    {
        /// <summary>
        /// Id of the parent, example 'hlvtf8c5mk3fjreha6tjexqu7'
        /// </summary>
        string ParentId { get; }

        /// <summary>
        /// Name of the parent, example 'fhirservicename'
        /// </summary>
        string ParentName { get; }

        /// <summary>
        /// Type of the parent, example 'account'
        /// </summary>
        string ParentType { get; }

        /// <summary>
        /// Type of the child, example 'iomtConnector'
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Name of the child resource, example 'ParentResourceName\ChildResourceName'
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Subscription id the child belongs to
        /// </summary>
        string SubscriptionId { get; }
    }
```

Common class for standardizing how child resources based off of the AccountRegistryDocumentModel.

```c#
    public abstract class ChildResource : AccountRegistryDocumentModel<string>,
        IChildResource
    {
        [JsonProperty("parentId")]
        public string ParentId { get; set; }

        [JsonProperty("parentName")]
        public string ParentName { get; set; }

        [JsonProperty("parentType")]
        public string ParentType { get; set; }

        [JsonProperty("subscriptionId")]
        public string SubscriptionId { get; set; }

        public override string PartitionKey => SubscriptionId;

        [JsonProperty("searchIndex")]
        protected virtual IReadOnlyDictionary<string, string> SearchIndex { get; set; }
    }
```

The `IInfrastructureEntity` and `IInfrastructureResource` interfaces define properties to track services and resources maintained on one or more infrastructure subscriptions.  

Properties like `InfrastructureSubscriptionId` differ from similar properties like `SubscriptionId`.  The latter tracks the customer's subscription id where as the former tracks the infrastructure subscription the resource is associated with.  

```c#
public interface IInfrastructureEntity
{
    // Subscription Id the infrastructure resource belongs to. Example 'c243745d-50bb-48d1-9e62-3f72efb3166c'
    string InfrastructureSubscriptionId { get; }

    // Resource Group Name, example 'rg-iomt-fhirsrv-name'
    string InfrastructureResourceGroupName  { get; }

    // The resource location, example 'US West2'
    string InfrastructureLocation { get; }
}
```

Extension of IInfrastructureEntity.  Includes properties to track specific resources deployed on behalf of the service. These resources aren't directly visible to the customer and are maintained by our service.  Examples include the StreamAnalytics job and EventHub Namespace created for the IoMT Connector.

```c#
public interface IInfrastructureResource : IInfrastructureEntity
{
    // The full resource id, example 'Microsoft.EventHub/namespaces/accountA-iomt1-ehn'
    string InfrastructureResourceId { get; }

    // The name of the resource, example 'accountA-iomt1-ehn'
    string InfrastructureResourceName { get; }

    // The type of resource, example 'Microsoft.EventHub/namespaces'
    string InfrastructureResourceType { get; }
}
```

In order to support common SearchIndexing for child resources an extension method will be available for any class implementing the IChildResource.

```c#
    public static class ChildResourceExtensions
    {
        /// <summary>
        /// Defines common search indexes for IChildResources and merges them with additional properties provided
        /// </summary>
        /// <typeparam name="TChild">The type of ChildResource</typeparam>
        /// <param name="child">The child resource to define search parameters for.</param>
        /// <param name="properties">Optional properties of the child to add in addition to the common search indexes.</param>
        /// <returns>A search index dictionary for the child resource</returns>
        public static SearchIndexDictionary<TChild> CreateSearchIndex<TChild>(this TChild child, params Expression<Func<TChild, string>>[] properties)
             where TChild : class, IChildResource
        {
            Expression<Func<TChild, string>>[] extendedProperties = (properties ?? Array.Empty<Expression<Func<TChild, string>>>())
                .Union(CommonChildSearchIndexExpressions<TChild>())
                .ToArray();

            return new SearchIndexDictionary<TChild>(child, extendedProperties);
        }

        /// <summary>
        /// Defines common search indexes across all ChildResources
        /// </summary>
        /// <typeparam name="TChild">The type of ChildResource</typeparam>
        /// <returns>An enumerable containing common search index expressions for a ChildResource</returns>
        private static IEnumerable<Expression<Func<TChild, string>>> CommonChildSearchIndexExpressions<TChild>()
            where TChild : IChildResource
        {
            yield return cr => cr.Name;
            yield return cr => cr.Type;
            yield return cr => cr.ParentId;
            yield return cr => cr.ParentName;
            yield return cr => cr.ParentType;
        }
    }
```

Defines the IomtConnector metadata.

```c#
   public class IomtConnector : ChildResource,
        IResourceIdentity,
        IInfrastructureEntity,
        IValidatableObject
    {
        public const string TypeValue = "iomtConnector";

        public IomtConnector()
        {
            SearchIndex = this.CreateSearchIndex(
                p => p.ResourceGroupName,
                p => p.ResourceTypeName,
                p => p.ProviderNamespace);
        }

        public IomtConnector(Account account)
            : this()
        {
            EnsureArg.IsNotNull(account, nameof(account));

            ParentId = account.Id;
            ParentName = account.Name;
            ParentType = account.Type;
        }

        /// <summary>
        /// Name from ARM will be in format ParentResourceName\ChildResourceName.  Name and Type will ensure connector name is unique for a given FHIR service.
        /// </summary>
        public override string Type => TypeValue;

        [JsonProperty("infrastructureSubscriptionId")]
        public string InfrastructureSubscriptionId { get; set; }

        [JsonProperty("infrastructureResourceGroupName")]
        public string InfrastructureResourceGroupName { get; set; }

        [JsonProperty("infrastructureLocation")]
        public string InfrastructureLocation { get; set; }

        [JsonProperty("iomtProperties")]
        public IomtConnectorProperties Properties { get; set; } = new IomtConnectorProperties();

        [JsonProperty("eventHub")]
        public IomtEventHubResourceDefinition EventHub { get; set; } = new IomtEventHubResourceDefinition();

        [JsonProperty("streamAnalytics")]
        public IomtStreamAnalyticsResourceDefinition StreamAnalytics { get; set; } = new IomtStreamAnalyticsResourceDefinition();

        [JsonProperty("storage")]
        public IomtStorageResourceDefinition Storage { get; set; } = new IomtStorageResourceDefinition();

        [JsonProperty("function")]
        public IomtFunctionAppResourceDefinition Function { get; set; } = new IomtFunctionAppResourceDefinition();

        [JsonProperty("keyVault")]
        public IomtKeyVaultResourceDefinition KeyVault { get; set; } = new IomtKeyVaultResourceDefinition();

        /// <summary>
        /// The current version of the source code deployed to the connector
        /// </summary>
        [JsonProperty("connectorVersion")]
        public string ConnectorVersion { get; set; }

        [JsonProperty("resourceGroupName")]
        public string ResourceGroupName { get; set; }

        [JsonProperty("providerNameSpace")]
        public string ProviderNamespace { get; set; }

        [JsonProperty("resourceTypeName")]
        public string ResourceTypeName { get; set; }

        [JsonProperty("state")]
        public ResourceState State { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            throw new System.NotImplementedException();
        }
    }
```

```c#
public class IomtConnectorProperties : IValidatableObject
{
    [JsonProperty("serviceConfiguration")]
    public IomtServiceConfiguration IomtServiceConfiguration { get; set; }

    [JsonProperty("deviceMapping")]
    public string DeviceMapping {get; set; }

    [JsonProperty("fhirMapping")]
    public string FhirMapping {get; set; }
}
```

IomtServiceConfiguration contains user defined settings for the service's operation. For public preview there is only one setting. Validate will ensure the ResourceIdentityResolutionType is valid.

```c#
    public class IomtServiceConfiguration : IValidatableObject
    {
        [JsonProperty("resourceIdentityResolutionType")]
        public string ResourceIdentityResolutionType { get; set; } = "LookUp";
    }
```

Below is a list of classes used to model the underlying resources used by the IoMT connector.  The proposal is to add a new namespace `Resources` under `Microsoft.Health.Cloud.ResourceProvider.Storage.Entities` to organize these classes.  All the classes implement the IInfrastructureResource interface to track key pieces of information.

Modeling the complete underlying resources from Azure was considered.  Doing so results in modeling many properties and entities that aren't currently being used and at the same time lacks the specific knowledge needed for the service use cases.  For example if we had a generic event hub model we will still need an IoMT specific model to designate the device and normalization endpoints the system uses.

Some of the below classes include refactors of classes previously defined as part of the [Secret Rotation](https://microsofthealth.visualstudio.com/Health/_wiki/wikis/POET.wiki/93/SecretRotationSpec) design.  The specific classes are called out in the comments below.

```c#
namespace Microsoft.Health.Cloud.ResourceProvider.Storage.Entities.Resources
{
    public class IomtEventHubResourceDefinition : IInfrastructureResource
    {
        [JsonProperty("infrastructureSubscriptionId")]
        public string InfrastructureSubscriptionId { get; set; }

        [JsonProperty("infrastructureResourceGroupName")]
        public string InfrastructureResourceGroupName  { get; set; }

        [JsonProperty("infrastructureLocation")]
        public string InfrastructureLocation { get; set; }

        [JsonProperty("infrastructureResourceId")]
        public string InfrastructureResourceId { get; set; }

        [JsonProperty("infrastructureResourceName")]
        public string InfrastructureResourceName { get; set; }

        [JsonProperty("infrastructureResourceType")]
        public string InfrastructureResourceType { get; set; }

        [JsonProperty("throughputUnits")]
        public int ThroughputUnits { get; set; }

        [JsonProperty("deviceDataEventHubName")]
        public string DeviceEventHubName { get; set; } = "deviceinput";

        [JsonProperty("normalizedEventHubName")]
        public string NormalizedEventHubName { get; set; } = "normalizeddeviceinput";

        // Contains the list of write only 'connections' the user has created through the API
        [JsonProperty("deviceWritePolicyNames")]
        public IList<string> DeviceWritePolicyNames { get; set; } = new List<string>();
    }

    // This class will replace StreamAnalyticsConfig from the prior SecretRotationSpec
    public class IomtStreamAnalyticsResourceDefinition : IInfrastructureResource
    {
        [JsonProperty("infrastructureSubscriptionId")]
        public string InfrastructureSubscriptionId { get; set; }

        [JsonProperty("infrastructureResourceGroupName")]
        public string InfrastructureResourceGroupName  { get; set; }

        [JsonProperty("infrastructureLocation")]
        public string InfrastructureLocation { get; set; }

        [JsonProperty("infrastructureResourceId")]
        public string InfrastructureResourceId { get; set; }

        [JsonProperty("infrastructureResourceName")]
        public string InfrastructureResourceName { get; set; }

        [JsonProperty("infrastructureResourceType")]
        public string InfrastructureResourceType { get; set; }

        [JsonProperty("StreamingUnits")]
        public int StreamingUnits { get; set; } = 1;

        [JsonProperty("EventHubNamespace")]
        public string EventHubNamespace { get; set; }

        [JsonProperty("EventHubName")]
        public string EventHubName { get; set; } = "normalizeddeviceinput";

        [JsonProperty("sharedAccessPolicyName")]
        public string SharedAccessPolicyName { get; set; } = "reader";

        [JsonProperty("outputStartMode")]
        public string OutputStartMode { get; set; } = "LastOutputEventTime";

        [JsonProperty("functionAppName")]
        public string FunctionAppName { get; set; }

        [JsonProperty("functionName")]
        public string FunctionName { get; set; } = "MeasurementCollectionToFhir";

        [JsonProperty("jobWindowUnit")]
        public string JobWindowUnit { get; set; } = "MINUTE";

        [JsonProperty("jobWindowMagnitude")]
        public int JobWindowMagnitude { get; set; } = 15;

        public string BuildQuery()
        {
            // Will generate the query used in the Stream Analytics job.
        }
    }

    public class IomtStorageResourceDefinition : IInfrastructureResource
    {
        [JsonProperty("infrastructureSubscriptionId")]
        public string InfrastructureSubscriptionId { get; set; }

        [JsonProperty("infrastructureResourceGroupName")]
        public string InfrastructureResourceGroupName  { get; set; }

        [JsonProperty("infrastructureLocation")]
        public string InfrastructureLocation { get; set; }

        [JsonProperty("infrastructureResourceId")]
        public string InfrastructureResourceId { get; set; }

        [JsonProperty("infrastructureResourceName")]
        public string InfrastructureResourceName { get; set; }

        [JsonProperty("infrastructureResourceType")]
        public string InfrastructureResourceType { get; set; }

        [JsonProperty("mappingContainer")]
        public string MappingContainer { get; set; } = "template";
    }

    public class IomtFunctionAppResourceDefinition : IInfrastructureResource
    {
        [JsonProperty("infrastructureSubscriptionId")]
        public string InfrastructureSubscriptionId { get; set; }

        [JsonProperty("infrastructureResourceGroupName")]
        public string InfrastructureResourceGroupName  { get; set; }

        [JsonProperty("infrastructureLocation")]
        public string InfrastructureLocation { get; set; }

        [JsonProperty("infrastructureResourceId")]
        public string InfrastructureResourceId { get; set; }

        [JsonProperty("infrastructureResourceName")]
        public string InfrastructureResourceName { get; set; }

        [JsonProperty("infrastructureResourceType")]
        public string InfrastructureResourceType { get; set; }

        [JsonProperty("functionAppServicePlanId")]
        public string FunctionAppServicePlanId { get; set; }

        [JsonProperty("fhirConversionFunctionName")]
        public string FhirConversionFunctionName { get; set; } = "MeasurementCollectionToFhir";

        [JsonProperty("deviceDataNormalizationFunctionName")]
        public string DeviceDataNormalizationFunctionName { get; set; } = "NormalizeDeviceData";

        [JsonProperty("managedIdentityObjectId")]
        public string ManagedIdentityObjectId { get; set; }
    }

    public class IomtKeyVaultResourceDefinition : IInfrastructureResource
    {
        [JsonProperty("infrastructureSubscriptionId")]
        public string InfrastructureSubscriptionId { get; set; }

        [JsonProperty("infrastructureResourceGroupName")]
        public string InfrastructureResourceGroupName  { get; set; }

        [JsonProperty("infrastructureLocation")]
        public string InfrastructureLocation { get; set; }

        [JsonProperty("infrastructureResourceId")]
        public string InfrastructureResourceId { get; set; }

        [JsonProperty("infrastructureResourceName")]
        public string InfrastructureResourceName { get; set; }

        [JsonProperty("infrastructureResourceType")]
        public string InfrastructureResourceType { get; set; }

        [JsonProperty("webJobStorageSecretName")]
        public string WebJobStorageSecretName { get; set; } = "blob-storage-cs";
    }
}
```

`ResourceState` is currently identical to `AccountState`.  The new enumeration is to allow statuses of accounts and underlying resources to diverge in the future if necessary.

```c#
    public enum ResourceState
    {
        /// <summary>
        /// The resource's provisioning request has been accepted
        /// </summary>
        Accepted = 0,

        /// <summary>
        /// The resource is provisioned
        /// </summary>
        Provisioned = 1,

        /// <summary>
        /// The resource was deprovisioned
        /// </summary>
        Deprovisioned = 2,

        /// <summary>
        /// The resource is being provisioned for the first time
        /// </summary>
        Provisioning = 3,

        /// <summary>
        /// The resource is being verified
        /// </summary>
        Verifying = 4,

        /// <summary>
        /// The resource is undergoing an update/reprovisioning
        /// </summary>
        Updating = 5,

        /// <summary>
        /// The resource failed to be provisioned
        /// </summary>
        Failed = 6,

        /// <summary>
        /// The resource will be deprovisioned
        /// </summary>
        Deprovisioning = 7,

        /// <summary>
        /// The resource is in a moving state
        /// </summary>
        Moving = 8,
    }
```

Below is an example refactoring of the `AccountSpecificStreamAnalyticsJob` class used for secret rotation.  The StreamAnalyticsConfig property is removed.  Instead `AccountSpecificStreamAnalyticsJob` will be a child resource of the IoMT connector and the full connector complete with the Stream Analytics settings will be loaded and available.  The IomtConnector Property is also removed because it is redundant with the ParentResourceName from the `ChildResource` base class.  The id & name will be 

```c#

public class AccountSpecificStreamAnalyticsJob : ChildResource,
    ISecretMetadata,
    IChildResource
{
    public AccountSpecificStreamAnalyticsJob()
    {
        this.ResourceTypeName = AccountSpecificStreamAnalyticsJob.TypeValue;
    }

    public AccountSpecificStreamAnalyticsJob(IomtConnector iomtConnector)
        : this()
    {
        EnsureArg.IsNotNull(iomtConnector, nameof(iomtConnector));

        this.ParentId = iomtConnector.Id;
        this.ParentName = iomtConnector.Name;
        this.ParentResourceTypeName = iomtConnector.ResourceTypeName;
    }

    public const string TypeValue = "accountSpecificStreamAnalyticsJob";

    public override string Type => TypeValue;

    public string ResourceGroup { get; set; }

    public string SubscriptionId { get; set; }

    public override string PartitionKey => SubscriptionId;

    public bool IsUsingPrimaryKey { get; set; }

    public DateTimeOffset SecretLastRotated { get; set; }

    public int SecretRotationCount { get; set; }

    public bool ForceRotation { get; set; }
}

```

To support operations across different services the current OperationDocument will be refactored to split generic components into an `OperationDocument` abstract class and a `FhirServiceOperationDocument` which will be a combination of the generic properties with the FHIR specific attributes.  The DocumentType will remain `"provisioningrequest"` and the PartitionId will remain the customer's subscription id.  This will continue the current pattern of only one outstanding request per customer's subscription. Restricting to one outstanding request per subscription eliminates certain complex scenarios.  For example if a customer sends a request to create an IoMT connector for a FHIR service but then while the first request is still processing issues a second request to delete the underlying FHIR service we would need to ensure the resources created in the first request were eventually deleted. If one request per subscription becomes too limiting we can reevaluate and allow some operations to run concurrently but we will need to guard against or account for problematic scenarios like the one above.

The `ProvisioningOrchestrator` class will now be responsible for delegating requests to the appropriate handler based on the `ResourceType` property.  Specifics are covered later in the design document.

```c#
public abstract class BaseOperationDocument : AccountRegistryDocumentModel<string>, IResourceIdentity
{
    public const string DocumentType = "provisioningrequest";

    public BaseOperationDocument()
    {
        SearchIndex = new SearchIndexDictionary<BaseOperationDocument>(
            this,
            p => p.SubscriptionId,
            p => p.ResourceGroupName,
            p => p.AccountId,
            p => p.ProviderNamespace);
    }

    public enum OperationType
    {
        /// <summary>
        /// Request to provision an account
        /// </summary>
        Provision = 0,

        /// <summary>
        /// Request to deprovision an account
        /// </summary>
        Deprovision = 1,

        /// <summary>
        /// Request to change the managed identity associated with an account
        /// </summary>
        Patch = 2,
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1717:Only FlagsAttribute enums should have plural names", Justification = "Status is not plural.")]
    public enum Status
    {
        /// <summary>
        /// The Operation was requested but is not being operated on yet
        /// </summary>
        Requested = 0,

        /// <summary>
        /// The Operation was picked up by a worker
        /// </summary>
        Running = 1,

        /// <summary>
        /// The operation failed after several retries
        /// </summary>
        Failed = 2,

        /// <summary>
        /// The operation was completed
        /// </summary>
        Completed = 3,

        /// <summary>
        /// The operation was canceled
        /// </summary>
        Canceled = 4,
    }

    public override string Type => DocumentType;

    [JsonProperty("provisioningOperation")]
    public OperationType ProvisioningOperation { get; set; }

    [JsonProperty("provisioningStatus")]
    public Status ProvisioningStatus { get; set; }

    [JsonProperty("primaryRegionName")]
    public string PrimaryRegionName { get; set; }

    [JsonProperty("secondaryRegionNames")]
    public IReadOnlyList<string> SecondaryRegionNames { get; set; }

    [JsonProperty("timeEnqueued")]
    public DateTime TimeEnqueued { get; set; }

    [JsonProperty("timeLastUpdated")]
    public DateTime TimeLastUpdated { get; set; }

    [JsonProperty("timeEnded")]
    public DateTime? TimeEnded { get; set; }

    [JsonProperty("accountId")]
    public string AccountId { get; set; }

    [JsonProperty("loggingParentId")]
    public string LoggingParentId { get; set; }

    [JsonProperty("loggingRootId")]
    public string LoggingRootId { get; set; }

    [JsonProperty("attemptCount")]
    public int AttemptCount { get; set; }

    [JsonProperty("exceptionDetails")]
    public string ExceptionDetails { get; set; }

    [JsonProperty("skuName")]
    public string SkuName { get; set; }

    [JsonProperty("percentComplete")]
    public double? PercentComplete { get; set; }

    public override string PartitionKey => SubscriptionId;

    [JsonProperty("searchIndex")]
    protected IReadOnlyDictionary<string, string> SearchIndex { get; }

    [JsonProperty("subscriptionId")]
    public string SubscriptionId { get; set; }

    [JsonProperty("resourceGroupName")]
    public string ResourceGroupName { get; set; }

    [JsonProperty("providerNamespace")]
    public string ProviderNamespace { get; set; }

    [JsonProperty("resourceTypeName")]
    public string ResourceTypeName { get; set; }

    public bool IsTerminalStatus()
    {
        return ProvisioningStatus == Status.Canceled || ProvisioningStatus == Status.Completed || ProvisioningStatus == Status.Failed;
    }
}
```

The OperationDocument now extends the BaseOperationDocument with the FHIR service specific attributes.

```c#
public class FhirAccountOperationDocument : OperationDocument
{
    public FhirAccountOperationDocument()
    {
    }

    public FhirAccountOperationDocument(IResourceIdentity src)
    : this()
    {
        EnsureArg.IsNotNull(src, nameof(src));

        this.CopyFrom(src);
    }

    [JsonProperty("offerThroughput")]
    public int OfferThroughput { get; set; }

    [JsonProperty("externalSecurityAuthenticationAuthority")]
    public Uri ExternalSecurityAuthenticationAuthority { get; set; }

    [JsonProperty("externalSecurityAuthenticationAudience")]
    public Uri ExternalSecurityAuthenticationAudience { get; set; }

    [JsonProperty("externalSecurityAuthenticationAllowedOids")]
    public IReadOnlyList<string> ExternalSecurityAuthenticationAllowedOids { get; set; }

    [JsonProperty("tags")]
    public IDictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

    [JsonProperty("corsOrigins")]
    public IReadOnlyList<string> CorsOrigins { get; set; }

    [JsonProperty("corsHeaders")]
    public IReadOnlyList<string> CorsHeaders { get; set; }

    [JsonProperty("corsMethods")]
    public IReadOnlyList<string> CorsMethods { get; set; }

    [JsonProperty("corsMaxAge")]
    public int? CorsMaxAge { get; set; }

    [JsonProperty("corsAllowCredentials")]
    public bool CorsAllowCredentials { get; set; }

    [JsonProperty("smartProxyEnabled")]
    public bool SmartProxyEnabled { get; set; }

    [JsonProperty("fhirVersion")]
    public FhirVersion FhirVersion { get; set; }

    [JsonProperty("externalManagedIdentity")]
    public ManagedIdentityEntity ExternalManagedIdentity { get; set; }
    }
}

```

```c#
public class IomtConnectorOperationDocument : OperationDocument
{
    public IomtConnectorOperationDocument()
    {
        this.ResourceTypeName = IomtConnector.ResourceTypeNameDefinition;
    }

    public IomtConnectorOperationDocument(IResourceIdentity src)
    : this()
    {
        EnsureArg.IsNotNull(src, nameof(src));

        this.CopyFrom(src);
    }

    [JsonProperty("iomtConnectorId")]
    public string IomtConnectorId { get; set; }

    [JsonProperty("serviceConfiguration")]
    public IomtServiceConfiguration IomtServiceConfiguration { get; set; }
}

```

`IomtConnectorMappingOperationDocument` class below handles operations to set or update mappings (Device & FHIR) on the IoMT Connector. **Note** because we can only handle one outstanding operation at a time per subscription we will need to make sure when we generate the ARM template for the customer that proper depends on are set (i.e. FHIR template depends on Device template).  This approach doesn't seem ideal and open to suggestions.

```c#
public class IomtConnectorMappingOperationDocument : OperationDocument
{
    public const string ResourceTypeNameDefinition = "IoMTConnectors/mappings";

    public IomtConnectorMappingOperationDocument()
    {
        this.ResourceTypeName = IomtConnectorMappingOperationDocument.ResourceTypeNameDefinition;
    }

    public IomtConnectorOperationDocument(IResourceIdentity src)
    : this()
    {
        EnsureArg.IsNotNull(src, nameof(src));

        this.CopyFrom(src);
    }

    [JsonProperty("iomtConnectorId")]
    public string IomtConnectorId { get; set; }

    [JsonProperty("mappingType")]
    public string MappingType { get; set; }

    [JsonProperty("mappingContent")]
    public string MappingContent { get; set; }
}

```

To accommodate the changes to Operation, the CompletedOperationDocument will be changed to reference the `OperationDocument` for the `Operation` property.  This is the generic base type common to all operations.  It will no longer be a `FhirAccountOperationDocument`.

```c#
public class CompletedOperationDocument : AccountRegistryDocumentModel<string>
{
    public const string DocumentType = "completedprovisioningrequest";

    public const string OperationDocumentPropertyName = "operation";

    public override string Type => DocumentType;

    [JsonProperty("accountName")]
    public string AccountName { get; set; }

    [JsonProperty("operation", Order = 0)]
    public BaseOperationDocument Operation { get; set; }

    [JsonProperty("partitionKey", Order = 1)]
    public override string PartitionKey => Operation.SubscriptionId;
}
```

### Repositories

Additions to repository interfaces and classes to support retrieval of parent and childern services.

```c#
public interface IAccountRegistryReadRepository
{
    Task<PagedResult<IEnumerable<TChildResource>>> GetChildResourcesByParentAsync<TChildResource>(string subscriptionId, string parentName, string parentType, string childType, CancellationToken cancellationToken)
        where TChildResource : IChildResource;

    Task<TChildResource> GetChildResourceByParentAndNameAsync<TChildResource>(string subscriptionId, string parentName, string parentType, string childName, string childType, CancellationToken cancellationToken)
        where TChildResource : IChildResource;

    Task<PagedResult<IEnumerable<TChildResource>>> GetChildResourcesByParentAsync<TChildResource>(string subscriptionId, string parentId, string childType, CancellationToken cancellationToken)
        where TChildResource : IChildResource;

    Task<TChildResource> GetChildResourceByParentAndNameAsync<TChildResource>(string subscriptionId, string parentId, string childName, string childType, CancellationToken cancellationToken)
        where TChildResource : IChildResource;

    Task<TParent> GetParentByChildAsync<TParent>(IChildResource child, CancellationToken cancellationToken);

    Task<TChildResource> GetChildResourceByIdAsync<TChildResource>(string subscriptionId, string childId, CancellationToken cancellationToken)
        where TChildResource : IChildResource;

    Task<TChildResource> GetChildResourceAsync<TChildResource>(IChildResource child, CancellationToken cancellationToken)
        where TChildResource : IChildResource;
}

public class AccountRegistryReadRepository : GlobalCosmosDBBaseRepository, IAccountRegistryReadRepository
{
    public Task<PagedResult<IEnumerable<TChildResource>>> GetChildResourcesByParentAsync<TChildResource>(string subscriptionId, string parentName, string parentType, string childType, CancellationToken cancellationToken)
        where TChildResource : IChildResource
    {
        /*
         * Query Global DB on documents in subscription partition where
         * searchIndex.ParentName == parentName
         * & searchIndex.ParentType == parentType
         * & searchIndex.Type == childType
         * & not deleted.
         */
    }

    public Task<TChildResource> GetChildResourceByParentAndNameAsync<TChildResource>(string subscriptionId, string parentName, string parentType, string childName, string childType, CancellationToken cancellationToken)
        where TChildResource : IChildResource
    {
        /*
         * Query Global DB on documents in subscription partition where
         * searchIndex.ParentName == parentName
         * & searchIndex.ParentType == parentType
         * & searchIndex.Name == childName
         * & searchIndex.Type == childType.
         */
    }

    public Task<PagedResult<IEnumerable<TChildResource>>> GetChildResourcesByParentAsync<TChildResource>(string subscriptionId, string parentId, string childType, CancellationToken cancellationToken)
        where TChildResource : IChildResource
    {
        /*
         * Query Global DB on documents in subscription partition where
         * * searchIndex.ParentId == parentId
         * & searchIndex.Type == childType
         * & not deleted.
         */
    }

    public Task<TChildResource> GetChildResourceByParentAndNameAsync<TChildResource>(string subscriptionId, string parentId, string childName, string childType, CancellationToken cancellationToken)
        where TChildResource : IChildResource
    {
        /*
         * Query Global DB on documents in subscription partition where
         * searchIndex.ParentId == parentId
         * & searchIndex.Name == childName
         * & searchIndex.Type == childType.
         */
    }

    public Task<TParent> GetParentByChildAsync<TParent>(IChildResource child, CancellationToken cancellationToken)
    {
        /*
         * Document lookup by childResource.ParentId in subscription partition.
         */
    }

    public Task<TChildResource> GetChildResourceByIdAsync<TChildResource>(string subscriptionId, string childId, CancellationToken cancellationToken)
        where TChildResource : IChildResource
    {
        /*
         * Document lookup by childResourceId in subscription partition.
         */
    }


    public Task<TChildResource> GetChildResourceAsync<TChildResource>(IChildResource child, CancellationToken cancellationToken)
        where TChildResource : IChildResource
    {
        /*
         * Document lookup by childResourceId in subscription partition.
         */
    }


public interface IAccountRegistryManagementRepository : IAccountRegistryReadRepository
{
    Task<IEnumerable<TChildResource>> CreateChildResourceMetadataAsync<TChildResource>(CancellationToken cancellationToken, params IChildResource[] childern)
        where TChildResource : IChildResource;

    Task<TChildResource> UpsertChildResourceMetadataAsync<TChildResource>(IChildResource child, CancellationToken cancellationToken)
        where TChildResource : IChildResource;

    Task<TChildResource> DeleteChildResourceMetadataAsync<TChildResource>(IChildResource child, CancellationToken cancellationToken)
        where TChildResource : IChildResource;
}

public class AccountRegistryManagementRepository : AccountRegistryReadRepository, IAccountRegistryManagementRepository
{
    public Task<IEnumerable<TChildResource>> CreateChildResourceMetadataAsync<TChildResource>(CancellationToken cancellationToken, params IChildResource[] childern)
        where TChildResource : IChildResource
    {
        /*
         * Create multiple childServicws using CreateMultipleDocumentsStoredProcedure.  
         * Current usage is to create IomtConnector with ISecretMetadata childern.
         */
    }

    public Task<TChildResource> UpsertChildResourceMetadataAsync<TChildResource>(IChildResource child, CancellationToken cancellationToken)
        where TChildResource : IChildResource
    {
        /*
         * Create or Update childResource document by id in subscription partition.
         */
    }


    public Task<TChildResource> DeleteChildResourceMetadataAsync<TChildResource>(IChildResource child, CancellationToken cancellationToken)
        where TChildResource : IChildResource
    {
        /*
         *  Delete childResource document by id in subscription partition.
         */
    }
}


```

### Processing

As part of the changes to the OperationDocument modifications are needed in the ProvisioningOrchestrator to handle the new operation document types.  The proposal is to introduce a chain of responsibility with multiple handlers.  Instead of a switch statement the ProvisioningOrchestrator will pass the base operation to the chain of responsibility.  It will be the job of each handler in the chain to evaluate the BaseOperation provided and determine if it is a request it should handle.  If the handler determines it will handle the request it will return a BaseCommand the ProvisioningOrchestrator to invoke.  If the handler cannot handle the request it will return null and the next handler in the chain will be invoked.  In this scenario a new handler will be created for the current logic contained in the switch statement of ProvisioningOrchestrator.ExecuteProvisioning responsible for FHIR Service provisioning requests.  A new handler for IoMT Connector provisioning requests will be created and added to the chain as well.  Finally a handler will exist at the end of the chain responsible for throwing an OperationNotFoundException if the request gets to the end of the chain and no handler has handled the operation.

As future operations are added new handlers can be added to the chain to process them.

## Test Strategy

Unit Tests and E2E tests.  E2E tests will be added to the new entities being created similar to the existing E2E tests that exist for the current entities and repository methods.

## Change Log

Summary of changes made and when between reviews.

### 2020-02-18

* Renamed OperationDocument to FhirAccountOperationDocument to clarify it's specific intent.
* Renamed BaseOperationDocument to OperationDocument for the common base of all operations.
* Updated IChildResource property names.  ParentResourceTypeName and ChildResourceType are no longer used, instead the actual Type property is used.
* Add extension method for setting IChildResource SearchIndexes.
* Type of the IomtConnector no longer concatenates the parent id with the type. Confirmed ARM will specific name as 'ParentName\ChildName' so name will already include the parent which will be enough to achieve the desired uniqueness.
* Updated repository design to use Type instead of ResourceTypeName.

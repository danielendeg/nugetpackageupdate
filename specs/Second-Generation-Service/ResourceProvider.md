# Gen2 Workspace Resource Provider and Storage Design

The following document describes the design for the Gen2 Workspace Resource Provider and storage entities.  As we design the Resource Provider for Gen2 there are several goals we hope to achieve:

* Prevent or reduce the need to copy and transform objects between Resource Provider, storage, and operation entities.
* Provide a common pattern for existing and new services to easily leverage and onboard
* Provide isolation and limit dependencies on Gen1 components.

[[TOC]]

## Project Structure

For Gen2, new standalone libraries will be added for Storage and Resource Provider code.  They will be self contained and have no direct dependencies on the existing libraries (ARMResourceProvider.Service & ResourceProvider.Storage).  The goal is enable a clean break and isolation between Gen2 and Gen1.  Some fundamental interfaces and concepts will exist in both (```IChildResource``` and ```IResourceIdentity``` as examples) but will be in separate namespaces and allow the concepts to evolve independently.

There may be a need to share common code, particular in ResourceProvider.Storage related to repositories and serialization. In order to avoid duplication of code shared between ResourceProvider.Storage & ResourceProvider.Storage.Workspace will be moved into ResourceProvider.Storage.Common.

The list of new projects (not including test projects) is:

* ARMResourceProvider.Service.Workspace
* ResourceProvider.Storage.Common
* ResourceProvider.Storage.Workspace

The new ARMResourceProvider.Service.Workspace & ResourceProvider.Storage.Workspace libraries will be consumed by the existing ARMResourceProvider.Service.

Fluent style extension methods will exist as part of the ARMResourceProvider.Service.Workspace library to enable dependency injection of the new Workspace resource provider handlers into the existing ARMResourceProvider.Service project that will be host them for the time being.

## Storage Persistence

The Gen2 Workspace entities will use the same global database (CosmosDB) as Gen1.  By reusing the same global database we don't need to provision additional resources and assume the cost associated with them.  

Gen2 Workspace entities will be stored in the same collection.  They will be differentiated with new ```Type``` values.  The same collection will be used so the Gen2 repository only needs to reference one collection. Using a different collection is problematic because subscription registration already exists in the collection used for Gen1.

## Storage Entities

Each publicly defined Resource Type in the manifest will have it's own storage entity.  This is a departure from the current design, specifically with IoMT resources.  The current Gen1 design for IoMT has one storage entity but several API entities (IoMT Connector, IoMT Mapping, IoMT Connector) projected from the storage entity. This projection adds extract complexity to the resource provider that isn't necessary and can be eliminated with an 1:1 storage to ARM resource mapping.

Operation documents will also be wrappers for the underlying storage entity for the service.  The content inside the wrapper will represent the new state that is to be applied to the respective storage entity.  This should simplify operation creation and usage.  Instead of creating a brand new operation object and copying properties back and forth and operation will be created that wraps the new ```ResourceObject``` state.  The operation class will have the properties.

### Resources

Functionally the same as the existing AccountName class used to guarantee the Workspace name is unique. Additional information on Gen2 naming can be found [here](https://microsofthealth.visualstudio.com/Health/_git/health-paas-docs/pullrequest/13902).

```c#
    public class WorkspaceName : DocumentModel<string>
    {
        public const string WorkspaceTypeName = "workspacename";

        public WorkspaceName(string subscriptionId, string name)
        {
            Id = EnsureArg.IsNotNullOrEmpty(subscriptionId, nameof(subscriptionId));
            SubscriptionId = EnsureArg.IsNotNullOrEmpty(name, nameof(name));

            Id = name;
            SubscriptionId = subscriptionId;
        }

        [JsonConstructor]
        protected WorkspaceName()
        {
        }

        public string SubscriptionId { get; set; }

        public override string Type => WorkspaceTypeName;

        /// <summary>
        /// All WorkspaceNames must be in the same partition so the unique constraint
        /// on Name+Type can be utilized.
        /// </summary>
        public override string PartitionKey => WorkspaceTypeName;
    }
```

```IChildResource``` is a parred down version of the existing interface in the storage library added as part of the IoMT connector work. The properties are reduced to just the fully qualified parent name (with all segments) and the parent's resource type.

```c#
public interface IChildResource
    {
        /// <summary>
        /// Full name of the parent, example 'workspacename' or 'workspacename\dicomservicename'
        /// </summary>
        string ParentName { get; }

        /// <summary>
        /// Type of the parent, example 'Microsoft.HealthCareApis\workspaces'
        /// </summary>
        string ParentResourceTypeName { get; }
    }
```

```IResourceIdentity``` remains unchanged.

```c#
    public interface IResourceIdentity
    {
        string Name { get; set; }

        string SubscriptionId { get; set; }

        string ResourceGroupName { get; set; }

        string ProviderNamespace { get; set; }

        string ResourceTypeName { get; set; }
    }
```

```IChildResourceIdentity``` combines the ```IResourceIdentity```  with the ```IChildResource``` interface.  The combination is very similar to the current ```IChildResource```.  The difference between ```IChildResource``` and ```IChildResource``` in Workspaces allows us to define child resources that may not be externally exposed through our APIs.

```c#
    public interface IChildResourceIdentity :
        IChildResource,
        IResourceIdentity
    {
    }
```

The ```IApiResource``` defines a standard way for updating and retrieving user defined properties back and forth from the ```IResourceEntity``` to the underlying storage entity.

```IApiResource```

```c#
    public interface IApiResource
    {
        void SetApiProperties(JToken properties);

        JToken GetApiProperties();
    }
```

```ResourceObject``` is the common object for representing all Resources (Workspace, DICOM, IoMT, FHIR).  The ```IApiResource``` interface is for setting and retrieving the Properties JToken for settings exposed on the customer for configuration (example ```FhirAccountProperties```).

The below example doesn't contain all necessary properties (location for example is missing.)  The example is skeleton to illustrate the concept.

```c#
   public abstract class ResourceObject :
        IApiResource,
        IResourceIdentity
    {
        public string Name { get; set; }

        public string SubscriptionId { get; set; }

        public string ResourceGroupName { get; set; }

        public string ProviderNamespace { get; set; }

        public string ResourceTypeName { get; set; }

        public ResourceState ResourceState { get; set; }

        public ResourceState? LastTerminalProvisioningState { get; set; }

        public DateTime ChangedTime { get; set; }

        public DateTime CreatedTime { get; set; }

        public IDictionary<string, string> Tags { get; set; }

        public abstract JToken GetApiProperties();

        public abstract void SetApiProperties(JToken properties);
    }
```

An example ```WorkspaceResource```.

```c#
    public class WorkspaceResource : ResourceObject
    {
        public override JToken GetApiProperties()
        {
            // Placeholder until we define workspace properties like SKU
            return null;
        }

        public override void SetApiProperties(JToken properties)
        {
            // Placeholder until we define workspace properties
        }
    }
```

An example using the existing FHIR service for comparison between Gen1 and Gen2.

```c#
    public class FhirServiceResource : ChildResourceObject
    {
        public FhirAccountProperties FhirAccountProperties { get; set; } = new FhirAccountProperties();

        public override JToken GetApiProperties()
        {
            return JToken.FromObject(FhirAccountProperties);
        }

        public override void SetApiProperties(JToken properties)
        {
            FhirAccountProperties = properties.ToObject<FhirAccountProperties>();
        }
    }
```

For persistence, ```ResourceObject``` is missing several properties we store with our document today.  The proposal is to use a ResourceDocumentModel that wraps the underlying ```ResourceObject```.  The ```ResourceObject``` is accessible via a ```Content``` property. Required properties like PartitionKey & SearchIndex are extracted from the Content.  Additional properties like the internal id will exist as part of the wrapper object; ```ResourceDocumentModel<TResource>```.

```c#
    /// <summary>
    /// Wraps and contains a Resource for storage.
    /// </summary>
    /// <typeparam name="TResource">Type of Resource to wrap</typeparam>
    public class ResourceDocumentModel<TResource> : DocumentModel<string>
        where TResource : IResourceIdentity
    {
        public ResourceDocumentModel()
        {
            SearchIndex = new SearchIndexDictionary<ResourceDocumentModel<TResource>>(
                                this,
                                p => p.Content.Name,
                                p => p.Content.ResourceGroupName,
                                p => p.Content.ResourceTypeName,
                                p => p.Content.ProviderNamespace);
        }

        [JsonProperty("content")]
        public TResource Content { get; set; }

        public override string PartitionKey => Content?.SubscriptionId;

        [JsonProperty("name")]
        public virtual string Name => Content.Name;

        // Use the ResourceTypeName as the Type constraint for storage for consistenty.
        [JsonProperty("type")]
        public virtual string Type => Content.ResourceTypeName;

        [JsonProperty("searchIndex")]

        protected IReadOnlyDictionary<string, string> SearchIndex { get; }
    }
```

## Operations

```OperationDocument``` will be updated for Gen2 workspaces.  A new ```WorkspaceOperation``` will define Gen2 operations.  It will wrap the ```ResourceObject``` that represents the desired new state for the resource the operation references.  By wrapping the desired state the operation document can focus on properties that are specific to managing the lifecycle and need to be returned as part of the ```IOperationEntity``` interface.

```c#
   public class WorkspaceOperation<TResource> : DocumentModel<string>
        where TResource : ResourceObject
    {
        public const string DocumentType = "workspaceoperation";

        public WorkspaceOperation()
        {
            SearchIndex = new SearchIndexDictionary<WorkspaceOperation<TResource>>(
                this,
                p => p.SubscriptionId,
                p => p.ResourceState.ResourceGroupName,
                p => p.ResourceState.Name,
                p => p.ResourceState.ProviderNamespace);
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
            /// Request to update a subset of the resource's properties
            /// </summary>
            Patch = 2,
        }

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

        [JsonProperty("type")]
        public virtual string Type => DocumentType;

        [JsonProperty("resourceState")]
        public TResource ResourceState { get; set; }

        /// <summary>
        /// Used to determine the type of operation to create on deserialization.
        /// </summary>
        [JsonIgnore]
        public string OperationResourceTypeName { get => ResourceState?.ResourceTypeName; }

        [JsonProperty("provisioningOperation")]
        public OperationType ProvisioningOperation { get; set; }

        [JsonProperty("provisioningStatus")]
        public Status ProvisioningStatus { get; set; }

        [JsonProperty("location")]
        public string Location { get; set; }

        [JsonProperty("timeEnqueued")]
        public DateTime TimeEnqueued { get; set; }

        [JsonProperty("timeLastUpdated")]
        public DateTime TimeLastUpdated { get; set; }

        [JsonProperty("timeEnded")]
        public DateTime? TimeEnded { get; set; }

        [JsonProperty("attemptCount")]
        public int AttemptCount { get; set; }

        [JsonIgnore]
        public int MaxAttempts { get; set; }

        [JsonProperty("exceptionDetails")]
        public string ExceptionDetails { get; set; }

        [JsonProperty("percentComplete")]
        public double? PercentComplete { get; set; }

        public override string PartitionKey => SubscriptionId;

        [JsonProperty("searchIndex")]
        protected IReadOnlyDictionary<string, string> SearchIndex { get; }

        [JsonProperty("subscriptionId")]
        public string SubscriptionId { get; set; }

        public bool IsTerminalStatus()
        {
            return ProvisioningStatus == Status.Canceled || ProvisioningStatus == Status.Completed || ProvisioningStatus == Status.Failed;
        }
    }
```

Two operation data handlers, Gen1 and Gen2 will exist.  The will be routed to the correct ```DefaultOperationResultsResourceTypeHandler``` based on the API version. The Gen2 API version will route to the new Workspace OperationHandler.  This means versions of our operation API will be specific to Gen1 or Gen2.

## Repositories

A new repository will be added for Workspace related resources.  Below are example operations that will be supported.  The goal is a repository that can support any resource in the Workspace hierarchy.

Some shared storage components (interfaces, stored procedures, etc.) may need to be refactored into a common library so they can be easily shared between the Workspace Storage library and the existing ResourceProvider.Storage library.

```c#
    public interface IWorkspaceRepository
    {
        Task<ResourceDocumentModel<TResource>> GetResourceAsync<TResource>(string subscriptionId, string resourceGroupName, string resourceName, string resourceTypeName)
            where TResource : ResourceObject;

        Task<ResourceDocumentModel<TResource>> GetResourceAsync<TResource>(IResourceIdentity resourceIdentity)
            where TResource : ResourceObject;

        Task<PagedResult<IEnumerable<ResourceDocumentModel<TResource>>>> FindResourcesByTypeAsync<TResource>(string subscriptionId, string providerNamespace, string resourceTypeName, CancellationToken cancellationToken, string resourceGroupName = null, int? top = null, string continuationToken = null)
            where TResource : ResourceObject;

        Task<ResourceDocumentModel<TResource>> InsertResourceAsync<TResource>(ResourceDocumentModel<TResource> resource, CancellationToken cancellationToken, int? quota = null)
            where TResource : ResourceObject;

        Task<ResourceDocumentModel<TResource>> UpdateResourceAsync<TResource>(ResourceDocumentModel<TResource> resource, CancellationToken cancellationToken)
            where TResource : ResourceObject;

        Task<ResourceDocumentModel<TResource>> UpsertResourceAsync<TResource>(ResourceDocumentModel<TResource> resource, CancellationToken cancellationToken)
            where TResource : ResourceObject;

        Task<ResourceDocumentModel<TResource>> DeleteResourceAsync<TResource>(ResourceDocumentModel<TResource> resource, CancellationToken cancellationToken)
            where TResource : ResourceObject;

        Task<PagedResult<IEnumerable<ResourceDocumentModel<IChildResourceIdentity>>>> GetChildernResourcesAsync(string subscriptionId, string parentResourceGroupName, string resourceName, string parentResourceTypeName, CancellationToken cancellationToken, string continuationToken = null, int maxPageSize = 10);

        Task<PagedResult<IEnumerable<ResourceDocumentModel<IChildResourceIdentity>>>> GetChildernResourcesAsync(IResourceIdentity parentResourceIdentity, string continuationToken = null, int maxPageSize = 10);

        IDistributedLock CreateDistributedLockObject(string lockId);
    }
```

## Resource Provider Entities

### IResourceEntity

On the resource provider side an object, ```ResourceEntity```, implementing ```IResourceEntity``` (the RP contract for resources) would wrap the ```ResourceDocumentModel```.  This would allow the ```ResourceDocumentModel``` and the ResourceObject in the context to be passed around the Resource Provider workflow without needing to convert back and forth between objects.

The open question is how will the properties on the IResourceEntity be resolved.  One option is to pass through the getter/setters to the underlying ```ResourceObject```.  This works well for simple objects.  It breaks when the objects are complex and need to be translated.

Instead synchronization methods on the ```ResourceEntity``` will be used to update the wrapped ```ResourceObject``` with the state provided in the ```ResourceEntity```. This avoids the issue with through properties noted above.

```c#
    public class ResourceEntity<TResource> : IResourceEntity
        where TResource : ResourceObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResourceEntity{TResource}"/> class.
        /// Create a ResourceEntity from an existing ResourceDocument. Used when resource already exists.
        /// </summary>
        /// <param name="resourceDocument">ResourceDocument to generate the entity from.</param>
        public ResourceEntity(ResourceDocumentModel<TResource> resourceDocument)
        {
            ResourceDocument = EnsureArg.IsNotNull(resourceDocument, nameof(resourceDocument));
            SyncFromDocument();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResourceEntity{TResource}"/> class.
        /// Create a new ResourceEntity from an ARM ResourceDefinition. A new ResourceDocument is also created.
        /// </summary>
        /// <param name="resourceDefinition">ResourceDefinition used to defined the ResourceEntity.</param>
        public ResourceEntity(ResourceDefinition<GroupWideResourceIdentifier> resourceDefinition)
        {
            // TODO: Set Entity propeties from the resource definition.
            EnsureArg.IsNotNull(resourceDefinition);

            ResourceDocument = new ResourceDocumentModel<TResource>
            {
                Id = Base36IdGenerator.Create(),
            };

            SyncToDocument();
        }

        /// <summary>
        /// Reference to underlying Resource.
        /// </summary>
        public ResourceDocumentModel<TResource> ResourceDocument { get; }

        #region IResourceEntity Properties

        public GroupWideResourceIdentifier Id { get; set; }

        public string Location { get; set; }

        public ResourcePlan Plan { get; set; }

        public ResourceSku Sku { get; set; }

        public string Kind { get; set; }

        public JToken Properties { get; set; } = new JObject();

        public ResourceTagsDictionary Tags { get; set; }

        public JToken InternalResourceState { get; set; } = new JObject();

        public ProvisioningState ProvisioningState { get; set; }

        public ProvisioningState LastTerminalProvisioningState { get; set; }

        public IReadOnlyList<string> Zones { get; set; }

        public ResourceIdentity Identity { get; set; }

        public ResourceSystemData SystemData { get; set; }

        public string DisplayName { get; set; }

        public string EntityTag { get; set; }

        public DateTime ChangedTime { get; set; }

        public DateTime CreatedTime { get; set; }

        #endregion

        /// <summary>
        /// Updates Entity from the underlying ResourceDocument properties
        /// </summary>
        public ResourceEntity<TResource> SyncFromDocument()
        {
            Properties = ResourceDocument.Content.GetApiProperties();

            // TODO: Sync remaining properties from document to entity

            return this;
        }

        /// <summary>
        /// Updates ResourceDocument properties from the underlying ResourceDocument properties
        /// </summary>
        public ResourceDocumentModel<TResource> SyncToDocument()
        {
            ResourceDocument.Content.SetApiProperties(Properties);

            // TODO: Sync remaining properties from entity to document

            return ResourceDocument;
        }
    }
```

### IOperationResultEntity

The implementation of ```IOperationResultEntity``` would be similar to the ```IResourceEntity``` with an OperationResultEntity that wraps ```ResourceObject```.  State is stored in the ```ResourceObject``` and the rest of the properties should be common to translate between the ```WorkspaceOperationDocument``` and the ```OperationResultEntity```.

## Resource Provider Worker

The Resource Provider Worker would work the same as today except with some minor modifications.

* There will be a separate instance responsible for dequeuing and working on Workspace operations.  This will allow Gen1 and Gen2 operations to work independently.
* The base structure will be the same but the Gen1 & Gen2 will work on different operation types **workspaceprovisioningrequest** vs **provisioningrequest**.
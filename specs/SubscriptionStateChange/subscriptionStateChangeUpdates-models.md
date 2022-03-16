# Proposed document models

See [Subscription State Change Updates](./subscriptionStateChangeUpdates.md) doc for more context

1) Add `StateChange` as an OperationType.

(Question: Sounds like the other operation types are usually triggered by customer request, and also specific to one resource. Does it make sense to add StateChange here? We could also pull `OperationType` out of `IOperation` and have `IResourceOperation` and `ISubscriptionOperation` each have their own kind of operation types.)

```
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

        /// <summary>
        /// Operation is a reaction to the change of an entity's state (ex: subscription)
        /// </summary>
        StateChange = 3,
    }
```

2) Create `ISubscriptionOperation` for all operations targeted towards a set of resources in a subscription. 

(Question: Do we anticipate more of these kinds of operations? I assumed yes, but we could change this to be more generic if not)

```
    public interface ISubscriptionOperation : IOperation
    {
        public ISubscriptionIdentity TargetSubscription { get; set; }

        public IEnumerable<IResourceOperationIdentity> ResourceOperations { get; set; }
    }

    public interface ISubscriptionIdentity
    {
        public string SubscriptionId { get; set; }

        public string ProviderNamespace { get; set; }
    }

    public interface IResourceOperationIdentity
    {
        public IResourceIdentity TargetResource { get; set; }

        public string OperationId { get; set; }

        public OperationStatus LastObservedOperationStatus { get; set; }
    }

    public class SubscriptionOperation : TypedDocumentModel<string>, ISubscriptionOperation
    {
        public const string DocumentType = "subscriptionoperation";

        public OperationType OperationType { get; set; }

        public OperationStatus OperationStatus { get; set; }

        public string Location { get; set; }

        public DateTime TimeEnqueued { get; set; }

        public DateTime TimeLastUpdated { get; set; }

        public DateTime? TimeEnded { get; set; }

        public string LoggingParentId { get; set; }

        public string LoggingRootId { get; set; }

        public int AttemptCount { get; set; }

        public int MaxAttempts { get; set; }

        public string ExceptionDetails { get; set; }

        public double? PercentComplete { get; set; }

        public ISubscriptionIdentity TargetSubscription { get; set; }

        public string SubscriptionId => TargetSubscription.SubscriptionId;

        public string ProviderNamespace => TargetSubscription.ProviderNamespace;

        public override string Type => DocumentType;

        public IEnumerable<IResourceOperationIdentity> ResourceOperations { get; set; }

        public bool IsTerminalStatus()
        {
            switch (OperationStatus)
            {
                case OperationStatus.Canceled:
                case OperationStatus.Completed:
                case OperationStatus.Failed:
                    return true;
                default:
                    return false;
            }
        }
    }
```

3) `SubscriptionStateChangeResourceOperation` -> `SubscriptionStateChangeOperation`. Inherits from `SubscriptionOperation`.

```
    public class SubscriptionStateChangeOperation : SubscriptionOperation
    {
        [JsonProperty("subscriptionState")]
        public SubscriptionState SubscriptionState { get; set; }
    }
```

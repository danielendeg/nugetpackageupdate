# Compute auto scaling (Gen1)

Auto scaling of compute layer is required for efficient handling of variable load on Fhir servers. We already have service fabric nodes scaling configured (based on node level resource consumption), so this document talks about service instance (per account) level scaling only. Moreover, Gen1 is the primary scenario considered here since Jupiter might need some discussion/alignment across different services.

## Goals / Design Principles
- Deploy -> Monitor -> Learn -> Update -> Deploy -> ...
- Low DRI overhead
- Ability to turn off/on or tweak scaling limits for any specific account
- Low dev cost

## Non-Goals
- Throttling improvement
- Resource governance per service instance
- Reduce service instance count to below 2 (something for future)
- Predictive auto scaling [waiting for Steve's AI class for this :)]

## Proposal
Service fabric supports [auto scaling](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-cluster-resource-manager-autoscaling) based on configured trigger (when) and mechanism (how). Scaling policy can be applied as a simple update to an existing service (requires no reprovisioing).

```
AveragePartitionLoadScalingTrigger trigger = new AveragePartitionLoadScalingTrigger();
trigger.MetricName = "servicefabric:/_CpuCores"; // cpu usage available after enabling resource monitor service; custom metrics also supported
trigger.ScaleInterval = TimeSpan.FromMinutes(2); // how often the trigger will be checked
trigger.LowerLoadThreshold = 0.4; // If the average load of all instances of the partitions is lower than this value, then the service will be scaled in;
trigger.UpperLoadThreshold = 1.0; // If the average load of all instances of the partition is higher than this value, then the service will be scaled out;

PartitionInstanceCountScaleMechanism mechanism = new PartitionInstanceCountScaleMechanism();
mechanism.MinInstanceCount = FhirService_InstanceCount; // default = 2
mechanism.MaxInstanceCount = Math.Max(mechanism.MinInstanceCount, GetCurrentCosmosDBThroughput() / CosmosDBThroughputPerComputeInstance); // linking scale limit to persistence layer cost.
mechanism.ScaleIncrement = 1; // how many instances can be added or removed per trigger
```

Concurrent request count (currently 15) per instance will be constant while scaling the number of instances.
For CosmosDBThroughputPerComputeInstance, we can start with say 4K (for reference, recently Cigna configured 300K RUs with 40 instances consuming ~1.2 cores at peak in a cluster of 80 VMs) and tune it later if needed.

As safeguards, FhirService_InstanceCount and CosmosDBThroughputPerComputeInstance can be overridden per account/subscription to control min/max instance count for any specific usage patterns.
CosmosDBThroughputPerComputeInstance=int.MaxValue should disable the auto scaling.

RP worker will apply the scaling policy,

- as a periodic task (say every 12 hours) on all accounts in the cluster (to ensure max instance count is proportional to current cosmos DB throughput which keeps increasing based on data size)
- and during service fabric application provisioning (to ensure immediate effect for any overridden value)


### Limitations
- Burst traffic scenario: Only upto ScaleIncrement instances can be added per ScaleInterval time period (assuming enough cluster capacity), so we might want to reserve more instances (by FhirService_InstanceCount=N) for accounts sensitive to burst load latency/failure rate.
- Instance count bound by available node count: Service fabric currently doesn't allow multiple instances of a stateless (single partition) service on same node. If required instance count is more than node count, application can go to warning state which can get resolved when one of the below condition is met. However, warning state should not (need to confirm) cause any operational (upgrade etc.) issue for the application/cluster.
    - when requried instance count drops below node count
    - or load on the node goes beyond threshold causing node scaling to kick in (adding VMs to the cluster). Right now, even a single account load can cause node scaling due to lack of resource governance.

## Metrics
- Scaling policy update periodic task iteration latency
- Scaling policy update failure rate
- Service instance counts

## Testing strategy
- Unit tests
- Acceptance: We should see a reduction in 429s and latency as scaling happens, though quantification of the impact at various load levels will be done as part of perf test system. Moreover, we should monitor the scaling for few of our large scale customers and tune the configs if needed.

### Open issues / other considerations
- During scale in, are requests drained gracefully?
- why not API reuqest count as trigger metric? : our infrastructure cost is more aligned with CPU consumption than with API call count (since actual load on service can vary for each request).
- Limit scaling based on throughput vs data size : Data size can be used for SQL backend too, but it might not be a good indicator for all usage scenarios (read heavy vs write heavy).
- Jupiter auto scaling limit : Since API request count is one of the billing meter, it might be okay to not have database metric based upper bound (keep scaling based on cpu consumption).

## References
- [PM spec](https://microsofthealth.visualstudio.com/Health/_git/health-paas-docs/pullrequest/15244?path=%2Fspecs%2FFHIR%2FAutoscaling%2Fautoscaling-gen1.md) by Benjamin
- [Initial investigation](https://microsoft.sharepoint.com/teams/msh/_layouts/15/Doc.aspx?sourcedoc={88c3d919-efdc-4b77-9910-b7e1f892113d}&action=edit&wd=target%28Features%2FSpecs.one%7C76b377e8-bfa9-4cd5-8f21-9705e022ec7f%2FService%20Fabric%20Autoscaling%20One-Pager%7C9cbb7b7b-4e72-4e11-8800-01d5d8315652%2F%29) by Meleese
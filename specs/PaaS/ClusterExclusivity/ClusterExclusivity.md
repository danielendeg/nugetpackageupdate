*Summary of the feature.*

[[_TOC_]]

# Business Justification

Create safe clusters to run testing in production. Currently there are a couple of underlying services not offered in Azure Dogfood(Private Link, Azure Storage) . This requires us to be able to test these features in production. Finishing this feature will enable us to provide a barrier between test in production clusters and produciton cluster. 

# Scenarios

1. Test changes in Private Link
1. Test changes in managed identity export
1. Test changes in deidentified export. 

# Metrics

We need to track number of operation documents from cluster group/region x picked up by RP worker in cluster group/region y. This can help to get data on cross cluster or region document pickup

# Design

In current desgin, RP worker can pickup operation document for another region if document is not acted upon in 10 minutes of creation. With exclusivity we will introduce a concept of cluster group id. Clusters within same group can pick up operation documents for each other. Clusters can not pick up operation documents from other than their own. 

On the flip side, each operation document will contain what cluster and cluster group they are targeted for. This way, each operation  and cluster is clearly marked with what group they belong to. 

Caution: In production, please make sure no production cluster should be by itself in a group. This will BREAK BCDR, since our BCDR process depends on creating an operation document to move the application from one cluster to another. 

Following changes will be made to cluster doc. 

Each cluster in clusters property in <code>environment.json </code> will contain a new property called "clusterGroupId". This is a free text field. This property should be writtin into cluster metadata document in global cosmosdb. This will require a change in <code>Deploy-Cluster.ps1</code> script. 

![](media\clusterGroupId.png)

![](media\Deploy-Clusterchange.png)

In order to insert cluster group Id in the operation document, modify <code>InsertOperationForResourceEntity</code> function to query cluster metadata document and find the correct group id. Each operation document shall contain cluster group id. 

In order for clusters to pick up operation documents only for their own cluster group, modify <code>ListPendingOperations</code> and <code>DequeueOperation</code> functions in <code>OperationRepository.cs</code> to include that cluster's group id. The <code>OperationRepository.cs</code> should lookup cluster metadata and make this determination.

During the initial roll out, there will be a period of time when some clusters are writing operation documents with group id while other clusters will not be aware of cluster group id. In order to mitigate a scenario where a cluster has not received the code update and needs BCDR (due to bad cluster, heavey traffic) code change will be gradual. For <code>ListPendingOperations</code> function in the first release will check to see if the property exist then it will enforce the cluster group id rule and worker timeout rule, and if the property doesn't exist then it will apply worker timeout.

Both of the test in production clusters, EUAP and TIP will receive the update at the very beginning of the release cycle, that way none of the test cluster will pick up other clusters' operation document. 


# Test Strategy

Appropriate unit test, integration and functional test cases will be added. 

# Security

*Describe any special security implications or security testing needed.*

# Other



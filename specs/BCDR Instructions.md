This document contains instructions on how to failover an account from one region to another in case a cluster has gone offline or bad for any reason. 

[[_TOC_]]

# Business Justification
To maintain availability of our FHIR accounts, it is imparative that we have the capability to move accounts from one region to another. In the past, we have encountered at least two incidents where entire region's compute went down. One was an Azure outage while in the other it was our Service Fabric cluster that was nonfucational for almost 24 hours. In both cases, our customer accounts availability was affected. Fortunately, we didn't receive any complaints in either of the outages, however it doesn't gurantee that in future that will be the case. Given the nature of the data we hold it is imparative that capability to move accounts between regions or clusters. 

# Scenarios

Our current BCDR capability is limited to dealing with situations where either compute or network is down. If underlying <code>CosmosDB</code> is experiencing outage, then there is almost nothing we can do to alleviate that. 

These instructions can be used to mitigate any outage where underlying <code>Cosmos DB</code> service is intact and available. 

Following business aspects should be considered before failing over customer's account. 

1. Customers provisioned their accounts in a specific region for a reason. If we were to move their accounts to another region, we need to obtain permission from the cutomers before moving them. 
1. When we move the customer accounts, we should consider moving these accounts to closest physical region to avoid extra latency. 
1. Accounts shouln't be moved out of jurisdiction if original region was subject to data sovereignty laws. 
 
# How to failover an account from one region to another 

* Setup your non saw PC to connect with production by following instructions on [OneNote](https://microsoft.sharepoint.com/teams/msh/_layouts/15/Doc.aspx?sourcedoc={88c3d919-efdc-4b77-9910-b7e1f892113d}&action=edit&wd=target%28Operations%2FHow%20to.one%7Cd4d99cd6-8fb8-492b-ade2-f4fccdcdd552%2FDraft%20Execute%20MultiTool%20on%20Prod%20From%20non-SAW%7Cb7d9ff2a-1dd7-416b-a067-8e2802b4a5c2%2F%29)
* Make sure you have obtained permission from customer to move their account. 
* Go to the cluters document in <code>Global Cosmos DB</code>. You can find the clusters document by running following query. 
```json
SELECT * FROM c
where c.id = 'clusters'
````

 * Find the cluster that is down in the document and add folllowing property in the correct cluster. 
Following is an example of Australia East cluster being marked offline. Dont forget the , at the end. :) 
```json
{
    "partitionKey": "clusters",
    "id": "clusters",
    "type": "clusters",
    "clusters": [
        {
            "regionName": "Australia East",
            "state" : "Offline",
            "applicationTypeLkg": {
                "FhirApplicationType": {
                    "ApplicationTypeName": "FhirApplicationType",
                    "ApplicationTypeVersion": "master.20200708.5.HASH.ey2zxcs1tglzyopaf35vgdxeschajc1q8dqyafbrgioe1i64t"
                },
                "FhirApplicationType.R4": {
                    "ApplicationTypeName": "FhirApplicationType.R4",
                    "ApplicationTypeVersion": "master.20200708.5.HASH.2i45l8au4p60nsudfpjahwi8sz055f5fv9nuo62g146rr5u55a"
                }
            },
```

* Get Id of the account you want to failover. 
* Get ShortId of the cluster that you want to failover to. 
* Execute following command <code>Reprovision --accountId 'accountId' --failover --failoverCluster 'targetCluster' </code> 
* Wait for the tool to finish. 
* Once the tool is finished, verify the account is available using healthcheck endpoint. 
* When the original cluster is back up and running, make sure to mark the cluster <code>Online</code> by modifying
```json
"state" : "Online",
```
* When the orginal cluster is back up and running, you can execute the same command in step 5 to move the account back to the original cluster. 

Notes:
* Tool is resilient to original cluster being out. It will move the account even if the original cluster is down. 
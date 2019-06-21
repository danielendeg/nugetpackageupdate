# Geo expansion plan for FHIR

As Azure API for FHIR goes to GA, it will require to get deployed in multiple regions to meet Azure ring 0 and ring 1 obligations. Our API will also need to expand to other regions as new customers will onboard. This document describes engineering team's approach to how to expand this service to new regions.  

[[_TOC_]]

# Business Justification

In order to provide our customers with geo located FHIR service, we will need to expand the service to multiple regions. Azure also requires our services be deployed to multiple data centers to qualify as Ring 1 and Ring 0 service. However at the moment, our API doesn’t have customers in few regions where Azure requires deployment. Due to this, there is a need to optimize our deployment stamps to avoid extra costs for running the stamp in such regions. 

# Scenarios

Following are the scenarios that we want to support as part of geo expansion. 

* As an Azure FHIR API administrator I can:
    - Deploy to new region with minimal cluster stamp size. 
    - Scale up/out regional cluster as customer demand increases. 
    - Scale down in regions where cluster is over provisioned. 

# Stamp sizes
Following are stamp sizes we will utilize in our geo expansion plan. For both of the stamp sizes, we should set auto scaling policy on VMSS for scaling out and set durability to silver. Since the durability characteristic will be set to silver, cluster should be able to scale in on its own without manual intervention. 

- Minimum stamp
- Standard stamp

Both stamp sizes are simply a way of standardizing definition of the stamps. This is not exhaustive size of stamps. In some regions we may need larger than standard size stamp. As and when we define those new stamp sizes, new stamp sizes will be added. 

## Minimum stamp
Minimum stamp is bare minimum that is needed to run the Azure API for FHIR. In this stamp size following is the configuration. 
- Front end node X 5 
- Back end node X 5
- Management node X 5

In this stamp all the VMs are F2 size. Durability level of the clusters should be Silver. 

These are production services; we need to maintain basic compute redundancy and fault tolerance within region. Also, to support upgrades in production we can’t have single node clusters. Due to these reasons, I selected 5 nodes for each application.  

## Standard stamp size
For standard stamp following is the configuration. 

- Front end node X 5
- Back end node x 15
- Management node X 5

In this stamp size all of the VMs are F4. Durability level of the clusters should be Silver. 

During the expansion, we may find that F2 and F4 arent available in some of the region. For those regions, we may choose a different size or work with Azure Compute to make those sizes available in the region. F2 and F4 arent available only in following regions. 
- France South
- Australia Central
- Australia Central 2
- UAE Central
- South Africa West

# Geo expansion plan. 

For each new region that we open, it will start with minimum stamp. As we start to add customer in those regions, we will switch stamp size to standard size, Azure's current recommendation is not to scale VMSS beyond 100 node, We should switch size preemptively at 80 nodes. Decision of when to switch between the stamp size will largely depend on the traffic received that cluster in the region. As a general suggestion, average CPU usage per minute approaches 80% for sustained periods of time (> 6 hours??) as we should consider switching the stamp size. 

In the beginning, we should also gradually add more VMs in the scale sets rather than jumping from minimum to standard stamp sizes.

It is recommended we dont change VM size on the fly. Instead we will need to introduce new VMSS with the new size and then scale in old VMSS. Instrucitons on how to do this will be contained in our SF guideline documents. VM size change for primary node (mgmt node in our case) isn't supported. 

## Regional expansion. 
Following regions will start with standard stamp size. 
- NCUS
- WUS2
- UK West

All other regions will start with minimum stamp size. 

# Durability  characteristic for our clusters. 

Durability tier is used to indicate to the system privileges that our VMs have with the underlying Azure infrastructure. For more information on durability tier see [durability characteristics of the cluster](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-cluster-capacity#the-durability-characteristics-of-the-cluster). For all of our clusters, durabilty should be set to minimum of Silver (requires at least 5 VMs of single node or above in each node type).

# Reliability characteristic for our clusters. 
The reliability tier is used to set the number of replicas of the system services that you want to run in this cluster on the primary node type. The more the number of replicas, the more reliable the system services are in your cluster. For more information see [The reliability characteristics of the cluster](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-cluster-capacity#the-reliability-characteristics-of-the-cluster). Reliability of our clusters should be set to silver. 

# Roll out changes/implementation 

In order to roll out each new region, following changes have to take place. 
- Add new cluster definition in [repo]\deployment\environment\prod.json   
e.g.  
<code>
 "wus2": {
            "location": "Canada Central",  
            "frontEndNodeCount": 5,  
            "backEndNodeCount": 5,  
            "managementNodeCount": 5,  
            "vmSize": "Standard_F4",  
            "enableAcceleratedNetworking": true  
        },
</coDe>
- Modify the release pipeline [Resolute_PaaS_Prod_New](https://microsofthealth.visualstudio.com/Health/_releaseDefinition?definitionId=67&_a=definition-tasks&environmentId=170) to add new stage to include new region. Please make sure to clone the existing stage, this will ensure correct triggers and approvers are copied from existing stage. 
- Update billing configuration in [repo]\deployment\environmentGroups\prod.json to include new region. Add new region and billing meter id.   
e.g.  
<code>
 "billing": {   
        "enabled": true,  
        "billedSubscriptionIds": [  
        ],  
        "billingStartTimeUtc": "02/7/2019 2:00:00 PM +00:00",  
        "locations": {  
            "ncus": {  
                "location": "northcentralus",  
                "cosmosDbMeters": [  
                    "65d4ded2-41ae-43a8-bb68-3c200e1ba864",  
                    "56f07b6a-c7d9-490f-a196-a7ee08e28712"  
                ]  
            },  
            "wus2": {  
                "location": "westus2",  
                "cosmosDbMeters": [  
                    "65d4ded2-41ae-43a8-bb68-3c200e1ba864",  
                    "56f07b6a-c7d9-490f-a196-a7ee08e28712"  
                ]  
            },  
            "ukw": {  
                "location": "ukwest",  
                "cosmosDbMeters": [  
                    "65d4ded2-41ae-43a8-bb68-3c200e1ba864",  
                    "56f07b6a-c7d9-490f-a196-a7ee08e28712"  
                ]  
            }  
        },  
        "billingPushAgent": {  
            "servicePrincipalName":   "30ad5fb8-2778-41a0-bec7-f5321be4a2a1"  
        }  
</code>
- Create new MDM account/metric for each region for shoebox. Right now this is manual work. 
- Update the prod ARM manifest with new endpoints and upload it using Geneva actions. 
- Register billing agnet in new region. 
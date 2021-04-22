# Dicom service azure requirements and best way to organize them in resource group

A generation 2 Azure healthcare RP has below Resource types

- Azure health data service (Microsoft.HealthcareApis)
    - Workspace
        - Dicom Service (Resource type), 1-n instances
        - Fhir Service (Resource type), 1-n instances

Today each instance of Azure API for FHIR creates a new resource group and uses subscription pooling when we hit the max capacity of a subscription.

We will look at dicom requirements and how we can optimize the capacity of a subscription for max dicom services. 

## Each Dicom Service Azure requirements

- Managed Identity 
- Storage account
- Key vault
- New DB in a SQL server elastic pool
- New dicom resource in AKS cluster
- Managed Identity role assigments
    - Role: StorageBlobDataContributor, Scope: Storage acct resource
    - Role: ManagedIdentityOperator, Scope: Resource group
- DNS cname record

## Current Resource grouping 

- Resources per Dicom server
    - Managed Identity
    - Storage account
    - Role assignments
- Shared resources
    - SQL server, DB
    - AKS cluster, Service
    - DNS zone, cname record

## Limits

| Resource | Limit | Unit  |
| -------- | ----- | ----- |
| Resource group | 980 | Per Subscription
| Deployments | 800 | Per Subscription Per Resource Group
| Resource | 800 | Per Resource type Per Resource Group
| Managed Identity | 500K | Per Tenant
| Key Vault | No limit |
| Storage Account | 250 | Per Region Per Subscription
| Traffic Manger|  200 | Profiles per subscription
| SQL   | 20-200 | Per Subscription
| SQL DB | 5000 | Per SQL server
| AKS | 1000 | Per Subscription
| AKS nodes | 1000 | 100 per node pool
| Role assignments | 2000 | Per subscription 

Notes:

- 980 limit on resource groups can be removed completely
    - 800 limit resource type per resource group can also be removed
    - 800 limit deployments per resource group **can not** be removed, old deployments need to be cleaned up
- Storage Account
    - Information on limits: https://docs.microsoft.com/en-us/azure/storage/common/scalability-targets-standard-account
- Traffic Manger
    - Has limit of 200 per subscription but can be increased by creating a ticket with the traffic manager team. FHIR subscriptions have increased to 1000 per subscription. 
    - Information on limits: https://docs.microsoft.com/en-us/azure/azure-resource-manager/management/azure-subscription-service-limits#traffic-manager-limits

- AKS
    - Information on limits: https://docs.microsoft.com/en-us/azure/aks/quotas-skus-regions

Monitoring:
- We will be monitoring the resources in the certain subscriptions to have alerts when we are reaching a limit. For information about which resources currently have monitoring and how alerting works view: [resource-monitoring](resource-monitoring.md)


## Options

### Option1: Resource group per dicom service

- 980 resource group limit will hit first

### Option 2: Resource group per workspace

- max 980 workspaces or max 1000 services would hit role assignment limit
- Can the role assignment limit be increased? If not we can try grouping all Managed Identities in a single RG and assign Managed Identity opertor role to that group. Will save 1 role assgm per service.
- New service can go to a new subscribtion.

### Option 3: One Resource group

- 800 resource per type per group will be hit. ManagedIdentityOperator assignment will happen once per workspace.

## Conclusion

-  Eventually we will hit the limits and need subscription pooling. Option 1 or Option 2 seems more favorable. 
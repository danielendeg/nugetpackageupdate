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
    - Keyvault
    - Role assignments
- Shared resources
    - SQL server, DB
    - AKS cluster, Service
    - DNS zone, cname record

## Limits

| Resource | Limit | Unit  |
| -------- | ----- | ----- |
| Resource group | 980 | Per Subscription
| Deployments | 800 | Per Subscription Per Location
| Resource | 800 | Per Resource type Per Resource Group
| Managed Identity | 500K | Per Tenant
| Key Vault | No limit |
| Storage Account | 250-350-5000 | Per Region Per Subscription
| SQL   | 20-200 | Per Subscription
| SQL DB | 5000 | Per SQL server
| AKS | 100 | Per Subscribtion
| AKS nodes | 1000 | 100 per node pool
| Role assignments | 2000 | Per subscribtion 

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
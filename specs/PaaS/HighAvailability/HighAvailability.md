# Providing High Availability option to our customers
## Description
Customers may opt to have their FHIR services run with high availability option due to business continuity and performance reasons. We would like to implement a system which would allow customers to choose High Availability as a configurable option for their service in Azure. 

[[_TOC_]]

## High level design
Primary way to provide high availability is to geo replicate customers data and have a traffic manager redirect customer requests to nearest available region where customers' data resides. 

There are three primary components for this design to work. 

- Storage
- Compute
- Traffic manager

### Storage 
Cosmos db provides an option to create instances with geo replication. If a customer chooses to opt in for the high availability option, geo replication should be turned on for their instances. Right now in our stack consistency level is set to strong consistency level.  Under the HA scenario storage will continue to have this level of consistency.  For this scenario to work, it will also require to enable automatic failover in our Cosmos DB instances. In essense, our Cosmod DB configuration will be multi region with single-write region. Reason for this configuration is, Cosmos DB does not handle conflicts. It just gives application list of conflicts and relies on application to resolve its own write conflicts. Since in our usecase we actually dont own the data, it is very hard to predict for us what is the right strategy to resolve these write conflicts as well.  

### Compute
When a customer selects the HA option, the instance should be deployed in two Azure paired regions. For more information on Azure Paired Regions, please see this [page](https://docs.microsoft.com/en-us/azure/best-practices-availability-paired-regions). At all times both of the cluster instances will stay up and ready to process any request. Depending on where the customer is picking the primary region, deployment of secondary compute should be chosen by the our mapping. Customer shouldn't be able to select which secondary region their instance gets deployed to. This will ensure instances are deployed in paired regions and not in just some random region.  

### Traffic Manager
Azure Traffic manager allows several [routing methods](https://docs.microsoft.com/en-us/azure/traffic-manager/traffic-manager-routing-methods). For this scenario, traffic manager should be configured based on priority/geographic values. 

## Costs for High Availability
For High Availability, there are a couple of cost considerations. 
- Cosmos DB
- Compute

For both of the components customer will incur extra costs. These costs should be passed on to the customer when selecting the HA option. 

## Current design considerations
In this spec, proposed configuration is active-active configuration. This option is by design more expensive, at the same time, this is also the simplest approach to reach high availability option. Current thinking is, we will start with this configuration and will continue to look into active-passive or active-reactive configuraiton. 

## Future considerations. 
Current design for HA is via active-active configuration. This configuration which is going to be more expensive. In the future, an active-passive configuration should be considered.

## Geo rollout
For detailed geo rollout plan see Resolute entry in [PAM tool](https://global.azure.com/product-availability/availability-by-offering/offering/1461). Overall strategy for geo rollout is our service will be offered under HA to all of the Hero regions except for Sweden, along with their Azure paired regions by GA (currently planned for September 2019). Please note some of the paired regions are not Hero regions (e.g. WUS). However in order to meet with HA requirements, deploying to hub regions will be necessary. 

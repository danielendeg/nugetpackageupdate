# Resource Provider, Global Database, and Single Data Residency

## Summary

Currently, per environment (Production, Test, Gov, etc) the Healthcare APIs infrastructure uses a [global database](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=/doc/project-resolute.md&_a=preview&anchor=cosmos-db-accounts) to store metadata about the services requested and provisioned by customers. This data is shared across all regions for the environment.  Some example data stored in the Global Database today includes:

* Subscription metadata
* Account Name metadata (Azure API for FHIR). Ensures account name chosen is globally unique.  Required since we derive service URLs based on the supplied name.
* Workspace Name metadata (Healthcare APIs). Ensures workspace name chosen is globally unique.  Required since we derive service URLs based on the supplied name.
* Resource metadata (Customer configuration and internal data needed for each service).
* Additional metadata, including secret rotation metadata.
* Provisioning Requests
* Completed Provisioning requests
* Resource Limit metadata
* Cluster and Service configurations

Some regions (EU, Brazil, Singapore) require data of certain classifications remain with in the region it was authored.  In a review of the data we store in the Global Database we found some of the documents.  We have until the end of 2021 to submit or plan to address the issue and we have till June 2022 to implement our solution.

## Proposal

* Separate the metadata stored in the global database into two groups, global and regional.  Global data will stay in existing Global database and will require data of certain classifications not be stored in those metadata records.  Other data will be migrated to regional databases.
* Classify if metadata is global or regional based on the type.  We have different repositories based on the type of document.  This should make it simple to split and update repositories for the different resources.
* Expand the idea of Cluster Groups.  Originally a logical concept added to regions for the Azure API for FHIR to prevent TIP and Canary RPs from working on other Production provisioning requests we would extend the concept into infrastructure.  Environments will now have cluster groups required and each region will be associated with one.  Each cluster group will also have infrastructure deployed and maintained like the CosmosDB to use for the group.  Resources created for these cluster groups will need HA/DR and other replication settings that align with the requirements for the region.
* Update operation processing logic for Legacy and Jupiter to use cluster groups. An operation that isn't picked up by it's original region can only be processed by an RP worker in the same group (instead of any RP worker as is the case for Jupiter today).
* Create regional or cluster group CosmosDBs that will contain the following document types:
  * Resource Metadata (Accounts, FHIR Services, IoT Connectors, DICOM Services, etc).
  * Additional metadata like secret rotation documents and internal proxy resources like Private Link, Events, etc.
  * Provisioning Requests
  * Completed Provisioning Requests
* The global database will remain for the following metadata:
  * Subscription metadata.  This information is global an not specific to location.
  * Account & Workspace names.  This information needs to be global to ensure global uniqueness.
  * Resource Limit metadata.  This technically could be moved to regional databases but due to the nature of the data this isn't required.  By keeping this global we limit the scope of the work.
  * Cluster and Service configurations.  This includes things like the LKG application version for FHIR and the AKS resource to use for a given location.  As with the resource limits above, this data could be theoretically moved to the regional databases but it doesn't contain any of the data classifications that would force such a move and by keeping it in the Global database we limit the scope of the work needed.
* Switch region specific ARM endpoints.  The ARM request for a location will be directed to the Healthcare APIs RP service in the same region.  This is similar to what we do already today for preview regions, TIP, and canary.
* We will need to do a data migration from the global database to the regional databases.  As part of the implementation we could allow regional resources to fall back and query the global database if the data wasn't found in the regional database to help with the transition period.

The current desire is to begin working on the transition after our March 2022 GA release to limit possible impact.  This may mean we can't GA in certain regions for Healthcare APIs until we satisfy the single data residency requirements.  The specifics are still being investigated.

Another option is we could look at a hybrid approach where add regional "cluster groups" for the regions that require single data residency.  Everything else would remain global.  This would allow us to limit the risk just to these specific regions and .  Everything else in the environment could be mapped to the default cluster group that points to the current Global Database.  The downside of this approach is if more regions adopt these restrictions in the future we will need a migration for the affected regions.

## Open Questions

### What about RPaaS?

RPaaS stores data per region so if the resources are location aware they will reside with the locality they were created in.  We still have .  Unfortunately, a full transition to RPaaS is unlikely to be feasible in the implementation timeframe.

### Resource Ids aren't scoped to location? Will location based routing work?

Short answer is it should.  We have region specific RPs today.  While a resource id (example ```/subscriptions/4092fd42-145e-4e91-aa15-96f66d2b6deb/resourceGroups/testrg/providers/Microsoft.HealthcareApis/workspaces/myworkspace```) doesn't contain a location embedded in it, for a tracked resource ARM knows the location of the resource and routes accordingly. Today, our root level resources, services & workspaces are tracked.  Any children, including proxy resources, under those tracked resources should be routed based on location.  If we have subscription or tenant level resources, or root level proxy resources will need to investigate our options further.

### What about subscription level queries?

Some resources support queries for all resources of a given type for a subscription.  This query isn't scoped for a given location.  For example you can query a specific workspace which will be routed to a regional specific RP but what happens if the RP request is for all workspaces in a subscription.  It isn't clear if this is handled in ARM first (i.e. the tracked resources are queried and then requests are federated to the necessary locations based on what was found) or will we need to implement a way to search across all the regional databases and return a result set.  Further investigation is needed.

### How this impact move operations?

Resources themselves will stay in the same location (subscription or resource group moves don't modify the resource's location only the resource group or subscription those resources are associated with on the customer's end). When you do a move, either subscription based or resource group based, the target resource is already scope. If we are only moving one resource per move request then there should be no change. The one caveat is the move operation will likely need to have the global db in scope because if we do a subscription move we need to update the subscription id the associated name document is linked to.  This should be handled automatically if we use the repository based approach outlined above since both resources already a managed by separate repositories.

# Azure Dogfood Migration Notes
*The goal of this document is to note solutions and problems considered in exploring options for moving our test environments to prod infrastructure. It should be used for notes and reference*

*Proposals for actionalble solutions will be addressed in other documents.*

[[_TOC_]]

# Business Justification
We currently rely on Azure Dogfood infrastructure for:
* Test Env
* Rel Env
* Personal and CI Envs

### Problems with Azure Dogfood
* Frequent downtimes outside of our department's control
* Customer hotfixes must wait when Dogfood is down
* Azure Storage cannot be tested
* Private Links cannot be tested
* Changes in TIP could break prod since they share the same global db

### Goals
* Test Private Link
* Test Azure Storage integration
* Avoid deployment downtimes

# Solution Space

### Azure Canary
* There are 2 Azure Canary regions that live within Azure prod, 'East US 2 EUAP' and 'Central US EUAP.'
* We may be able to use Central US EUAP as our Test region.
* All regions are different, so we need to deploy and prove that Healthcare APIs will work as expected in Central US EUAP.
* Deployment process:
    1. Request Cosmo access to central us region
    2. Verify Cosmo access
    3. Create prod change request PCR
    4. Update prod.json configs
        1. networking
        1. scaling
        1. sku on public id
        1. type of managed disks
        1. availability zones
        1. prefixV6 not overlapping
    5. Deploy by modifying classic pipeline for release to tips canary
manual steps
    6. Creates billing storage accounts
    7. Geneva action to upgrade push agent v2 (PAV2)
update ARM manifest
* We have mostly setup the FHIR deployment. We need to provision DICOM infrastructure for deployment.
    * [Instructions to deploy new DICOM region](https://microsofthealth.visualstudio.com/Health/_git/workspace-platform?path=/docs/adding-new-regions-for-DicomService.md&_a=preview)
* If there are resources that are unavailable in Central US EUAP we may be able to map them to other regions.
* We might want to re-purpose East US 2 EUAP to become the Rel Env. Today, this uses same bits as release. We suggest giving it its own global resources (DB, KV). This helps ensure global uniqueness for customer URIs.
#### Risks
* If we are going to open up central euap that is currently configured as prod as our test env, than we are taking more risk than we should. Because we are sharing global DB and we will run into issues with ARM manifest.
* We may want new subscriptions so as not to conflict with customers.
* This env may need higher permissions to debug issues.
* We currently use Azure Canary (East US 2 EUAP) region in our prod release pipeline.
SDP requires prod deployments start by deploying to a Azure Canary region first.
For scheduled downtimes, we have mitigation procedures in place.

* We want to avoid breaking anything that depends on how we currently use East US 2 EUAP.
* We need contingencies for when scheduled disaster recovery drills conflict with hotfixes that cannot wait
    * [Canary Drill Process](https://microsoft.sharepoint.com/teams/AzureCanaryMSFT/SitePages/FAQs.aspx#what-are-drills)
* If global resources are isolated from prod we need to make sure that test URIs don't collide with production customers. We would probably need to have new DNS entries.

### AFEC Flags
* We can have multiple clusters on same region using feature control flags for different clusters.
* With multiple clusters per region we need different customer subscriptions.
* We may need to have separate 1st party app IDs.
* Concern about subscriptions that fall in multiple paths.
* Concern about ARM validation not being sophisticated enough.
* Engineers will have to be vigilant about not introducing subscription overlap issues.
* We need guards to ensure tests are run in the correct cluster.
* Given we only have 2 sets of AAD tenant, we are going to need more user facing subscriptions.
* We can't use same subscription in Central to do the same test in East US 2 because we won't know which it will route to.
* One potential upside is that each subscription will have its own quota.

### Azure Stage
* There are 2 Azure Stage regions.
* We will need to dig into what resources are there.
* Some feature flags are required.
    * [Engineering hub ARM Locations Behind Feature Flags](https://eng.ms/docs/products/arm/api_contracts/armlocationsbehindfeatureflags#public-additional-region-info-as-per-arms-configuration)

### Virtual Regions
#### Background
* [Virtual regions in ARM](https://armwiki.azurewebsites.net/internals/configuration/virtual_regions.html) is an entirely logical region in ARM and does not have its own compute or storage and instead borrows its infrastructure from another region.
* To customers, virtual regions appear as just another region in ARM and are unaware of the underlying infrastructure region being different.
* To add a virtual region, an entry must be added in the virtualRegions section in the [cloud specs](https://msazure.visualstudio.com/One/_git/AzureUX-ARM?path=%2Fsettings%2FRegions%2FPublic%2F_cloud.json). 

#### Usage
* By adding a virtual region, we can reserve this region as another TiP region to deploy and test Microsoft.HealthcareAPIs bits, i.e., we could create new virtual regions to increase the number of TiP regions available for testing.
* In addition to adding this new virtual region in our RP's arm manifest, this would also need to be added to other RP manifests that we depend on. 
    * While not impossible, this would be difficult to coordinate with all other RPs to include this new region. 
    * As an alternate to the above, we can create and maintain a custom mapping b/w a virtual region and the infra region, within our RP such that requests to other RPs would target the mapped infra region directly.
* Open questions - 
    * How will linked resources (such as AAD RBAC, Managed Identity, Shoebox logging, etc.) or other first party integrations like (Event Grid, Private Link, etc.) work with this new virtual region?

#### Outcome
* Pros
    * Number of TiP regions can be increased by creating virtual regions without having to do any other ARM region setup since its a logical region. 
* Cons
    * In the recommended scenario, all other dependent RPs must include the region in their manifest which is hard to coordinate.
    * In the scenario where we choose to maintain the mapping on our end, since ARM's virtual region is a globally visible entity (i.e., customers would see these) and it would be confusing if only one service is supported for this region.
    * Non-trivial RP changes to maintain mapping of virtual regions to infra region and perform routing (to other RPs) appropriately.
    * Complicates scenarios like billing.
    * Not a traditional use of virtual regions, and no other RP today uses virtual regions in Public cloud.
    
# Migration Planing Notes
1. We want to focus on the Test Env first and then Release. Due to different complications, we want tackle Personal and CI Envs separately.
4. We are also looking into using AFEC feature flags to host multiple clusters in the same region.
5. Once we have a parallel Test Env we can re-assess next steps for Rel and Personal and CI Envs.
6. Decommission legacy Test Env.
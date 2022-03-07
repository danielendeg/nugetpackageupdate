*Goals*
* *Use the "Azure Public" cloud for our test environment.*

*Non-Goals*
* *Release environments, personal environments, CI environments.*

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

### Scenarios
* Test Private Link
* Test Azure Storage integration
* Avoid deployment downtimes
# Migration Plan for Test Env
1. We want to focus on the Test Env first and then Release. Due to different complications, we want tackle Personal and CI Envs separately.
2. Deploy to the 2nd Canary region, Central US EUAP, to match our Test Env.
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
3. If Central US EUAP can meet our Test Env requirements, we can begin simultaneously deployments.
4. We are also looking into using AFEC feature flags to host multiple clusters in the same region.
5. Once we have a parallel Test Env we can re-assess next steps for Rel and Personal and CI Envs.
6. Decommission legacy Test Env.

### Risks
* We currently use Azure Canary (East US 2 EUAP) region in our prod release pipeline.
SDP requires prod deployments start by deploying to a Azure Canary region first.
For scheduled downtimes, we have mitigation procedures in place.

* We want to avoid breaking anything that depends on how we currently use East US 2 EUAP.
* We need contingencies for when scheduled disaster recovery drills conflict with hotfixes that cannot wait
    * [Canary Drill Process](https://microsoft.sharepoint.com/teams/AzureCanaryMSFT/SitePages/FAQs.aspx#what-are-drills)
* If global resources are isolated from prod we need to make sure that test URIs don't collide with production customers. We would probably need to have new DNS entries.

# Alternatives To Azure Dogfood

### Azure Canary
* There are 2 Azure Canary regions that live within Azure prod, 'East US 2 EUAP' and 'Central US EUAP.'
* We may be able to use Central US EUAP as our Test region.
* All regions are different, so we need to deploy and prove that Healthcare APIs will work as expected in Central US EUAP.
* We have mostly setup the FHIR deployment. We need to provision DICOM infrastructure for deployment.
    * [Instructions to deploy new DICOM region](https://microsofthealth.visualstudio.com/Health/_git/workspace-platform?path=/docs/adding-new-regions-for-DicomService.md&_a=preview)
* If there are resources that are unavailable in Central US EUAP we may be able to map them to other regions.
* We might want to re-purpose East US 2 EUAP to become the Rel Env. Today, this uses same bits as release. We suggest giving it its own global resources (DB, KV). This helps ensure global uniqueness for customer URIs.

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
NOTE: Turns out we cannot pursue this option because we would have to depend on an Azure wide ARM team that hanldes all Virtual Regions.
* A Virtual Region can be configured within a region we already deploy to.
* Custom mapping is needed because other RPs won't be in the same region.
* We may need to map virtual regions to their explicit parent location instead of assuming co-regionality.
    * [Stockoverflow mention](https://stackoverflow.microsoft.com/questions/285030/286597)
* If we have a resource in virtual East US, then rather than assume our infra resources (storage, cosmos db, event hub, etc) go into the same region, we can see what actual region is mapped to the inbound region and use that. 
* External resource dependencies should also be possible (FHIR export to storage, event hub for IoMT) but would need to be deployed in the actual region.
* Going from Virtual East US to East US and back might be considered different regions and we would incur egress charges.
* An open question is how linked resources work and will those be a problem with the new locations. Examples of this would be AAD RBAC permissions or Shoebox logging settings for the customer.
* We are not sure how this would interact with other first party integrations like Event Grid and Private Link.

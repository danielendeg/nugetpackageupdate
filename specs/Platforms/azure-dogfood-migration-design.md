*Design proposal to migrate HealthcareAPIs test infrastructure pipelines off of Azure Dogfood environment and into Azure Public cloud environment.*

[[_TOC_]]

# Business Justification

ARM Dogfood environment is a PPE (Pre-Production Environment) for deploying and testing custom bits.

Currently we leverage Azure Dogfood for testing various versions of Microsoft.HealthcareAPIs, specifically with the following setup:
* Test environment - This environment is based off of the RP bits from master branch.
* Rel environment - This env is based off of the RP bits from the release branch.
* Personal environments - These environments are based off the the RP bits from personal developer branches.

There are CI/CD pipelines in place that deploy Test/Rel environments to Dogfood and test pipelines that periodically run integration tests against these environments.

However, there are some problems when using Dogfood that impacts reliable testing and developer productivity:
* Frequent downtimes/flakiness outside of our department's control
* Customer hotfixes must wait when Dogfood is down
* There are many RPs that are not fully supported with all the features that are otherwise supported in Azure Public cloud (eg:- Azure Storage, Event Hubs, Private link), which in turn impacts various of our integration tests which cannot be performed in Dogfood that rely on these external RPs.

As a result, in order to make HealthcareAPIs' test infrastructure more reliable and available, there is a need to migrate off of Azure Dogfood.

Another aspect of this design proposal is to also alleviate the resource contention with TiP regions used for testing custom bits in Prod environment. Currently, we dedicate two regions (Central US and Australia Central) for TiP testing, and since its a shared resource, developers have to schedule their TiP reservations which often times leads to delay in testing depending on this schedule. While migrating off of Dogfood will reduce the stress on TiP to some extent (since most of the integration tests will going forward run against the Azure Public cloud and thereby reduce the number of test scenarios that developers generally lean on TiP for), having more TiP regions can further reduce the resource contention and improve development efficiency.

# Scenarios
The goal of this design is to enable the following scenarios:
* Ability to reliably deploy and test Microsoft.HealthcareAPIs in Azure Public cloud.
    * Goals
        * Deploy and test HealthcareAPIs to ***Test*** environment in Azure Public cloud.
            * Update CI/CD infrastructure to deploy RP bits from master branch into Azure Public cloud.
            * Update test pipelines to test against "test" Microsoft.HeatlhcareAPIs in Azure Public cloud.
        * Deploy and test HealthcareAPIs to ***Rel*** environment in Azure Public cloud.
            * Update CI/CD infrastructure to deploy RP bits from release branch into Azure Public cloud.
            * Update test pipelines to test against "rel" Microsoft.HeatlhcareAPIs in Azure Public cloud.
        * Deploy and test custom HealthcareAPIs to ***TiP*** environments in Azure Public cloud.
            * Increase number of TiP environments that can be setup and deployed to.
    * Non-goals
        * Deploy and test HealthcareAPIs personal environment in Azure Public cloud.
* Ability to add integration tests around scenarios that are dependent on other RPs that are not fully supported in Azure Dogfood.
    * IoT E2E data flow test (with EventHub, Storage dependency).
    * Private link scenarios.

# Design

## Solution Space
There are various options that were considered and explored as alternatives to Dogfood. This section details the various approaches explored and the outcomes for each.
### **Azure Canary**
* We considered using Central US EUAP for our Test environment.
* We are deploying to prove that Healthcare APIs will work as expected in Central US EUAP.
* To include DICOM we need to provision DICOM infrastructure: [Instructions to deploy new DICOM region](https://microsofthealth.visualstudio.com/Health/_git/workspace-platform?path=/docs/adding-new-regions-for-DicomService.md&_a=preview).
* If there are resources that are unavailable in Central US EUAP we may be able to map them to other regions.
* We considered re-purposing East US 2 EUAP to be our Release environment. Today, this uses same bits as release. We suggest giving it its own global resources (DB, KV). This helps ensure global uniqueness for customer URIs.*

### **Azure Stage**
* There are 2 Azure Stage regions.
* We will need to dig into what resources are there.
* Some feature flags are required.
    * [Engineering hub ARM Locations Behind Feature Flags](https://eng.ms/docs/products/arm/api_contracts/armlocationsbehindfeatureflags#public-additional-region-info-as-per-arms-configuration)

### **Virtual Regions**
* [Virtual regions in ARM](https://armwiki.azurewebsites.net/internals/configuration/virtual_regions.html) is an entirely logical region in ARM and does not have its own compute or storage and instead borrows its infrastructure from another region.
* This solution is no longer feasible as the ARM Virtual Region team mentioned that they are unable to justify the usage of virtual regions for this scenario and will not be able to approve this request, and instead suggested to explore usage of AFEC flags for testing in Public cloud.**

### **Azure Feature Exposure Control (AFEC)**

#### Background
* [Azure Feature Exposure Control (AFEC)](https://armwiki.azurewebsites.net/rp_onboarding/afec/FeatureExposureControl.html#introduction) allows an RP to control ARM's routing behaviour to resource type endpoints or api versions, based on a set of "Features" which can be enabled or disabled for a subscription.
* It is commonly used to control exposure of new resource types, api versions, or to route requests to dev/test endpoints based on the originating subscription.

#### Usage
* Create AFEC flagged endpoints in our RP manifest.
    * For each resource in our RP manifest, create another endpoint:
        * With the `endpointUri` pointing to a newly deployed cluster that is separate from the Production env/clusters.
        * Set the `requiredFeatures` to a newly created AFEC feature registered under our RP.
* For subscriptions which have enabled this AFEC feature, ARM will route the request that originated in these subscriptions, to the endpoint that has listed this feature under `requiredFeatures`, otherwise defaults to an endpoint based on standard routing options (api versions, order in the list, etc.).
* We can use disjoint AFEC flags to segment versions in the same way as a region works.
* Therefore, using AFEC flags we can control the routing of the requests to specific test endpoints that have test versions of Microsoft.HealthcareAPIs deployed to.

#### Outcome
* Pros
    * Recommended approach for testing different RP versions. Several other RPs (eg:- Microsoft.Compute,...) employ AFEC to manage Canary/PPE environments, so it is tried and tested.
    * No limitations on the number of test environments we want to configure as there are no restrictions on the number of AFEC flagged endpoints that can be created.
        * This solution can help with setting up "Test"/"Rel" in Azure Public cloud and also in increasing the number of TiP environments we wish to configure.        
    * No restrictions on the number of regions we want to enable in "Test"/"Rel" environments as there is no concern in overlapping these regions with Prod (i.e., we can have test clusters set up for each of the Prod regions supported if needed).
    * No RP changes required.
    * Fairly straightforward setup
        * Environment deployment scripts can be used as is to set up a new test environment with minor changes to the environment configurations.
        * Minor changes to test scripts to ensure tests are run on the appropriate subscriptions.
* Cons
    * Need a dedicated customer facing test subscription for each distinct test environment, i.e., each test subscription should only enable one of the test AFEC flags, so we will require multiple isolated test subscriptions.
    * Maintainence of the arm manifest - every resource will need to include new endpoints for each of the test environments.

**This design approach seems to be the most feasible one among the above options, as the cons listed are not real blockers at this point and based on the PoC that was done, the usage of AFEC flagged endpoints was validated and seems like a more straightforward way to setup Test, Rel and TiP environments.**

## Proposed Solution *(WIP)*

As mentioned in the previous section, among all the design approaches explored, the usage of AFEC flagged endpoints seemed like the most feasible option.
Detailed below is the set of proposed changes for each of the scenarios using the AFEC design approach.

### Configure ***Test*** env for Microsoft.HealthcareAPIs in Azure Public cloud

1. Create new ***Test*** [environment](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=/doc/project-resolute.md&_a=preview&anchor=environments) which will    contain all the Azure resources (clusters, global resources, etc.) required for the entire PaaS offering.
    * By creating a new environment, there will be a dedicated set of global resources like global DB, global Key Vault, etc. that is exclusively used for this environment. This level of isolation ensures that only the RP worker that is deployed in this test environment will work on the provisioning operations specifically created on the test endpoint and there is no overlap with the Prod RP workers and vice-versa.
    * There will be a global arm resource provider traffic manager profile set up for this test environment that will be used to route the requests to the clusters in this environment.
    * For ***Test*** environment, we can have the infrastructure be hosted in non prod subscriptions in the MSFT tenant (same as current test/rel env setup).
        * The dnsZone would be "mshapis.com". We can create child zones within this for each of the test environments, i.e., one for ***Test*** - say `test.mshapis.com`, and similarly for ***Rel*** - say `rel.mshapis.com`.
    * Steps:
        * Setup "test.json" environment configuration file (i.e. deployments\environments\test.json) for the test environment.
            * Either overwrite the existing test.json or create new configuration.
                * Since we might want to first test this out and not impact the existing test env config that currently is set up to work with Dogfood, we can start with creating a new test env named differently.
            * Use the same non-prod subscription to host this env that we currently use for our test/rel environments, i.e., HealthPaaS-NonProd.
            * Add cluster configurations - can include clusters based on as many Prod regions as needed.
            * Use `armNamespace` as "Microsoft.HealthcareApis".
            * Ensure `arm` section has `autoRegisterArmManifest` set to false since we are updating prod arm manifest which requires JIT elevation (Step 3).
            * Reference "test.json" environment group configuration file (i.e., deployments\environmentGroup\test.json)
                * Update the `testCustomerAuth` section to point to Azure Public cloud details and dedicated test subscription details.
                * Update the `arm` section to point to the Azure Public cloud endpoint.
        * Deploy test.json using [Deploy-Environment.ps1](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=/deployment/Deploy-Environment.ps1&anchor=environments) script
            * Script updates to replace Dogfood references.
        * Deploy the applications to the Service Fabric clusters using [Deploy-Applications.ps1](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=/deployment/Deploy-Applications.ps1&anchor=environments) script.
            * Script updates to replace Dogfood references.

2. Create an AFEC feature specifically to route requests to the endpoint associated with this ***Test*** environment.
    * The AFEC feature should be unique to the test environment i.e., every test environment should have a separate AFEC feature.
    * The AFEC feature should be created under the Microsoft.HealthcareAPIs RP namespace. 
    * Steps:
        * Follow the steps in [here](https://armwiki.azurewebsites.net/rp_onboarding/afec/FeatureExposureControl.html#creating-a-feature) to create a feature.
            * For the `Feature allowlist Tenant Ids ` provide the test tenant IDs that contains the test subscriptions that we intend to reserve for the test environments.

3. Update "prod" arm manifest.
    * Since we are using the same arm namespace in Azure Public cloud across the test environments, we need to update the prod arm manifest to include the AFEC flagged endpoints.
    * Steps:
        * Update [prod.json](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=/deployment/armManifests/prod.json&anchor=environments) arm manifest configuration file.
        For each resource, add a new endpoint:
            * The same `apiVersions` as other endpoint.
            * The same `locations` as other endpoint.
            * The `endpointUri` pointing to the global arm resource provider traffic manager profile created for the test endpoint.
            * The `requiredFeatures` specifying the AFEC feature created in Step 2.
            * Eg:- if the environment is named - `test` then the TM profile created is `armresourceprovider-test.azurehealthcareapis.com`, and if the AFEC feature created is named `Microsoft.HealthcareApis/MSHAPISTestEnv`, then the endpoint would look like:
            
                        {
                            "enabled": true,
                            "apiVersions": [
                                "2020-11-01-preview",
                                "2021-03-31-preview",
                                "2021-06-01-preview",
                                "2021-11-01",
                                "2022-01-31-preview",
                                "2022-02-28-preview"
                            ],
                            "endpointUri": "https://armresourceprovider-test.azurehealthcareapis.com:15612/providers/Microsoft.HealthcareApis/",
                            "locations": [
                                "West US 2"
                            ],
                            "requiredFeatures": [
                                "Microsoft.HealthcareApis/MSHAPISTestEnv"
                            ],
                            "timeout": "PT20S"
                        }
        * Update the ARM manifest in Prod following the steps in [here](https://microsoft.sharepoint.com/teams/msh/_layouts/15/Doc.aspx?sourcedoc={88c3d919-efdc-4b77-9910-b7e1f892113d}&action=edit&wd=target%28Teams%2FARM%20and%20Portal.one%7Cce4f3ca8-18a2-48a9-b20a-f9c8d7ddd591%2FUpdating%20ARM%20Manifest%20in%20Prod%7C40a97080-cdce-4bbf-b2b5-21ecfa73b242%2F%29&wdorigin=703)

4. Reserve set of dedicated customer facing test subscriptions for the ***Test*** environment.
    * Each of these subscriptions should enable the AFEC feature created in Step 2.
    * Steps:
        * Follow the steps in [here](https://armwiki.azurewebsites.net/rp_onboarding/afec/FeatureExposureControl.html#registering-a-subscription) to register a subscription against a feature.
        * Alternatively, using the portal:
            * Search for `Preview Features`.
            * Select the subscription.
            * Select the feature and click `+Register`.
            * Post the above, the "State" for the selected feature should show "Registered".

5. Update CI/CD pipelines to automate deployment to the ***Test*** environment.
    * Step 1 involved setting up the ***Test*** environment using the scripts as an adhoc step. To automate this deployment as part of our CI/CD pipelines, we need to update the pipelines to point to the correct test environment configuration.
    * Steps:
        * Update pipeline variables such as environment name to the name of the ***Test*** environment.
            * [Resolute_PaaS_CI](https://microsofthealth.visualstudio.com/Health/_build?definitionId=595&_a=summary).
            * [Resolute_PaaS - Applications](https://microsofthealth.visualstudio.com/Health/_release?definitionId=124&view=mine&_a=releases)
            * [Resolute_PaaS - Environment](https://microsofthealth.visualstudio.com/Health/_releaseDefinition?definitionId=123&_a=definition-tasks&environmentId=456)
        * Include variable for the test subscriptions (specific to this ***Test*** environment) to run the functional tests against.

6. Update test pipelines to run the functional tests against the ***Test*** environment.
    * Steps:
        * Update the functional test scripts ([Run-FunctionalTests.ps1](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=/deployment/Run-FunctionalTests.ps1)) to target the test subscriptions in Azure Cloud - update the test env variables as needed.
            * Add subscription checks to ensure when invoked we are verifying the test subscriptions enabled for the intended test environment.
        * Update the pipelines to point to the correct set of subscriptions (new pipeline variable?) and test environment:
            * [Resolute_PaaS_Tests_Only](https://microsofthealth.visualstudio.com/Health/_release?_a=releases&view=mine&definitionId=78)
            * [Resolute_PaaS_Rel_Tests_Only](https://microsofthealth.visualstudio.com/Health/_release?_a=releases&view=mine&definitionId=80)
            * [Resolute_PaaS_IoMT_Tests_Only](https://microsofthealth.visualstudio.com/Health/_release?view=all&_a=releases&definitionId=129)
            * [Resolute_PaaS_Rel_IoMT_Tests_Only](https://microsofthealth.visualstudio.com/Health/_release?view=all&_a=releases&definitionId=128)


### Open questions/points to consider for other envs:
* What is the level of environment isolation we desire?
    * Separate global resources for each environment.
        * This is achieved by default if we create different environments for each test setup.
        * Concerns-
            * How do we achieve global uniqueness of resources since they all share the same ARM RP namespace but the global DBs are split(resource name has to be unique and not the FQDN)?
    * For Test/Rel environments, we can have the infrastructure be hosted in non prod subscriptions in the MSFT tenant (same as current test/rel env setup).
        * The dnsZone is "mshapis.com" with child zones created for each of the environments (eg:- test.mshapis.com, rel.mshapis.com)
        * Since both these test environments are set up in the non prod subscriptions in MSFT, it might be okay to even have them share the service principals, certs, etc.
    * For TiP environments, if we want to keep it more similar to the Prod env, then we can host this in prod subscriptions in the AME tenant.
        * Concerns - 
            * TiP service principals would have same permissions as Prod service principals to access the Prod 1P apps if the same certs are being used.  
            (Note - This isn't an issue with Test/Rel since these env are hosted in the MSFT tenant)
                * Explore setting up multiple certs (different subject names) for the 1P apps.
                    * Update the deployment scripts to configure the app manifest params for the apps, with the appropriate certs for the TiP env.
                * Set up new 1P app? completely different service principals, certs, domain...

# Test Strategy

To test out if the AFEC flagged endpoints works as expected and can be leveraged to set up test environments in Azure Public cloud as per the proposal above, we can start with setting up mock environments in Dogfood that mimic the prod and test environment setup.

To elaborate:
* Create two (personal) environments - `mockprod.json` and `mocktest.json`.
    * Have these environments share the same RP namespace - say `Microsoft.HealthcareApisMock`
* Follow guidance from the Steps 1-4 from the proposal section in accordance to the personal environments created.
* Validate - 
    * A provisioning request created in the test subscription with the AFEC feature enabled, was routed to the mocktest env-cluster and therefore the operation was completed by the mocktest RP.
    * A provisioning request created in the test subscription without the AFEC feature enabled, was routed to the mockprod env-cluster and therefore the operation was completed by the mockprod RP.


# Security

*Describe any special security implications or security testing needed.*

# Privacy

*Describe any new data that is being stored/processed (including logging), what data classifications they are, and how long the data will be retained.*
*Will any of the above require an update to the privacy data inventory? If yes, provide a work item link to track that.*

# Other

*Describe any impact to privacy, localization, globalization, deployment, back-compat, SOPs, ISMS, etc.*

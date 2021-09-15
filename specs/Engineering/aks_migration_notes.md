# Migrating FHIR from health-paas to workspace-platform
[[_TOC_]]

# Prototype
To kick off the project, we will create a prototype of the FHIR service running in the workspace platform
* This will be in a feature branch to start with
* Add a fhir-server directory in workspace-platform with the necessary csproj files for the FHIR service's PaaS customizations
    * Changes needed in the FHIR service to use MI and not use the ARS
		* Updated Dockerfile if needed
	* Write a temporary YAML spec for a service deployment, for prototyping purposes
		* Does this need to install our azure rbac cert?
		* Add ingress for FHIR server
	* Write a temporary script and/or ARM template that deploys Azure resources for a FHIR service:
		* Managed Identity
		* Traffic Manager Profile?
		* DNS Zone?
		* SQL Database/Cosmos DB
		 App insights & availability tests


# FHIR in Workspace Platform
After learning from the prototype, we can take the learnings from it and start working on the feature. Since this feature will take a relatively long time to implement, we should work in the main branch rather than working in a feature branch, to avoid the pain of merge conflicts. We would have to be careful about ensuring all changes checked in do not break the test, prod, or personal environment deployments.

The structure of the code base for the workspace-platform FHIR service should loosely match how the structure of the DICOM and IoMT code bases. So we would end up with the following directories:

* build - build related artifcats
* deploy - EV2 deployment related artifacts
* operator - the controllers and CRDs for provisioning fhir compute resources, updating them with releases, and managing schemas
* service - service customizations for PaaS
* provision - provisioning related code
* roles - rbac roles
* scripts - helper scripts (provision/deprovision, any other helpers needed)
	
## Operator
DICOM and IoMT use the operator pattern. Sync with Nick to get details on how they use it. In summary:
* dicomrelease controller watches a dicomrelease crd to determine if there is an update to the dicom container image
* dicom controller will run on dicom CRDs to ensure they have the latest container image. It also sets up the ingress resource for a DICOM service, the configuration for a DICOM service, and managing schema initialization and upgrades for a DICOM service's SQL database

We will need to create similar controllers for the FHIR service. There may be opportunities to refactor out common code from existing controllers and re-use them.

## Provision
Provisioning will be described in detail later in the doc

## Deploy
The workspace-platform uses EV2 deployments by default. We will need to rewrite the FHIR deployment process to run in EV2.

## Shared code base
Some of the PaaS specific implementations will need to be copied over to the workspace-platform. We should move these into NuGet packages that health-paas and workspace-platform would consume, rather than copying code around. This way development can continue in both code bases without issues syncing new changes in health-paas to the workspace-platform.


# Dual Stack Support

During development and before the final migration to the workspace platform, there will be a period of time where the FHIR service will need to continue to support running in Service Fabric while we're incrementally working on rebuilding the FHIR service in the workspace platform.

The provisioning flow will need to be updated to support adding the necessary resources to run a FHIR service in workspace-platform, for example it will need to support writing a FHIR CRD into the AKS cluster, adding a Traffic Manager profile pointing to the AKS cluster, etc.

However, in different stages of the development process and in different environment, we will need to provision differently. For example, during development:
* Test, prod, rel, and gov should continue to provision for only Service Fabric
* Personal environments should be configurable to provision in either workspace-platform or health-paas
* During the cut-over process, we will need to provision in dual stack mode (that is, resources are available in both workspace-platform and health-paas)

Additionally for dual stack mode, we will need to be able to configure which cluster a FHIR service would hit, even though it is available in both SF and AKS:
* SF Only
* AKS Only
* Both

To support this, provisioning will need to be configurable to specify which mode a FHIR service should be provisioned in. We can separate these out into a config and a subconfig: ProvisioningMode and RoutingMode. Provisioning Mode could be:
* health-paas
* workspace-platform
* dualstack

Routing mode should be nullable and only settable if provisioning mode is dualstack. Then, the values could be:
* health-paas only
* workspace-platform only
* both

During the migration process, routing mode may be set to both. To support this, some options to look into for routing traffic:

1. 1 traffic manager profile configured with multiple endpoints (SF and AKS in dual stack), and a specific routing method (e.g. round robin)
    * \+ Easy to implement
    * Will require a reprovision to apply to all accounts, or a tool
    * \- DNS records will be cached by clients, so the switch could take some time. Could be problematic if something is broken in AKS and we need to switch back quickly
1. 3 Traffic Manager Profiles:
	* SF Only
	* AKS Only
	* Hybrid (round robin or some other method)
		We would redirect traffic to the right infrastructure by pointing the DNS entry to the desired traffic manager profile.
        - \+ It's simple to implement.
        - \-This can take some time to take effect due to DNS caching on the client side
        - \- Will require reprovision to change every customer's DNS Zone to point to a different traffic manager
        - \- DNS has a much longer TTL than traffic manager, even more problematic if we need to switch back quickly
1. Add a separate load balancer for each SF cluster that is used by FHIR service traffic only. During cut-over, update the LB to point to the corresponding AKS cluster instead of the SF cluster.
	* ?Is this possible
    * Will need to separate out FHIR traffic from other traffic, since services such as the RP will need to route to SF only.
    * \+ Switching between clusters should take effect quickly
    * \+ Can be applied to all customers with only one change
    * \- Can't granularly change traffic routing mode, e.g. for test accounts only
1. Add a load balancing service
	* Would live in either AKS or SF and can be configured to route x% traffic
	* \- Have to build and maintain this service
	* \- Adds latency to whichever infrastructure it isn't built in
	* \- +Gives us the ability to support replicating traffic in test environments to verify all traffic works in either environment.
	* \- ?Is there an existing solution we can leverage instead of building one?

Option 1 seems the most appealing to me. We should have enough confidence that everything will work before going into prod, so ideally we shouldn't have an issue that we don't catch in test or canary first, that would require an immediate rollback. It also gives us fine grain control to be able to set the routing rules on specific accounts, so we could have a gradual rollout process that goes through test accounts first, etc. Option 3 is somewhat appealing, assuming it's feasible. 2 and 4 don't seem appealing. 2 is a worse version of 1. 4 would take more development time.

# Provisioning

## Prototype
Start with a hacky provisioning prototype to be able to test the prototype of the FHIR service running in AKS.
This includes 3 steps:

	* Kubernetes spec for provisioning:
		* FHIR service from container image
		* Ingress resource
		* AzureIdentity and AzureIdentityBinding
	* ARM template for provisioning:
		* DNS zone
		* TM Profile
		* SQL Database/Cosmos DB Database
		* Managed Identity for the FHIR service
		* App Insights Web Test?
		* All resources should have a common tag for the prototype and be in the same resource group
	* Script that will take in the instance name and apply the kubernetes spec and ARM template
	* Script to clean up all these resources?

## Console App
Take lessons learned from the provisioning prototype & FHIR prototype to start working on a Console app for provisioning. The DICOM service has a similar console tool.

This tool would be specifically for the scenario where a FHIR service would be provisioned to run in workspace-platform only. 

Aside from helping with further prototyping the provisioning flow in workspace-platform only mode, this console tooling is used in workspace-platform to perform provisioning without having to have a health-paas personal environment configured to perform provisioning in a personal workspace-platform.

This stage will probably need a preliminary implementation of the FHIR and FHIR release controllers implemented.

## RP Worker
The next step is to integrate some of the work from the Console App to implement provisioning for FHIR in workspace-platform in the RP Worker. We will need to be able to do this in a manner that does not impact prod provisioning until we reach a relatively stable state. Our options are to either:

1. work in a feature branch and merge in to main when the code is stable
1. or actively work in main branches, with configurations to disable workspace-platform FHIR provisioning until we are ready to enable it for dual stack.

Since this work can take several sprints, the merge from feature to main could get complicated and need significant merges to complete. Given that, option 2 seems more appealing, as long as we ensure that disabling workspace-platform provisioning in health-paas works.

There should be a mechanism added that would specify the provisioning mode (SF only, workspace-platform only, or dual stack). There are 3 options here:
1. The easiest implementation would be a configuration flag in the RP worker, however this would need redeployment to change the setting.
2. On the other hand, allowing to specify the infrastructure to provision in for each provision would be more flexible, but would be more complicated to implement in a way such that it is not an exposed option to customers in prod.
3. A middle ground could be having a document in the global DB to allow overriding the configuration value in the RP worker. This can be used for testing purposes, but also to help with changing the behavior quickly in prod if we run into an issue during cutover that needs quick mitigation.

I would go with option 3. This should include the mechanism for specifying the routing mode as well, as described in the dual-stack section above.

### health-paas Only
This is the current provisioning mode - FHIR service + Azure resources are provisioned for SF only. During development, prod, rel, gov and test should be configured to provision in this mode. There should not be any difference to what we provision for customers. Personal environments should also still be in this mode by default.

### workspace-platform Only
This is the mode that our services will run in at the very end, and it will be the mode we use during the initial development process. This should leverage the work from the Console App to provision the FHIR service to work in workspace-platform only. The RP Worker would be configured to provision the resources necessary to run a FHIR service in workspace-platform only. This will allow us to ensure that the FHIR services are running fully in Kubernetes. Developers of this feature should be able to configure the RP worker in a personal environment to run in this mode.

### workspace-platform + health-paaS
This is the mode that our services will run in during the migration process. In this mode, FHIR services will be provisioned in both Service Fabric as well as in the workspace-platform. The other difference between the above modes and this one is in how we do routing, as described in the dual stack support page. We would need to have a way to configure which infrastructure customer traffic should be going to.


# Cluster Health, Monitoring, & Dashboards

In SF, we currently have alerting when SF FHIR services are unhealthy, including some dashboards. We will need to have similar health monitoring & alerting in the AKS backed FHIR services.

We will also need to ensure existing DRI dashboards are supported for AKS FHIR instances as well, before performing the  migration. This will ensure that the DRI process during the migration remains the same, and the existing DRI will be able to continue keeping an eye on things.

# Cutover

For the cut-over process, we will gradually shift all traffic from Service Fabric instances to Workspace-Platform instances. We will need to decide at what granularity we want to shift traffic, which will inform the decision on how we build dual-stack in the above section. Options can be any combination of the following:

	• X% of FHIR instances
	• Test instances first followed by lower traffic instances followed by higher traffic instances
	• X% of traffic to each FHIR instance is diverted to workspace-platform and ramped up gradually

For verifying feature correctness, existing tests should suffice (though some may need to be rewritten or adjusted for AKS). We could write a test that would send a variety of requests to an SF instance, and send the same ones to the AKS instance, and verify the behavior is correct in AKS.

## Process
There are multiple steps needed to run in order to perform the cut-over. This would also depend on the method used for routing traffic to either Service Fabric or Kubernetes, but loosely it would be:

1. Configure RP Worker to provision in dual stack, with traffic routed to SF instances
1. Force a re-provision for all FHIR services, so the necessary components are available in both SF and AKS
1. Run the cut-over tool to point the test instances in the cluster to AKS.
1. ?Configure RP worker to provision with FHIR traffic routed to AKS instances (some or all of it). (We're doing this at this point to prevent a customer from running a reprovision that would cause them to be routed back to SF)
1. Run the "cut-over" tool to point x FHIR server instances from SF to AKS (stage per cluster in a similar fashion to SDP)
1. Continue running cut-over tool with broader sets of FHIR services in a region until all have been routed to AKS only
1. Monitor AKS FHIR dashboards for any issues. If an issue comes up:
	1. Configure RP worker to provision with FHIR traffic routed to SF only (but keep it configured to support dual-stack provisioning)
	1. Run the cut-over tool to point all instances to SF.
	1. Submit a fix for the FHIR container image in workspace-platform and repeat from step 3
1. Follow SDP to proceed to the next region
	
The reason to build and use a "cut-over" tool is to ensure the flipping from SF to AKS is done very quickly. The simpler approach is to reprovision all instances, however since provisioning for FHIR services can take 5 - 10 minutes, this might be too slow for reverting back to SF if any issues arise.

The cut-over tool can be a simple command line tool (or Geneva Action). It should work by using the same piece of provisioning code that configures traffic for a FHIR service to point to the desired infrastructure, and use existing global DB repositories. This should help minimize the testing surface needed for the tool.


## Reprovisioning
Note that we will need to be able to reprovision for gen 2 - not sure if we have the tooling for this yet.

# SF FHIR Deprecation
Once the cutover process is complete and we're happy with the FHIR services running in AKS, we will need to deprecate the FHIR services in Service Fabric.

As part of this process, we need to deprovision the components that are used by individual SF FHIR services such as:
* Traffic Manager profile
* ?SF FHIR load balancers
* SF Applications

So we will need a tool or an RP worker command that would specifically deprovision SF FHIR service components

The process at this point would look like this:

1. Configure all RP workers to provision workspace-platform FHIR components only
1. Run the SF FHIR deprovision tool
1. Remove SF FHIR components, deployment scripts, etc. from code base

# Things to keep in mind

* Need to decide on whether Cosmos DB backed instances should be migrated or not
		* I would say we should migrate them. You could decide to migrate them later on, rather than at the same time as gen 2 instances, if it seems like there's more work involved to support that. But they should be moved over to reduce the dependencies there currently are in SF
* Try and maintain feature parity as much as possible. Examples include:
    * Private link support
    * BCDR Support (specifically for Cosmos DB, where we technically support it for customers that asked for it, but don't expose it to the customer. We should carry over that support during migration).
	* ?Auto-scaling of FHIR compute instances?
* SQL Schema init/upgrade for FHIR will need to be changed, to match how it is done with DICOM. See this spec for details.
*   To maintain private link support (which DICOM is building to proxy traffic through service fabric), the frontend and account routing service will still need to be maintained
* The ARM RP Service requires .NET framework, so there is more work needed to support migrating it to AKS. However, some components of the RP can be migrated, such as the RP Worker, Global DB rotator, SQL Management application
* Need a plan to train the team continuously during the migration process to make the transition for development seemless. This includes:
	* How SF concepts map to AKS
	* Basics of using Kubernetes & AKS
	* AKS & MI
	* AKS networking
	* Development cycle in the workspace-platform
	* Etc…
* A lot of the new infrastructure will need documentation, especially to make the team productive as quickly as possible. Maintaining good documentation hygiene is a great opportunity to also help the team maintain good documentation going forward. Instill a culture of keeping documentation up to date while building this feature, and find ways/processes to ensure that culture infuses with the rest of the team.
		

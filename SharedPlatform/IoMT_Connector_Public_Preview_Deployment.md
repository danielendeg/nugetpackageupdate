# IoMT Connector – Public Preview - Deployment Project

## 1.	Resource Provider Worker:
Current Resource Provider Worker changes/updates will address the deployment of infrastructure in subscriptions outside of Resolute PROD.

https://microsofthealth.visualstudio.com/Health/_git/health-paas-docs/pullrequest/12961 

Prerequisites for the Resource Provider Worker to be deployed into other AME subscriptions outside of Resolute PROD:
 - Subscription(s) need to be created before initial deployments.
 - Resource Provider Worker needs to be assigned contributor access to the subscription(s).
 - SubscriptionID(s) need to be documented in the ClusterMetadata.

**NOTES:**
 - What changes need to be made to the resource provider manifest?

## 2. Azure PowerShell and CLI
  - Are there any changes/additions that should be considered around Azure PowerShell and CLI for deployments? 

## 3. Initial Azure Regions to deploy in:
 - West US 2, East US 2, and UK South 
 - These are all Hero regions which are preferred and not subject to the level of capacity constraints Hub and Satellite regions experience.
 - **We are considering going to a subscription per region model to allow for more customer IoMT Connectors (5 per) and Azure resource limits.**

## 4.	Deployment .json Updates:
All json here will need to be updated to reflect IoMT Connector deployments:
 - Health-paas -> deployment -> environmentGroups -> *.json
 - Health-paas -> deployment -> environments -> *.json

 **NOTES:** 
 - If it is only regional infrastructure, then there is no need to update the Deploy-EnvironmentGroup.ps1
 - If there are no globally shared resources, then only update the Deploy-Environment.ps1 or IoMT specific deployment script(s).

## 5. Deployment Script Updates:
The following deployment scripts will need to be updated to reflect IoMT Connector deployments:
 - Health-paas -> deployment -> Deploy-EnvironmentGroup.ps1 - **used to open a new region**
 - Health-paas -> deployment -> Deploy-Environment.ps1 - **used for incremental deployments/updates**

## 6. ARM Manifest:
 - Updates/Changes to the Microsoft.HealthcareApis Azure Manifest would need to be completed:
 - Allow for deployment in selected region(s).

 https://jarvis-west.dc.ad.msft.net/actions -> Environment -> TEST and PROD -> Microsoft.HealthcareApis

## 7. IoMT Connector Regional Infrastructure and Update Steps - Deployed into a Resource Group within AME subscription(s):
1. Deployed via PowerShell + ARM template(s)
    - Azure Storage Account
    - App Service Plan
    - App Service (Web) - Geneva Agent - https://microsofthealth.visualstudio.com/Health/_git/health-iomt-poc/pullrequest/13007
2.	Deployed via PowerShell
     - Cluster metadata updates in the Global DB
     - Service Fabric application settings

## 8. IoMT Connector Customer Infrastructure - Deployed into a Resource Group within AME subscription(s) - Will be handled as part of the Resource Provider worker updates:
1. Event Hubs Namespaces
2. Key Vault
3. Storage Account
4. Stream Analytics Job
5. App Service (Function) - Apps are hosted on shared IoMT Connector Regional Infrastructure App Service Plan.

 ## 9. Service Tree Metadata Updates:
 How do we want to handle the collapsing of the Azure API for IoMT Service Tree service into a component of the FHIR Server Service?
   - Subscriptions and metadata updates.

**NOTES**
 - Do we need to address how variables will be populated in the ARM tempates (e.g. Geneva Agent?

 **Cluster metadata updates in the Global DB:**

**Options:**
 - IoMT Team owned. 
 - Res-Ops owned with information from IoMT Connector Team.

**Service Fabric application settings:**
 - Do we need to extend current script to include IoMT settings?

**Options:**
 - Res-Ops owned with information from IoMT Connector Team.
 - Could be tied into Resource Provider Worker work.

##Project Tracking

###Resource Provider worker updates:
 1. https://microsofthealth.visualstudio.com/Health/_git/health-paas-docs/pullrequest/12961

 ###Add Geneva monitoring arm templates
 1. https://microsofthealth.visualstudio.com/Health/_git/health-iomt-poc/pullrequest/13007

###IoMT Connector User Stories and Tasks:
 1. Initial Public Preview Installation
    - https://microsofthealth.visualstudio.com/Health/_workitems/edit/71682
 2. Add App Service Plan to deployment scripts
    - https://microsofthealth.visualstudio.com/Health/_workitems/edit/73043
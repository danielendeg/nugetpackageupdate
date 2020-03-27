# IoMT Connector – Public Preview - Deployment Project

## 1. Project Tracking

###Resource Provider worker updates:
 1. https://microsofthealth.visualstudio.com/Health/_git/health-paas-docs/pullrequest/12961

 ###Add Geneva monitoring arm templates
 1. https://microsofthealth.visualstudio.com/Health/_git/health-iomt-poc/pullrequest/13007

###IoMT Connector User Stories and Tasks:
 1. Initial Public Preview Installation
    - https://microsofthealth.visualstudio.com/Health/_workitems/edit/71682
 2. Add App Service Plan to deployment scripts
    - https://microsofthealth.visualstudio.com/Health/_workitems/edit/73043

## 2. Regions to deploy in:
 - West US 2, North Central US, and UK South 

## 3. IoMT Connector Regional Infrastructure - Deployed into a Resource Group within AME subscription(s):
1. Deployed via PowerShell + ARM template(s)
    - Azure Storage Account
    - App Service Plan
    - App Service (Web) - Geneva Agent
2.	Deployed via PowerShell
     - Cluster metadata updates in the Global DB
     - Service Fabric application settings

**Cluster metadata updates in the Global DB:**

**Options:**
 - IoMT Team owned. 
 - Res-Ops owned with information from IoMT Connector Team.

**Service Fabric application settings:**
 - Need to extend current script to include IoMT settings

**Options:**
 - Res-Ops owned with information from IoMT Connector Team.
 - Could be tied into RP work.

## 4. IoMT Connector Customer Infrastructure - Deployed into a Resource Group within AME subscription(s) - Will be handled as part of the Resource Provider worker updates:
1. Event Hubs Namespaces
2. Key Vault
3. Storage Account
4. Stream Analytics Job
5. App Service (Function) - Apps are hosted on shared IoMT Connector Regional Infrastructure App Service Plan.

## 6.	Resource Provider Worker:
Current Resource Provider Worker changes/updates will address the deployment of infrastructure in subscriptions outside of Resolute PROD.

Prerequisites for the RP Worker to deploy into other AME subscriptions:
 - AME subscription needs to be created.
 - Resource Provider Worker needs to be assigned contributor access.
 - SubscriptionID needs to be documented in the ClusterMetadata.

**NOTES:**
 - What changes need to be made to the resource provider manifest?

## 7. Azure PowerShell and CLI
  - Are these any changes/additions that should be considered around Azure PowerShell and CLI for these deployments? 

## 8.	Deployment .json Updates:
All json here will need to be updated to reflect IoMT Connector:
 - Health-paas -> deployment -> environmentGroups -> *.json
 - Health-paas -> deployment -> environmens -> *.json

## 9. Deployment Script Updates:
The following deployment scripts will need to be updated to reflect IoMT Connector:
 - Health-paas -> deployment -> Deploy-EnvironmentGroup.ps1 - used to open a new region
 - Health-paas -> deployment -> Deploy-Environment.ps1 - incremental deployments / updates

**NOTES:** 
 - If it is only regional infrastructure, then there is no need to update the Deploy-EnvironmentGroup.ps1
 - If there are no globally shared resources, then only update the Deploy-Environment.ps1 or IoMT specific deployment script(s).

## 10. Azure Manifest:
 - Updates/Changes to the Microsoft.HealthcareApis Azure Manifest would need to be completed:
 - Allow for deployment in selected region(s).

 https://jarvis-west.dc.ad.msft.net/actions -> Environment -> TEST and PROD -> Microsoft.HealthcareApis

 ## 11. Service Tree Metadata Updates:
 How do we want to handle the collapsing of the Azure API for IoMT Service Tree service into a component of the FHIR Server Service?
   - Subscriptions and metadata updates.

**NOTES**
 - Do we need to address how variables will be populated in the ARM tempates (e.g. Geneva Agent?
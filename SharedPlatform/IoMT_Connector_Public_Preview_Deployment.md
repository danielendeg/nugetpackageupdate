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
 - West US 2, North Central US, and UK West 

## 3. IoMT Connector Regional Infrastructure - Deployed into a Resource Group within AME subscription(s):
1. Deployed via PowerShell + ARM template(s)
    - Azure Storage Account
    - App Service Plan
    - App Service (Web) - Geneva Agent
2.	Deployed via PowerShell
     - Cluster metadata updates in the Global DB
     - Service Fabric application settings

## 4. IoMT Connector Customer Infrastructure - Deployed into a Resource Group within AME subscription(s) - Will be handled as part of the Resource Provider worker updates:
1. Event Hubs Namespaces
2. Key Vault
3. Storage Account
4. Stream Analytics Job
5. App Service (Function) - Apps are hosted on shared IoMT Connector Regional Infrastructure App Service Plan.

## NOTE: 

## 5. Service Tree Metadata Updates:
1. How to we want to handle the collapsing of the Azure API for IoMT Service Tree service into a component of the FHIR Server Service?
   - Subscriptions and metadata updates.

## Notes:
### Move the document to Azure DevOps:
 - Better collaboration - Can’t see comments until the individual closes the document
 - May be needed for generalized document but may not necessary for IoMT Connector
 - Encapsulate consolidation – good for purpose of communication. 

### Work needed:
 - Initial roll-out (script)
 - Standing up service clusters etc.
 - Encapsulated in deploy environment group
 - Script – Idempotent
 - Support Powershell and ???
 - Every time a new region is opened
 - Templatize or hardcode region wise?
 - Incremental Deploy environment script
 - Periodic changes coming in from the team
 - Feature on/off capability

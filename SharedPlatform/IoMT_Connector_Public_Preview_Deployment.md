# IoMT Connector – Public Preview - Deployment Project

# The BIG question(s) we should attempt to address during this next meeting:
Is IoMT Connector Team writing the specification(s) for changes to the deployment(s) OR are they responsible for making changes to the actual deployment scripts, ARM templates, tasks, etc.?

Let's determine (on a high-level) the division responsibilities.

## 1. Project Tracking

###Resource Provider worker updates:
 1. https://microsofthealth.visualstudio.com/Health/_git/health-paas-docs/pullrequest/12961

 ##Geneva Add Geneva monitoring arm templates
 1. https://microsofthealth.visualstudio.com/Health/_git/health-iomt-poc/pullrequest/13007

###IoMT Connector User Stories and Tasks:
 1. Initial Public Preview Installation
    - https://microsofthealth.visualstudio.com/Health/_workitems/edit/71682
 2. Add App Service Plan to deployment scripts
    - https://microsofthealth.visualstudio.com/Health/_workitems/edit/73043

 ##2. How many regions to deploy in?
1. 3 
3. West US 2, North Central US, and UK West 

##NOTE: These are the same regions as FHIR Server Public Preview 

## 3. Placeholder

## 4. How do we want to handle the storage account for Function Apps code deployment?
1. Per region
2. Can we use an existing Resolute infrastructure local storage account and just add a container?

## NOTE: Just a few MB ZIP file for the Function Apps code

## 5. When we deploy the IoMT Connector initial regional infrastructure, this is what is deployed within our subscription(s) or changes to existing infrastructure:
1.	Azure Blob Storage 
2.	App Service Plan  
3.	Cluster metadata updates in the Global DB
4.	Service Fabric application settings

## NOTE: Application Insights could be done as part of the initial infrastructure, but our thought was that we would use the existing Azure API for FHIR account.

## 6. When a customer deploys an IoMT Connector, this is the infrastructure that is deployed within our subscription(s):
1. Event Hubs Namespaces
2. Key Vault
3. Storage Account
4. Stream Analytics Job
5. App Service (Function - Hosted on shared IoMT Connector regional App Service Plan)

## NOTE: FHIR Server is required if one does not already exist in the designated Public Preview region(s).

## 7. Azure FHIR Server Manifest PROD updates:
1. Add section that is IoMT Connector specific to help facilitate deploying to specific regions?

## 8. Service Tree Metadata Updates:
1. How to we want to handle the collapsing of the Azure API for IoMT Service Tree service into a component of the FHIR Server Service?
   - Subscriptions and metadata updates.

## 9. Documentation:
1. heath-paas-docs repo -> SharedPlatform folder -> IoMT_Connector_Public_Preview_Deployment (markdown file format)

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

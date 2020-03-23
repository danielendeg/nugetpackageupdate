IoMT Connector – Public Preview

Resource Provider worker updates:
•	https://microsofthealth.visualstudio.com/Health/_git/health-paas-docs/pullrequest/12961

 How many regions to deploy in?
•	3 
•	Same regions as FHIR Server Public Preview
•	West US 2, North Central US, and UK West 

Are we writing the specification for changes to the deployment(s) or making changes to the actual deployment scripts, ARM, tasks, etc.?

How do we want to handle the storage account for Function Apps code deployment?
•	Per region
•	Can we use an existing Resolute infrastructure local storage account and just add a container?

NOTE: Just a few MB ZIP file for the Function Apps code

When we deploy the IoMT Connector initial regional infrastructure:
•	Azure Blob  
•	App Service Plan  
•	Cluster metadata updates in the Global DB
•	Service Fabric application settings

NOTE: Application Insights could be done as part of the initial infrastructure, but our thought was that we would use the existing Azure API for FHIR account.

When a customer deploys the solution, this is what is deployed within our subscriptions:
•	Event Hubs Namespaces
•	Key Vault
•	Storage Account
•	Stream Analytics Job
•	App Service (Function - Hosted on shared IoMT Connector regional App Service Plan)

NOTE: FHIR Server if one does not already exist in the designated Public Preview region(s) or a separate testing FHIR Server is desired.

Azure FHIR Server Manifest PROD updates:
•	Add section that is IoMT Connector specific to help facilitate deploying to specific regions?

Documentation:
•	heath-paas-docs repo
o	SharedPlatform folder
	Markdown file format

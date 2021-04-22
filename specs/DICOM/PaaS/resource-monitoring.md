# Azure Resource Monitoring

## Overview
There are certain limitations on azure resources at different levels (subscription/region/resource group). We will be monitoring our subscriptions to see when we are reaching the limits and then alert so we are able to take measures such as increasing the limit if possible or creating new subscriptions etc.

## How it works
 
The monitoring occurs through a class ([AzureResourceMonitor](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fsrc%2FResourceProviderApplication%2FResourceProviderWorker%2FAzureResourceMonitor.cs)) in the Resource Provider Worker in Health-PaaS that runs every hour. 

>Note: This class is used for both Gen1 and Jupiter, documentation here is specific to Jupiter.

It reads information from a json file ([sample](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fdeployment%2Fworkspaces%2FworkspaceLimits%2Fprod.json))
 which defines which subscriptions to monitor, which resources need alerts, and at what point to alert for the resources. When the AzureResourceMonitor detects that there are more resources than the alert threshold (which we define to be **80% of resource quota** in the json) it emits a critical log into Geneva. There is then a Geneva  workflow automation which looks for the corresponding log and and if it exists then it creates an IcM incident.

Basic flow:

AzureResouceMonitor counts + logs to Geneva -> Workflow automation reads logs -> IcM incident created


### What is currently monitored
We are monitoring the  resources in the Dicom customer subscription. Specifically this includes 
 - Storage Accounts
 - Role Assignments
 - Deployments
 - Traffic manager (currently we do not use traffic manager but we have limits for future use)


[Information on limits](arm-resources-dicom-service.md)

### Supported levels of monitoring
Currently we support monitoring of resources at the levels of 
- Subscription
- Region
- Resource group

When adding the subscription to be monitored each resource type can be monitored at its own level.


## How to onboard a new subscription
Add a new entry in the workspaceSubscriptionLimits list in the correct json in Health-Paas repo.

For prod subscriptions:
- [deployment/workspaces/workspaceLimits/prod.json](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fdeployment%2Fworkspaces%2FworkspaceLimits%2Fprod.json)

For test subscriptions:
- [deployment/workspaces/workspaceLimits/test.json](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fdeployment%2Fworkspaces%2FworkspaceLimits%2Ftest.json)

Need to specify
 - Subscription ID
 - Service that owns the subscription (ex. Dicom)
 - Resources that need to be monitored. For each resource
    - ResourceType
    - Threshold to alert at (should be 80% of max amount)
    - Level the threshold is at (ex 200 storage accounts at region level) can be one of (exact names):
        - `subscription`
        - `region`
        - `resourceGroup`

Example of a json, for each subscription a new entry needs to be added to the workspaceSubscriptionLimits list.

![Resource Monitoring Json](imgs/resource-monitoring-json.png)


For any new subscriptions that are being added make sure to follow the directions in workspace-platform for setting up a new subscription. [subscription-setup.md](https://microsofthealth.visualstudio.com/Health/_git/workspace-platform?path=%2Fdocs%2Fsubscription-setup.md)

 
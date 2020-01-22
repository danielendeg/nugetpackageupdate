[[_TOC_]]

# Business Justification
Customers usually need FHIR de-identification against either a FHIR data response or a FHIR dataset.
* For a FHIR data response data like a json file, users can simply build our De-Id tool and run it in console.
* For big datasets that comes from export result of FHIR server, users can use the libraries and build an extension to ADF to de-identify the data in the cloud from one blob storage to another.

# Scenario
Users can create and run an De-Identification Azure Data Factory with a powershell script. Users need to provide a Data Factory Configuration and log in with their Azure credentials to authorize automatic resource management. Below is an example of user configuration file.
```json
{
    "dataFactoryName": "[Your data factory name]",
    "resourceLocation": "WestUS",
    "batchComputeNodeSize":"compute node size of Azure batch service",
    "sourceStorageAccountName":"[source storage account hosting input data, activity application]",
    "sourceStorageAccountKey":"[source storage account key]",
    "destinationStorageAccountName":"[destination storage account hosting output data]",
    "destinationStorageAccountKey":"[destinationstorage account key]",
    "sourceContainerName": "[source container name]",
    "destinationContainerName": "[destination container name]",
    "activityContainerName":"[container name of custom activity application]"
}
```
# Design
The De-Identification data transformation process on FHIR resources isn't directly supported by Azure Data Factory,
users can create a Custom activity with the De-Identification logic and use that activity in a pipeline. 

![ADF.jpg](/.attachments/ADF-f7f075d6-29ea-4e64-b19b-00b38edba106.jpg)

Here is the De-Identification framework using Azure Data Factory pipeline. 
We will describe the Azure Data Factory integration work as two parts: 
* **Custom Activity** that denotes the De-Identification logic to transform data.
* **Azure Data Factory Deployment** that denotes the Azure resource management logic that create and manages all resources needed by Azure Data Factory.

### Custom Activity
Since our De-Id tool is written with .Net Core framework, we have two transformation options do the integration, the Azure Function activity & the Custom activity. The FHIR dataset might be very large and the De-Identification process might be time consuming. Hence, an Azure Function activity is not suitable for the de-identification task and we utilize custom activity as the core function for ADF De-Identification.
The logic of our De-Identification custom activity is to download all resource blobs from the source Azure blob storage, de-identify all resource files (currently the file Format is *.ndjson*), and upload the de-identified blob to the destination Azure blob storage.

![custom activity.jpg](/.attachments/custom%20activity-bbd3d18c-02f2-4f26-83c6-19b282b418e6.jpg)

### Azure Data Factory Deployment
As shown in the picture of Azure Data Factory Framework, users need to configure several dependent resources along with Azure Data Factory. We provide a Powershell script to help users deploy these resources. We list the core steps for deploying a Azure Data Factory pipeline for FHIR De-Identification:
1. Build the custom activity application and copy the application folder to Azure Blob storage.
2. Create Azure resource group, Azure batch service and Azure Data Factory resources.
3. Run Azure Data Factory pipeline and show pipeline results.

The command to run the deploy script is
```
.\DeployAzureDataFactoryPipeline.ps1 
    [-ConfigFile AzureDataFactorySettings.json]
    [-SubscriptionId a2bd7a81-579e-45e8-8d88-22db48695abd]
    [-RunPipelineOnly]
```
The ConfigFile parameter is the filepath of user configuration and it has a default value of "AzureDataFactorySettings.json". The SubscriptionId parameter enables user to selectwhich subscription to deploy the resources. The RunPipelineOnly parameters can be used when user has deployed all resources and just want to run the De-Identification pipeline.

# Error handling & logging
If an error occured in the de-identfication custom activty, e.g. failures in parses the input resource document or deidetifying resourses, the custom activity will throw an exception and execution result of the ADF pipeline will be Failed.
 
Whenever user runs the pipeline with the script we provided, the execution result will be displayed on the console like
```
[2020-01-22 02:04:20] Pipeline is running...status: InProgress
[2020-01-22 02:04:43] Pipeline is running...status: InProgress
[2020-01-22 02:05:06] Pipeline run finished. The status is: Succeeded

ResourceGroupName : adfdeid2021resourcegroup
DataFactoryName   : adfdeid2021
RunId             : d84a33a0-aceb-4fd1-b37c-3c06c597201b
PipelineName      : AdfDeIdentificationPipeline
LastUpdated       : 1/22/2020 6:04:51 AM
Parameters        : {}
RunStart          : 1/22/2020 6:04:15 AM
RunEnd            : 1/22/2020 6:04:51 AM
DurationInMs      : 35562
Status            : Succeeded
Message           :
Activity 'Output' section:
"exitcode": 0
"outputs": [
  "https://deid.blob.core.windows.net/adfjobs/d04171de-aba5-4f43-a0fc-456a2f004382/output/stdout.txt",
  "https://deid.blob.core.windows.net/adfjobs/d04171de-aba5-4f43-a0fc-456a2f004382/output/stderr.txt"
]
"computeInformation": "{\"account\":\"adfdeid2021batch\",\"poolName\":\"adfpool\",\"vmSize\":\"standard_d1_v2\"}"
"effectiveIntegrationRuntime": "DefaultIntegrationRuntime (West US)"
"executionDuration": 31
"durationInQueue": {
  "integrationRuntimeQueue": 0
}
"billingReference": {
  "activityType": "ExternalActivity",
  "billableDuration": {
    "Managed": 0.016666666666666666
  }
}
```
If the job failed, users can find the detailed outputs and error logs in azure blob storage. These logs can be helpful for monitoring jobs and debugging errors.


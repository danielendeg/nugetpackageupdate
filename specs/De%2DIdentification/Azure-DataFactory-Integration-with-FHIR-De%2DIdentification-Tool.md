[[_TOC_]]

# Business Justification
Customers usually need FHIR de-identification against either a FHIR data response or a FHIR dataset.

For a  FHIR data response data like a json file, users can simply build our De-Id tool and run it with.
```
./FHIR.DeIdentification.Tool.exe resource.json resource.redacted.json
```
But when users want to de-identify a FHIR dataset, which may comes from an *Export* action in FHIR server, it's better for him to have an visualized experience in monitoring the data transform job. Thus, [Azure Data Factory](https://docs.microsoft.com/en-us/azure/data-factory/) is our first choice.

# Scenario
Our scenario is to enable users access FHIR De-Identification Tool through Azure Data Factory. We provide two ways to use Azure Data Factory with De-Id tool, the manual way and the automatic way.
- If a user is quite familiar with Azure Data Factory, he can set up Data Factory in Azure Portal manually with our CustomActivityProject. 
- Users can create and run an De-Identification Azure Data Factory with a powershell script. Users need provide a Data Factory Configuration as the script input and log in with their Azure credentials to authorize automatic resource management. Below is an example of user configuration file.
```json
{
    "dataFactoryName": "[Your data factory name]",
    "resourceLocation": "WestUS",
    "inputContainerName": "[Your input container name]",
    "outputContainerName": "[Your output container name]",
    "storageAccountName":"[Your storage account hosting input, output and activity application]",
    "storageAccountKey":"[Your storage account key]"
}
```
In this spec, we will discuss the second way in details.

# Design
### Azure Data Factory Integration
Azure Data Factory supports different data transformations.
Since our De-Id tool is written against .Net Core, we have two transformation options, the Azure Function activity & the custom Activity. The FHIR dataset might be very large and the De-Identification process might be time consuming. Hence, we exclude Azure Function that is not suitable for [long running tasks](https://docs.microsoft.com/en-us/azure/azure-functions/functions-best-practices). 

On the contrary, custom activity utilize Azure Batch service as computing environment which has adequate computing resources. The job schedule strategy in Azure Batch service is listed below

![Azure Batch Job Schedule Framework](/.attachments/tech_overview_03%20(1)-dcef1066-a5f0-4dee-8ffe-d81406ab20b7.png)

Hence, we design our Azure Data Factory Integration in three steps:
1. Script will build the custom activity application and copy the output folder to Azure Blob storage.
2. Script will create Azure resource group, Azure batch service and Azure Data Factory resources.
3. Script will create and run Azure Data Factory pipeline with generated configurations.

### De-Id custom activity
De-Id custom activity is the core application that performs the FHIR De-Identification task. This activity takes a blob container as input and write the redacted resources to the output container. The execution command is 
```
Fhir.DeIdentification.CustomActivity.exe [-f/--force] // set -f option to overwrite existing blobs in output container
```
The custom activity works like below:
- List all blobs in input container.
- Downloads a resource file.
- Redact the downloaded file.
- Upload the redacted file.


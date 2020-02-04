[[_TOC_]]

# Overview
Azure Function App support customer deploy serverless application with pay-per-use pricing model. With Azure Functions, the cloud infrastructure provides all the up-to-date servers to keep application running at auto scale. Function App also supports long running application with durable mode which is supported by ADF. Customer can use the function app as a standalone tool not only for web request.

# Function App Design
We use http trigger and durable function implement as async API for de-identification task. The logic of the function is to download the single blob file from storage, de-identify resource file and upload result to destination blob.

* Request for start de-identification
```json
{ 
  "InputContainer": "<InputContainer>", 
  "InputFileName": "<InputFileName>",  
  "OutputContainer": "<OutputContainer>"
}
```

* Accept response (Follow durable function pattern)
```json
{
    "id": "<taskid>",
    "statusQueryGetUri": "https://***.azurewebsites.net/runtime/webhooks/durabletask/instances/**?taskHub=DurableFunctionsHub&connection=Storage&code=*****",
    "sendEventPostUri": "***",
    "terminatePostUri": "***",
    "rewindPostUri": "***",
    "purgeHistoryDeleteUri": "***"
}
```

* Result Response
```json
{
  "instanceId": "<taskid>",
  "runtimeStatus": "<durable function status>",
  "output": 
  {
    "OutputContainer": "<OutputContainer>",
    "OutputFileName": "<OutputFileName>"
  }
  # ... <Other properties for durable function result>
}
```

* Failed Result Response
```json
{
    "instanceId":"<taskid>",
    "runtimeStatus":"Failed",
    "output":"Orchestrator function 'TransformFromStorage_Orchestrator' failed: The activity function 'TransformFromStorage' failed: \"Container: fake-container not exist\". See the function execution logs for additional details.",
    "createdTime":"2020-02-04T13:43:46Z",
    "lastUpdatedTime":"2020-02-04T13:43:48Z",
    # ... <Other properties for durable function result>
}
```

Durable function has different timeout for 3 hosting plan. Currently we would expect 10 mins for 1G single file, customer can choose hosting plan based on their usage.
| Hosting Plan  | Default Timeout   | Maximum Timeout   |
| :------------ | :----------:      | -----------:      |
|  Consumption  | 5 min             | 10 min            |
|  Premium      | 30 min            | 60 min            |
|  App Service  | 30 min            | Unlimit           |

* Credentials used by function would be stored in the key vault, and referenced by application configuration through key vault path.
* Support function key authentication. 

# Integrate with ADF Pipeline
In this ADF pipeline, we trigger durable function for each storage blob in the container, the function app would download data from storage and upload the result to destination container. Customer can extend the pipeline with following activities for further ETL & Analysis tasks.

![ADF Pipeline.jpg](/.attachments/ADF%20Pipeline-54653e1f-fab7-40ac-8e1b-ef6418a2e9c9.jpg)

* ADF support activity parallel execution through foreach activity (maxium number == 50). For every blob we can trigger a function call would help accelerate the pipeline execution.
* For credential, we would suggest customer to store the connection string & function key in the key vault, data factory can use key vault linked service to reference the secret. 
* In the pipeline de-identify operation uses same storage account (different containers) for both source and destination to decouple with other operation like copy, transform... 
* Pipeline parameter: <SourceContainer> & <DestinationContainer>. Customer provides the parameters during execution.

# User Scenario
1. Prepare resource

   We would provide scripts for customer to build function and prepare all azure resources. Following resources would be used.
   - Key Vault (Customer provide): Used for store credential.
   - Storage (Customer provide): Used for store resource files (*.ndjson).
   - Function App (Script create): Used for handle De-Id tasks.
   - Data Factory (Script create): Used for De-Id pipeline.

* Commands Sample:
```sh
.\Prepare-Resource.ps1  
    [-Region] <String>
    [-ResourceGroup] <String> 
    [-SecretStore] <String>
    [-StorageConnectionString] <String>
    [-FunctionName] <String>
    [-DataFactoryName] <String>     
```
-SecretStore: the key vault name

2. Run ADF pipeline
```
Invoke-AzDataFactoryV2Pipeline
      [-ResourceGroupName] <String>
      [-DataFactoryName] <String>
      [-PipelineName] <String>
      [-ParameterFile <String>]
```

3. Troubleshot at De-Identification function failure

Customer can find detail error message from ADF Activity result (Web Activity pull status and show error result), here's one sample error:
```json
{
    "errorCode": "2108",
    "message": "{\"name\":\"TransformFromStorage_Orchestrator\",\"instanceId\":\"c92644840fe4451a9045bc11ffd7627d\",\"runtimeStatus\":\"Failed\",\"input\":{\"$type\":\"DeIdentification.TransformRequest, DeIdentification\",\"InputContainer\":\"not-exist-container\",\"InputFileName\":\"not-exist-file\",\"OutputContainer\":\"not-exist-file\"},\"customStatus\":null,\"output\":\"Orchestrator function 'TransformFromStorage_Orchestrator' failed: The activity function 'TransformFromStorage' failed: \\\"Container: fake-container not exist\\\". See the function execution logs for additional details.\",\"createdTime\":\"2020-02-04T13:51:31Z\",\"lastUpdatedTime\":\"2020-02-04T13:51:34Z\"}",
    "failureType": "UserError",
    "target": "DeId-Result-WebActivity",
    "details": []
}
```
For those unexpected error like internal server error and no meaningful infomations in result, details execution logs can be find in function app monitoring page.

# Future Work
1. Single big file performance improvement

   Currently resource file (*.ndjson) might be very large > GB, that cause bad performance for single function call. To improve this we can split the big file into small files and execute in parallel. ADF currently support data flow activity which supports such operation. We can leverage DataFlow Activity integrate with current pipeline to do split and merge operation.

2. Extend Function App to support more trigger
   Function app support blob trigger directly, use function app as a standalone tool with blob trigger can help customer De-Identify resource file backend without any manual operation. 


   







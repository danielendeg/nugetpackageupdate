[[_TOC_]]

# Business Justification
Customers usually need FHIR de-identification against either a FHIR data response or a FHIR dataset.

For a  FHIR data response data like a json file, users can simply build our De-Id tool and run it with.
```
./FHIR.DeIdentification.Tool.exe resource.json resource.redacted.json
```
But when users want to de-identify a FHIR dataset, which may comes from an *Export* action in FHIR server, it's better for him to have an visualized experience in monitoring the data transform job. Thus, [Azure Data Factory](https://docs.microsoft.com/en-us/azure/data-factory/) is our first choice.

# Scenario
Our scenario is to enable users access FHIR De-Identification Tool through Azure Data Factory.

# Design

Azure Data Factory supports different data transformations.
Since our De-Id tool is written against .Net Core, we have two transformation options, the Azure Function activity & the custom Activity. The FHIR dataset might be very large and the De-Identification process might be time consuming. Hence, we exclude Azure Function that is not suitable for [long running tasks](https://docs.microsoft.com/en-us/azure/azure-functions/functions-best-practices). 

On the contrary, custom activity utilize Azure Batch service as computing environment which has adequate computing resources. The job schedule strategy in Azure Batch service is listed below

![Azure Batch Job Schedule Framework](/.attachments/tech_overview_03%20(1)-dcef1066-a5f0-4dee-8ffe-d81406ab20b7.png)




If users are quite familiar with Azure Data Factory, he can setup Data Factory and run tranform pipel

# Tests


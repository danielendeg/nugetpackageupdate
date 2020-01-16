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
Since our De-Id tool is written against .Net Core, we have two transformation options, the Azure Function activity & the custom Activity. Here we exclude Azure Function that is not suitable for long running tasks as the dataset might be very large.

We design the data flow in custom activity as below:


If users are quite familiar with Azure Data Factory, he can setup Data Factory and run tranform pipel

# Tests


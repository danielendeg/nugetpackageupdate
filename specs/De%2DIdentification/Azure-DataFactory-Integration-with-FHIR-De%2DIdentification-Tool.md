Customers usually need FHIR de-identification against either a FHIR data response or a FHIR dataset.

For a  FHIR data response data like a json file, users can simply build our De-Id tool and run it with.
```
./FHIR.DeIdentification.Tool.exe resource.json resource.redacted.json
```
But when users want to de-identify a FHIR dataset, which may comes from an *Export* action in FHIR server, it's better for him to have an visualized experience in monitoring the data transform job. Thus, [Azure Data Factory](https://docs.microsoft.com/en-us/azure/data-factory/) is our first choice.

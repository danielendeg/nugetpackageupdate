# Simple FHIR Validator App

To validate a resource:

```
dotnet run /FileName samples/simple-patient.json
```

Or validate with a specific profile:

```
dotnet run /FileName samples/simple-patient.json /Profile http://microsoft.com/fhir/StructureDefinition/PatientWithNoExtensions
```

Resource StructureDefinitions are read from the basic FHIR specification and any additional definitions added to `profiles/`


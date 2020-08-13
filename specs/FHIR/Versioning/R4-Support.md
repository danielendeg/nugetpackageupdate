[[_TOC_]]

# FHIR R4 Support

This document outlines the approach to supporting multiple versions of the FHIR specification.

To enable this multi-version support several structural changes and dependency updates are required. The fhir-net-api library that this project depends on is still undergoing its own incremental refactoring to slowly decouple the models from common logic such as serialization and FHIRPath evaluation. Some of these refactorings are available as of v1.2 of HL7.FHIR.[Stu3/R4] nuget releases.

## Dependencies

The new and important structural changes in the fhir-net-api is the decoupling of Hl7.Fhir.Serialization, Hl7.Fhir.ElementModel and Hl7.FhirPath from the main Models library. This should enable common code to be written against low-level interfaces such as `ITypedElement` in order to handle FHIR data in common non-version-specific classes.

In future the fhir-net-api project plans to introduce another common layer that will include base FHIR models such as Base, Resource, Meta, and DomainResource. This should make sharing common logic less complicated in future.

### fhir-net-api library structure
```
Hl7.Fhir.[Stu3] v1.2
├── Hl7.Fhir.Serialization
│   └── Hl7.Fhir.ElementModel
├── Hl7.Fhir.Support
└── Hl7.FhirPath
    └── Hl7.Fhir.ElementModel
```

## Project structural changes

To support loading multiple versions of the models, the follow project structual changes are proposed.

```
Microsoft.Health.Fhir
├── src
│   ├── FHIR
│   │   ├── STU3
│   |   │   ├── Microsoft.Health.Fhir.Stu3.Api
│   |   │   ├── Microsoft.Health.Fhir.Stu3.Core
│   │   ├── R4
│   |   │   ├── Microsoft.Health.Fhir.R4.Api
│   |   │   ├── Microsoft.Health.Fhir.R4.Core
│   │   ├── Microsoft.Health.Fhir.Api
│   │   ├── Microsoft.Health.Fhir.Core
│   │   ├── Microsoft.Health.Fhir.CosmosDb
│   ├── Shared
│   |   ├── Microsoft.Health.Abstractions
│   |   ├── Microsoft.Health.*
|   ├── Microsoft.Health.Fhir.Web

```

In the following projects, all references to Hl7.Fhir.[Stu3] must be removed and replaced with one of the base libraries (Hl7.Fhir.Serialization or Hl7.Fhir.ElementModel):

- Microsoft.Health.Fhir.Api
- Microsoft.Health.Fhir.Core
- Microsoft.Health.Fhir.CosmosDb
- Microsoft.Health.Fhir.Web (this will reference Microsoft.Health.Fhir.R4.Api, Microsoft.Health.Fhir.Stu3.Api)

A major update to the code requires removing uses of `Resource`, `DomainResource`, `Base`, `ConformanceStatement` and other hard coded models, to  be replaced with the new underlying interface `IElementModel`.

## Configuration

When configurating the FHIR server, only a single FHIR version will be supported, these changes might look something like:

```
services.AddFhirServerR4(Configuration).AddFhirServerCosmosDb(Configuration);
```

## Conformance statement

Conformance, validation and the conformance statement will differ between versions. All concrete classes related to a specific version will be moved into the spec specific library, i.e. Microsoft.Health.Fhir.R4.Core.

Given that many features in the FHIR Server check if a feature is turned on or specified in the conformance document, some interfaces and abstrations will need to be created and utilized in Core code.

## Validation

Validation will be version specific, validators that utilize the correct model versions will exist in the spec version libraries.

## Testing

Unit tests: If specific logic for a spec version is created then tests can be created in the corresponding Microsoft.Health.Fhir.R4.Core.UnitTests library.

E2E: To give us full coverage and compatability matrix the new enumerations in the E2E test suite can be utilized. 
```
[HttpIntegrationFixtureArgumentSets(DataStore.CosmosDb, Format.Json | Format.Xml, FhirSpecification.R4)]
```
Given that resources might have either changed for be completely new, these tests should load sample data from a new `Microsoft.Health.Fhir.Tests.Common.TestFiles.R4` directory based on the enum configuration.

## Out of scope
- Conversions between resource versions
- Supporting more than 1 spec version with a single instance

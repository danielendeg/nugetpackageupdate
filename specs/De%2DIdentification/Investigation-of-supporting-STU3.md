[[_TOC_]]

# STU3 and R4
STU3: last updated on 2019-10-24 with v3.0.2.

R4: last updated on 2019-10-30 with v4.0.1.

R4 compare to R3:
- Some content marked [Normative](https://www.hl7.org/fhir/versions.html#std-process)
- 34 new resources
- 5 renamed resources
- 2 deleted resources
- Datatypes: rework _Money_ and add _Expression_
- Fields: [_Appointment.reason_](http://hl7.org/fhir/STU3/appointment.html) vs [_Appoint.reasonCode_, _Appointment.reasonReference_](http://hl7.org/fhir/appointment.html), etc.

# Implementation to support STU3

## Core support
There is a version specific package dependency in our code: Hl7.Fhir.Stu3 or Hl7.Fhir.R4.

Firstly, we'll create a shared project to host shared codes of STU3 and R4.
Based on the shared project, we'll create projects for STU3 and R4 to host corresponding version specific packages, respectively.
The shared project does not have any output (in DLL form).
Instead, the code is compiled into each project that references it.
So Anonymizer will generate two sets of binaries, one for STU3 and one for R4.
Shared project is also used in FHIR Server.

Classes that Anonymizer use from the version specific library.
Most of them are related with resource definitions, parsing and validation, which can differ in different versions.
- AnonymizerEngine: FhirJsonParser, Resource, ToPoco, FhirJsonSerializationSettings
- Extensions
  - ElementNodeOperationExtensions: PocoStructureDefinitionSummaryProvider, ResourceType, Meta, ToPoco
- Models
  - SecurityLabels: Coding
- Validation
  - AttributeValidator: Validation, Resource, DotNetAttributeValidation
  - ResourceValidator: Resource

## Unit tests and functional tests support
Same method will be applied to test projects.

## Tool support
Same method will be applied to local tool.

Different sample configurations will be attached to STU3 and R4.
After building Anonymizer, there will be two sets of binaries, one for STU3 and one for R4.

## ADF support
Same method will be applied to ADF.

## Sample configuration support
We'll create a sample configuration file for STU3, like the one we do for R4.

# Effort estimation
Total effort estimation: around 1 sprint (PaaS integration not included)
- Codes: 3 story points.
  - Implementation, including core, test projects, tool and ADF.
  - Code review.
- Sample configuration: 2 story points.
- Testing:
  - 1-2 story points for functional tests, including different options for tool (e.g., --validate) and ADF deployment tests.
  - 3-5 story points for 1 testing vendor.
- PaaS integration:
It depends.
With more understanding of integrating R4 into PaaS, we can get more clear about the effort of STU3.

# References
- Publication history: [http://hl7.org/fhir/directory.html](http://hl7.org/fhir/directory.html)
- Version History: [https://www.hl7.org/fhir/history.html](https://www.hl7.org/fhir/history.html)

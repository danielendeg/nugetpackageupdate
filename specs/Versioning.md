Describe the feature

[[_TOC_]]

# FHIR v4.0.0

## Description
Customers should be able to select which version of the FHIR spec they would like to deploy.

The version that is currently available for customers to deploy on Azure API for FHIR is version v3.0.1. 
As of 4/4/2019, the latest version of the FHIR specification is version v4.0.0 and it is not available for customers to deploy on Azure API for FHIR

Between Release 3 and Release 4, nearly 3000 change proposals were applied to the specification, including >1000 substantive changes, of which 339 were labeled 'non-compatible'.
You can find the differences between Release 3 and Release 4 in the version history <https://www.hl7.org/fhir/history.html>

A customer may choose to deploy Azure API for FHIR in the FHIR Release 3 spec, if the source data they plan to migrate is in the same schema
A customer may choose to deploy Azure API for FHIR in the FHIR Release 4 spec, if they plan to start fresh in  the lastest spec with Release 4

The versions that are in scope today are v3.0.1 and v4.0.0 only

## Requirements
### Selecting the version of the FHIR 
Customers should be able to select which version of the FHIR specificaion they would like to deploy when creating Azure API for FHIR. 
The option to make this selection should be offered in the "Additional Selections" tab
The field should be name "FHIR version"
The field should be a required field
All version options should be made available via a drop downbox and should be listed in descending order
The default selection should be the lastest version offered and should be labeled as such
The tooltip should read "The version of FHIR to be provisioned. You will not be able to migrate between versions. View FHIR version history <https://hl7.org/fhir/history.html>"

<SCREENSHOT>


## Test Strategy
It should be validated that the version selected is the one deployed


## Security
N/A


## Out of Scope
- Allowing the customers to deploy versions older than v3.0.1 of the FHIR spec: There is no customer demand for this and as such DSTU1 and DSTU2 will not be supported.

- Allowing customers to migrate from a newer version to an older version of the FHIR spec: There is no customer demand for this and as such it is out of scope.

- Allowing customers to migrate from an older version to a newer version of the FHIR spec: Given the complexity this feature, especially given that "339 were labeled 'non-compatible'", it is out of scope for now. We may revist this if required at a later time.




Describe any impact to localization, globalization, deployment, back-compat, SOPs, ISMS, etc.


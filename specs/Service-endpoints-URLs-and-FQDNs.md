# Service endpoints (URLs and FQDNs)

## TL;DR

The purpose of this document is to provide guidance on the Fully Qualified Domain Name (FQDN) and Uniform Resource Locator (URL) naming of the current and future services of the Azure Healthcare Platform.

Currently when a customer instantiates Azure API for FHIR account, we generate an endpoint that has FQDN  of [accountname.azurehealthcareapis.com](https://accountname.azurehealthcareapis.com). This endpoint represents FHIR server. As we are building new services/features on the platform (DICOM, IoMT, genomics, Conversion APIs, Tools for Anonymization…), we need to have a guidance on how we would generate service endpoints that would be consistent across services.

Current Domain Name of the service is azurehealthcareapis.com, which is generic enough that could support all future services.
When we think about service FQDNs and URLs we need to consider several aspects:

* Backward compatibility with existing naming
* Future services and how they fit into the naming scheme
* Support for the custom domain – Bring Your Own Domain (BYOD)

Currently we are considering between four options:

* https://**accountname**.azurehealthcareapis.com/**service**
* https://**accountname**.**service**.azurehealthcareapis.com/
* https://**my-organization**.azurehealthcareapis.com/_**service**
* https://**my-organization**.azurehealthcareapis.com/**service**/**accountname**

Where **service** represents one of the core services (fhir, dicom, genomics) and **accountname** represents assigned account name of the service endpoint

### Option 1: https://**accountname**.azurehealthcareapis.com/**service**

This is the current model for service naming, where we create endpoint **accountname.azurehealthcareapis.com** and map the root to the FHIR Server. Moving forward we could (for backward compatibility) still map the FHIR server to root URL, but then ass a **service** path to url (fhir, dicom,...).

Example would be: https://account1.azurehealthcareapis.com/fhir, where for backward compatibility we could have FHIR server mapped to the root https://account1.azurehealthcareapis.com, and expose same FHIR server also at path https://account1.azurehealthcareapis.com/fhir and DICOM server at https://account1.azurehealthcareapis.com/dicom

* Pro: Easy backward compatibility
* Pro: Consistent naming with existing naming scheme
* Pro: No change in existing TLS certificates as FQDN stays the same
* Con: Does not allow adding new **services** endpoint under the same account
* Con: More complex routing between **services** as we would need to parse the url

### Option 2: https://**accountname**.**service**.azurehealthcareapis.com/

In this model, we would break services at the FQDN level, where we would add **service** as part of the FQDN (fhir, dicom...). This would still allow us to mantain backward compatibility by still creating a legacy endpoint that would map to FHIR service.

Example of this would be: https://account1.fhir.azurehealthcareapis.com, https://account1.dicom.azurehealthcareapis.com

* Pro: Service routing is easy on the domain level
* Pro: Good backward compatibility
* Con: Does not allow adding new **services** endpoint under the same account
* Con: Modifications of our TLS certificates 

### Option 3 & 4: https://**my-organization**.azurehealthcareapis.com/_**service** or https://**my-organization**.azurehealthcareapis.com/**service**/**accountname**

This two options are more complex one as they introduce the concept of Organization or Workbook or Project etc... The concept is that a customer can create a collection of services under common naming scheme. This could be good if we would allow a customer to create new endpoints of existing services or new services under the same URL.

Example: https://project1.azurehealthcareapis.com/fhir/service1, https://project1.azurehealthcareapis.com/fhir/service1-deid, https://project1.azurehealthcareapis.com/dicom/service1

* Pro: Allows new ways to combine the services under the same organization, project, workbook,...
* Pro: Flexible naming scheme for including de-id endpoints
* Con: More complex to maintain backward compatibility
* Con: More complex routing between **services** as we would need to parse the url

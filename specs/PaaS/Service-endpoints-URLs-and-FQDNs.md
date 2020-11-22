# Service endpoints (URLs and FQDNs)

## TL;DR

The purpose of this document is to provide guidance on the Fully Qualified Domain Name (FQDN) and Uniform Resource Locator (URL) naming of the current and future services of the Azure Healthcare Platform.

Currently when a customer instantiates Azure API for FHIR account, we generate an endpoint that has FQDN  of [accountname.azurehealthcareapis.com](https://accountname.azurehealthcareapis.com). This endpoint represents FHIR server. As we are building new services/features on the platform (DICOM, IoMT, genomics, Conversion APIs, Tools for Anonymization…), we need to have a guidance on how we would generate service endpoints that would be consistent across services.

Current Domain Name of the service is azurehealthcareapis.com, which is generic enough that could support all future services.
When we think about service FQDNs and URLs we need to consider several aspects:

* Backward compatibility with existing naming
* Future services and how they fit into the naming scheme
* Support for the custom domain – Bring Your Own Domain (BYOD)

Currently we are considering between three options:

1. https://**workspace**.azurehealthcareapis.com/**service**/**dataset**
1. https://**workspace**.**service**.azurehealthcareapis.com/**dataset**
1. https://**workspace-dataset**.**service**.azurehealthcareapis.com/

Where **service** represents one of the core services (fhir, dicom, genomics, iot) and **workspace** represents workspace name and **dataset** represents specific dataset (named instance of the service).

There are currently two major limitations for our FQDN/URLs:

* SSL certificates can only support only one wild card subdomain.  This effectively prevents another option where the dataset could be a subdomain (https://**dataset**.**workspace**.**service**.azurehealthcareapis.com/**dataset**).  To support this today we would need to create SSL certificates for each workspace the customer creates.  At this the time the automation for production certificates to support this does not exist.
* SMART on FHIR requires the audience and the service URL match.  This is an issue for option 1 & 2 because AAD doesn't support wildcards in the path.  We are currently following up with the AAD team to see if that is a feature on their roadmap.

### Option 1: https://**workspace**.azurehealthcareapis.com/**service**/**dataset**

This is the current model for service naming, where we create endpoint **account.azurehealthcareapis.com** and map the root to the FHIR Server. Moving forward we could (for backward compatibility) still map the FHIR server to root URL, but then ass a **service** path to url (fhir, dicom,...).

Example would be: https://account1.azurehealthcareapis.com/fhir/dataset1, where for backward compatibility we could have FHIR server mapped to the root https://account1.azurehealthcareapis.com, and expose same FHIR server also at path https://account1.azurehealthcareapis.com/fhir/dataset1 and DICOM server at https://account1.azurehealthcareapis.com/dicom/dataset2

* Pro: Easy backward compatibility
* Pro: Consistent naming with existing naming scheme
* Pro: No change in existing TLS certificates as FQDN stays the same
* Con: Does not allow adding new **services** endpoint under the same account
* Con: More complex routing between **services** as we would need to parse the url
* Con: Breaks SMART on FHIR

### Option 2: https://**workspace**.**service**.azurehealthcareapis.com/**dataset**

In this model, we would break services at the FQDN level, where we would add **service** as part of the FQDN (fhir, dicom...). This would still allow us to maintain backward compatibility by still creating a legacy endpoint that would map to FHIR service.

Example of this would be: https://account1.fhir.azurehealthcareapis.com/dataset1, https://account1.dicom.azurehealthcareapis.com/dataset2

* Pro: Service routing is easy on the domain level
* Pro: Good backward compatibility
* Con: Does not allow adding new **services** endpoint under the same account
* Con: Modifications of our TLS certificates
* Con: Breaks SMART on FHIR

### Option 3: https://**workspace-dataset**.**service**.azurehealthcareapis.com/

Option 3 is a modification of option two that includes the dataset concatenated with the workspace to form the subdomain.

Example of this would be: https://account1-dataset1.fhir.azurehealthcareapis.com/dataset1, https://account1-dataset2.dicom.azurehealthcareapis.com

* Pro: Service routing is easy on the domain level
* Pro: Good backward compatibility
* Pro: Works with existing AAD constraints
* Con: Does not allow adding new **services** endpoint under the same account
* Con: Modifications of our TLS certificates
* Con: Non-standard URL pattern.

### Recommendation

At this time we are preceding with **option 3** as the recommendation. Workspaces will be globally unique.  In addition hyphens will be prohibited characters for workspace names to prevent collisions between different hyphenated workspace and dataset combinations.  We are planning on keeping the azurehealthcareapis.com domain for now though this may change prior to release.  The inclusion of the service in the FQDN will prevent collisions between Gen 1 FHIR services and the Gen 2 services.

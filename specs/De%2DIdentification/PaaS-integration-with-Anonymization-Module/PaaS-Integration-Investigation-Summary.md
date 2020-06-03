
**Summary**

This document summarizes current proposed solution for Anonymization integration with FHIR Managed Service. In this design, the service keeps separate anonymized dataset in DB while providing Anonymized View through its controller and needed provisioning functions. 

**Architecture**

This diagram shows the current design.

![duplicate-data-design.jpg](/.attachments/duplicate-data-design-bbcb7123-033a-4fbf-bcac-ddf6d158713f.jpg)


New anonymized data set get created and indexed during provisioning time with its own configuration. When ready, users can access the anonymized data through FHIR API with context based controller support. 


**Design Assumptions**

Integration with OSS version of the FHIR server is not immediate goal. Most of integration will happen at PaaS code base. 

Limits of total number of anonymized views per customer is 24 for now. 

For now, this design assumes that data synchronization needs is low.  


**Provisioning UX**

Provisioning User Interface for managing Anonymized Virtual Endpoints will happen through both CLI and its ARM [page](https://microsoft.sharepoint.com/:w:/r/teams/ProjectResolute/_layouts/15/doc2.aspx?sourcedoc=%7BDB436EFD-C5E1-4026-AC3F-F4B55BCCCB41%7D&file=UI%20Spec.docx&action=default&mobileredirect=true&cid=07db57d2-b058-4509-a0fb-9b1291548179).  


**Provisioning API**

Provision, Cancel, List, Get, Delete operations will be supported. With success provision, an anonymized view name/id will be generated. The name/id will be used in URL for accessing anonymized data. 

![architecture.jpg](/.attachments/architecture-9f06fd86-925c-41ec-8686-44426d5b4c59.jpg)

**Backend Provisioning Process**

The backend provisioning process follows similiar design as $Export. It has worker service to handle anonymization process on chosen dataset. It may use BulkImport if it's available. 


**Cosmos DB Integration**

Further investigation needed on pricing model with options of shared RU or multiple RU. 


**Anonymized View Controller and RBAC integration**

Call routing and URL rewrite is needed for Anonymized view data access API calls to get through authorization and locate right dataset with view name/id. RBAC phase1 shall the scenario with pre-defined scope of authorization.  Further discussion needed on how to leverage RBAC phase2 data slice for the scenario. 


**Monitoring, Logging and Audits**

Monitoring mechanism on newly added facing Restful APIs will be provided by integrating with exiting monitoring system. Proper logs and Audits information will be added for trouble shooting and pricing purposes.


**Deployment**

Need to support "OneBox", Test / Dev environment. 


**Dependencies and integration points**

Codebase of OSS FHIR Server - necessary change to support DB will be needed.

Codebase of Managed Service - most of the code changes will happen in this codebase. 

CLI - python lib for build time. CLI team support.

ARM - ARM onboarding support needed to register new provisioning APIs on ARM

RP - Needed change on Resource Provider and add service auth for the purpose. 

FHIR Operation Store - changes are needed for background Anonymization process




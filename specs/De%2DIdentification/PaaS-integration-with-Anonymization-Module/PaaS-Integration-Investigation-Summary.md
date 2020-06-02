## Investigation Summary on Anonymization Integration with FHIR Managed Service (PaaS)

**Summary**

This document summarizes current proposed solution for Anonymization integration with FHIR Managed Service. In this design, the service keeps seperate anonymized dataset in DB while providing Anonymized View through its controller and needed provisioning functions. 

**Architecture**

This diagram shows the current design.

![Diagram](duplicate-data-design.jpg)


New anonymized data set get created and indexed during provisioning time wtih its own configuraiton. When ready, users can access the anonymized data through FHIR API with context based controller support. 


**Design Assumptions**

Integration with OSS version of the FHIR server is not immediate goal. Most of integration will happen at PaaS code base. 

Limits of total number of anonymized views per customer is 24 for now. 

Bulk Import support, current design assumes that Bulk Import support will be ready, even though there is workaround without the feature. 

For now, this design does not cover Data Synchronized yet. 


**Provisioning UX**

Provisioning User Interface for managing Anonymized Virtual Endpoints will happen through both CLI and its ARM [page](https://microsoft.sharepoint.com/:w:/r/teams/ProjectResolute/_layouts/15/doc2.aspx?sourcedoc=%7BDB436EFD-C5E1-4026-AC3F-F4B55BCCCB41%7D&file=UI%20Spec.docx&action=default&mobileredirect=true&cid=07db57d2-b058-4509-a0fb-9b1291548179).  


**Provisioning API**

Provision, Cancel, List, Get, Delete operations will be supported. With success provision, an anonymized view name/id will be generated. The name/id will be used in URL for accessing anonymized data. 

![Diagram](paas-provision.jpg)


**Backend Provisioning Process**

The backend provisioning process follows similiar design as $Export. It has worker service to handle anonymization process on chosen dataset. It may use BulkImport if it's available. 


**Cosmos DB Integration**

Futher investigation needed on pricing model with options of shared RU or multiple RU. 


**Anonymized View Controller and RBAC integration**

Call routing and URL rewrite is needed for Anonynimzed view data access API calls to get through authorization and locate right dataset with view name/id. RBAC phase1 shall the scenario with pre-defined scope of authorization.  Further discussion needed on how to leverage RBAC phase2 dataslice for the scenario. 


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


** Appendix Cost Estimate **

Features	Weeks
Provision and Cancel op 	2
List, Get, Delete	2
Integration with provision	1
Integration with RP	1
	
RP Change (Service + Wokrer)	2
Integration with ARM	1
	
ARM onboard + reviews	1
	
Provision UX	3
Provision UX integration	1
	
Provision CLI	2
CLI Integration	1
	
RBAC Authenticate & Authorization support	3
Security review	1
	
Engineering core lib, nuget, branch 	1
	
OSS FHIR change for store and export	2
PaaS support anonymized view FHIR API - routing	2
	
Backend - Export and Anonymization Processs	2
Backend - Anoymized Export	1
Backend - Store Q management and optimization	1
	
Backend - Bulk Import	2
Performance tuning and stress	2
	
Controller for Anonymized View	2
	
Search Integration 	1
Export Integration	1
	
Core - Anonymization Engine - Validation of Config	1
Core - Anonymization Engine - Consistency with FHIR Server	1
	
Data Insulation & RU management	2
Compute Isolation	1
	
OneBox Support	1
Performance 	2
	
Logginng	2
Monitoring	1
	
deployment - test	1
deployment - staging	1
deployment - production	1
	
Total without buffer    52
	
	
Total with buffer	68

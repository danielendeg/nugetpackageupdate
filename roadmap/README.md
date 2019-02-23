# Roadmap

## Immediate Roadmap

1. Billing:
    1. Fixed 1000 RUs
    1. Query commerce engine
    1. Egress charges
    1. Some charges for compute layer (could come later)

1. Ability to configure Authority/Audience and CosmosDB throughput

    The customers expect to be able to select/change the AAD tenant and set the Audience. This is required for SMART on FHIR (see below). We have the capability to set these things currently, but there is ARM and Portal work. Similarly, for CosmosDB throughput. 

1. CORS

    Customers expect to be able to write single page apps backed by Azure API for FHIR. We have current partners that are blocked by this.

1. SMART on FHIR in PaaS

    This functionality is already implemented and advertised. We should unblock the use of it urgently since it is a differentiator. In addition to providing SMART on FHIR compatibility we also enable (combined with 3.) CORS on Azure AD, which is another blocker for people using Azure AD for FHIR applications.
  
1. Bulk FHIR `$export`

1. Bulk FHIR `$import`

1. Control Plane

    We are far along with the work on providing a control plane, we should expose this control plane as originally planned. It is needed for identity and RBAC.  

1. Bring your own identity and basic RBAC

    Customers will want to bring their own identity provider (AAD B2C, Okta, Auth0, Authy, etc.). Once the control plane is exposed, we can start building out these capabilities.  

## Next Steps

1. SQL Persistence Provider

    Chained searches, transactions, and FHIR service use cases that include population-based queries, e.g. for analytics or machine learning, need queries that do joins across multiple resources.

1. Transactions, Batch

    It is a critical component of bulk data ingest to be able to accept a FHIR bundle of type "transaction" and/or "batch" at the base URL and have all (or none) the resources in the bundle persisted in a transactional fashion.

1. Versioning R4, etc.

1. Subscriptions (event triggers)

    Customers have a need to set up subscriptions/triggers. They are essential in scenarios where customer/provider apps may update resources and these updates need to be communicated to other systems, e.g. emitted as an HL7 v2 message to alert an EMR. Another use case is where registration of a new patient or encounter should activate a decision support system and possible generate risk assessments. The Subscription (https://www.hl7.org/fhir/subscription.html) specification is one option, but we should investigate others. We could link the service to event hub or event grid.

1. Profiling and Extensions

    Customers need the ability to define [profiles](https://www.hl7.org/fhir/profiling.html), i.e. specify for each resource which field can, must, should be there. In addition, there is a need to specify which extensions are searchable. We need to provide an interface for defining profiles and search parameters. 

1. Advanced Role Based Access Control

    The plans for RBAC include the ability to assign users (and groups) to roles and for roles to define access policies; including resource level template expressions.

1. Azure Monitor / Audit Logs

    Customers should have access to their FHIR service audit logs.  The PaaS currently writes audit logs to IFxAudit, which is internally accessible to Resolute engineers, but we should integrate the audit logs with Azure Monitor so customers can access them directly.

1. Azure Government

    The OSS code deploys into Azure Government, but we need to have CI/CD set up for this scenario to ensure it doesn’t break. We also need to make plans for making the managed PaaS offering available in Azure Government for customers such as VA, CDC, FDA, NIH, SLGs, etc.  

1. DICOM (DICOMWeb)

    Customers want to ingest DICOM data. The goal should be to have metadata extracted into FHIR resource extensions and have those extensions indexed for search. We need provide a DICOMWeb endpoint on the service, there are some places we can look to start (https://github.com/DICOMcloud/). The image files would be persisted in (and served from) BLOB storage.

1. Internal consistency

    At the moment, we allow resources to have internal references to resources that don’t exist. While this may be desirable in some cases, it should be optional, and default should be that we reject resources that reference non-existing internal references.

1. VNet isolation

    Customers want to be able to isolate access to a virtual network. Either using a model like App Service Environment or just VNET service endpoint. The use case is for customers to connect to the service via express route. The service should not be accessible from the internet. VNET service endpoints and IP filtering could be used to achieve the same although some customers would still be anxious about have the service be routable from the internet.

1. Custom domain names in PaaS

    Customers expect to be able to add their own custom domain names (and SSL certs) in the same way that they would on a web app.

1. PaaS Provisioning/Deprovisioning time

    Currently provisioning is on the order of 5 minutes and deprovisioning is >12 minutes. In order to reduce this, we may need to preprovision Cosmos accounts and possibly recycle Cosmos accounts when deprovisioning.  

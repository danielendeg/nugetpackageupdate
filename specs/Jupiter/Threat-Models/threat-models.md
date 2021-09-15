# Jupiter/Workspace Threat Models

## Shared - Control Plane
[Threat Model](Shared-Control-Plane.tm7) Collection of common threat models for the Workspace/Jupiter platform.

### Common Resource/Service Provisioning
Describes common provisiong workflow of the service.

### ARM Integration
Describes authenication with ARM.

### Service Telemetry
Descrbies the collection of common service telemetry.

### Provision Customer Managed Identity
Describes common process for provisioning customer managed identity through ARM.

### Rotate Managed Indentity
Describes rotation of credentials for customer managed identities used by the service.

### Service Fabric Deployment
Describes common process for deploying updates to the Service Fabric based infrastructure.

## Service - SQL Integration
[Threat Model](SQL-Integration.tm7) Threat models for SQL integration for control and data plane requests for the Workspace/Jupiter platform.

### FHIR SQL Provisioning
Describes the FHIR service SQL provisioning flow.

### FHIR SQL Data Plane Requests
Describes the FHIR service SQL querying flow.

## Service - Schema Management
[Threat Model](Service-Schema-Management.tm7) Threat models for SQL schema management for the Workspace/Jupiter platform.

### Fhir Schema Management
Describes the FHIR service SQL schema management flow.

### DICOM Schema Management
Describes the DICOM service SQL schema management flow.

## Service - FHIR
[Threat Model](Service-FHIR.tm7)

### FHIR
Describes core FHIR Service.

### FHIR Auth
Describes how authentication into the FHIR service works.

### FHIR Auth Managed Identity
Decribes how authentication into the FHIR service works using a customer MI.

### Provisioning 
Describes FHIR service specific items related to the provisioning of the service.  Broader model documented in Shared - Control Plane: Common Resource/Service Provisioning.

## Service - DICOM
[Threat Model](Service-DICOM.tm7)

### DICOM
Describes core DICOM Service.

### Inside AKS
Describes inner workings of DICOM service and interactions between the DICOM service and Kubernetes.

### Provisioning
Describes DICOM service specific items related to the provisioning of the service.  Broader model documented in Shared - Control Plane: Common Resource/Service Provisioning.

## Service - IoT Connector
[Threat Model](Service-IotConnector.tm7)

### IoT Connector
Describes core IoT Connector Service.

### Get Customer Managed Identity
Describes how customer managed identity is used to connector source and destination resources.

### Provisioning
Describes IoT Connector service specific items related to the provisioning of the service.  Broader model documented in Shared - Control Plane: Common Resource/Service Provisioning.

## SQL LoadBalancer
[Threat Model](SQL-LoadBalancer.tm7) Threat models for SQL Load balancer for the Workspace/Jupiter platform.
# Data Flow Diagrams

This document contains Data Flow Diagrams (DFD) for C+AI privacy reviews.  For reference, see [Enterprise Online Services Data Taxonomy](https://aka.ms/EntOnlineSvcDataTaxonomy) and [DFD examples](http://aka.ms/privacyreview/dfdexamples).  To upload the DFD in the privacy manager tool, open the mermaid diagram in a viewer like ADO wiki or VS Code, and take a screenshot of the rendered diagram.

## Resource Provider, FHIR Service, and Service Fabric
::: mermaid
flowchart LR

subgraph Customer
azdns[Azure DNS]<-->|OII| client 
aztm[Azure Traffic Manager] <-->|OII| client
aad[Azure AD] <-->|Customer Content, OII| client
end

subgraph sf[Service Fabric]
client <-->|Customer Content, EUII, EUPI, OII| fesvc[Frontend Svc]
fesvc <-->|Customer Content, EUII, EUPI, OII| fhirsvc[FHIR Svc]
fesvc <-->|OII| arsvc[Account Routing Svc]
fesvc <-->|Customer Content, EUII, EUPI, OII| rp[Resource Provider]
sfmgr[SF Manager]
end

sfmgr -->|OII, System Metadata| sflogs[Azure Storage - SF logs]
sfmgr -->|OII, System Metadata| geneva[Geneva]

fesvc -->|OII, System Metadata| geneva

rp <-->|OII, Access Control Data| akv[Azure Key Vault]
rp <-->|Customer Content, EUII, EUPI, OII, System Metadata| globaldb[Azure Cosmos DB - metadata]
rp <-->|OII, System Metadata| arm[Azure Resource Manager]
rp -->|EUII, EUPI, OII, System Metadata| geneva
rp -->|OII| billingstorage[Azure Storage - Billing]

arsvc <-->|OII, Access Control Data| akv
globaldb -->|OII, System Metadata| arsvc
arsvc -->|OII, System Metadata| geneva

fhirsvc <-->|Customer Content, EUPI| fhirdbsql[Azure SQL DB - FHIR]
fhirsvc <-->|Customer Content, EUPI| fhirdbcosmos[Azure Cosmos DB - FHIR]
fhirsvc -->|Customer Content, EUII, EUPI, OII, System Metadata| geneva
fhirdbsql -->|OII, System Metadata| valogs[Azure Storage - SQL VA logs]

geneva -->|OII, System Metadata| kusto[Kusto] -->|OII, System Metadata| bisql[Azure SQL DB - BI]

akv -->|OII, System Metadata| loganalytics[Log Analytics]
globaldb -->|OII, System Metadata| loganalytics
fhirdbcosmos -->|OII, System Metadata| loganalytics
fhirdbsql -->|OII, System Metadata| loganalytics
valogs -->|OII, System Metadata| loganalytics
sflogs -->|OII, System Metadata| loganalytics
billingstorage -->|OII, System Metadata| loganalytics
:::

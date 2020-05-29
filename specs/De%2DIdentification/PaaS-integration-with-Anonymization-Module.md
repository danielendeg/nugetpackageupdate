# PaaS integration with Anonymization Module

Azure API for FHIR enables users to ingest, manage and exchange clinical health data in the FHIR format on Azure. As there are large amounts of individually identifiable health information in health data, data owners may want to anonymize these data before exchanging them with other teams. To meet this need, we introduce an anonymization module in Azure API for FHIR .

## User Scenarios
- _Administrators_ are able to **create** multiple anonymized views on Azure Portal or CLI. Anonymized Views are a set of anonymized endpoints with possibly different anonymization configurations. Endpoint formats like `https://<base>/Anonymized-view/<view-name>`. 
- _Administrators_ are able to **control access** of anonymized views with Azure RBAC.
Multiple customers can be assigned access to an anonymized view.
- _Customers_ who have the access to an anonymized view are provided with anonymized data according to specific configuration.
- _Customers_ can use APIs supported by FHIR Server (e.g., search, export). Actions with write permissions are not allowed (e.g., create, delete).
- _Administrators_ are able to **check** if anonymized views are still creating or already created.
- **\***When anonymized views are created, _Administrators_ are able to **manage** (e.g., view, update, delete, enable, disable) these views.
- **\*** When anonymized views are created, administrators are able to **synchronize data** to specific anonymized views.



# Anonymized View Management API
For anonymized view we would have management APIs: Provision, Get, List, Update and Delete.

## Authentication and authorization on anonymizated view management APIs
Anonymized view management APIs require different roles than FHIR APIs. 
Similar to settings API, new role "fhirServerAnonymizedViewAdmin" would be added to authorize Anonymized View Management requests.

## Provision Anonymized View
For provision anonymized view API, new collection would be created from base collection with anonymized data. 
It should be a heavy operation and should follow async pattern.
Here's the invoke flow of provision operation:

# ![ProvisionFlow.jpg](/.attachments/ProvisionFlow-ec687a4e-4bb5-44ef-ab04-d6f0b949d85d.jpg)

- User must be able to call `POST [base]/anonymized-view` to provision new anonymized view.
- User must specify the `anonymized-view-name` and `anonymization-config` in the request body.
   `anonymized-view-name` would be string less than 32 characters and can only contains a-z, A-Z, 0-9 and '-'
   `anonymization-config` would be a standard config, details in [here](https://github.com/microsoft/FHIR-Tools-for-Anonymization#configuration-file-format)

   Sample request payload:
```json
{
    "anonymized-view-name": "<<name>>",
    "anonymization-config": {
        "fhirPathRules": [
            { "path": "Patient.name", "method": "redact"}
        ],
        "cryptoHashKey": "XXX"
    }
}
```

- User must have privilege to queue a provision job. If user does not have the corresponding privilege, then `403 Forbidden` should be returned.
- API must respond with `202 Accepted` when a provision job is accepted.
- API must respond with `Location` header so the client can poll the asynchronous processing state.
- User must supply Accept header with `application/fhir+json`. If any other value is specified, then `400 Bad Request` should be returned.
- User must supply Prefer header with `respond-async`. If any other value is specified, then `400 Bad Request` should be returned.
- API must return `409 conflicts` if the anonymized view already exist.
- API can return `429 Too Many Requests` if there are too many concurrent provision job.
    - Initially, only one concurrent job is supported.
- API must return with `400 bad request` in case of error with `OperationOutcome` in JSON format describing the user error.
- API must respond with `500 Internal Server Error` in case of error with `OperationOutcome` in JSON format describing the error.

## Checking a provision job status
- User must be able to call `GET [polling location]` to get the current status of the job.
- User must have privilege to check a provision job. If user does not have the corresponding privilege, then `403 Forbidden` should be returned.
- API must respond with `202 Accepted` if the job is pending or in-progress.
- API must respond with `200 OK` if the job is completed.
- API must return `output`, which is `AnonymizedViewInfo`.
    - `AnonymizedViewInfo` must return `anonymized-view-name` to indicate the name of the view.
    - `AnonymizedViewInfo` must return `anonymization-config` to indicate the anonymized config to the view.
- API must return `request` to indicate the URI of the original provision job.
- API must respond with `404 Not Found` in case the job does not exist.
- API must respond with `500 Internal Server Error` in case of error with `OperationOutcome` in JSON format describing the error.

## Cancel provision job operation
Cancel operation should stop anonymization and import operation and clean all related resources.

![cancel_flow.jpg](/.attachments/cancel_flow-d9f02a9c-77ec-4399-802e-70efa79df60b.jpg)

- User must be able to call `DELETE [polling location]` to cancel the provision job.
- User must have privilege to check a provision job. If user does not have the corresponding privilege, then `403 Forbidden` should be returned.
- API must respond with `202 Accepted` if the job is in-progress and canceled succeed.
- API must respond with `409 Conflicted` if the job is completed.
- API must respond with `404 Not Found` in case the job does not exist.
- API must respond with `500 Internal Server Error` in case of error with `OperationOutcome` in JSON format describing the error.


## Get anonymized view
- User must be able to call `GET [base]/anonymized-view/<<view-name>>` to get the information of the anonymized view.
- User must have privilege to get a anonymized view. If user does not have the corresponding privilege, then `403 Forbidden` should be returned.
- API must return `output`, which is `AnonymizedViewInfo`.
    - `AnonymizedViewInfo` must return `anonymized-view-name` to indicate the name of the view.
    - `AnonymizedViewInfo` must return `anonymization-config` to indicate the anonymized config to the view.
- API must respond with `404 Not Found` in case the view does not exist.
- API must respond with `500 Internal Server Error` in case of error with `OperationOutcome` in JSON format describing the error.

## List anonymized view
- User must be able to call `GET [base]/anonymized-view` to provision new anonymized view.
- User must supply Accept header with `application/fhir+json`. If any other value is specified, then `400 Bad Request` should be returned.
- API must return `output`, which is a list of `AnonymizedViewInfo`. If no views are returned, then output must be an empty array.
    - `AnonymizedViewInfo` must return `anonymized-view-name` to indicate the name of the view.
    - `AnonymizedViewInfo` must return `anonymization-config` to indicate the anonymized config to the view.
- User must have privilege to list anoymized views. If user does not have the corresponding privilege, then `403 Forbidden` should be returned.
- API must respond with `500 Internal Server Error` in case of error with `OperationOutcome` in JSON format describing the error.

## Delete anonymized view
- User must be able to call `DELETE [base]/anonymized-view/<<view-name>>` to delete the anonymized view.
- User must have privilege to delete a anonymized job. If user does not have the corresponding privilege, then `403 Forbidden` should be returned.
- API must respond with `200 OK` if the view is deleted.
- API must respond with `204 NO CONTENT` if the view is not existed.
- API must respond with `500 Internal Server Error` in case of error with `OperationOutcome` in JSON format describing the error.

# FHIR API for anonymization view in request processing flow
To adapt standrad FHIR API with anonymized view support. We would add anonymization view middleware in FHIR service is for pre-processing request: rewrite URL and extract anonymized view information to context for following middleware.
It should before most of middleware, and here's request processing flow with anonymizaton flow:

![AnonyumizationMiddleware.jpg](/.attachments/AnonyumizationMiddleware-a308c627-0812-4b4f-9cae-5d76f2d324fb.jpg)

# Azure RBAC on FHIR API of anonymized view
Anonymized view should be a sub-resource of _Azure API for FHIR_. FHIR service would use data plane checkaccess to authenticate customer's requests with scope to view.
Customer can assign role (FHIR Data Reader/) to scope like:  
```
$scope = "/subscriptions/a2bd7a81-579e-45e8-8d88-22db48695abd/resourceGroups/tongwu/providers/Microsoft.HealthcareApis/services/fhirdemo-origin-data/anonymized-view/view1"
az role assignment create --role "FHIR Data Writer" --scope $scope --assignee $principleid
```

# End-to-end flow for anonymized view management from Portal/CLI
We plan to support azure portal and CLI for anonymized view management API and the user should be contributor or owner of the resource.

- Step 0: Portal and CLI send request to ARM and ARM would help to check user permission.
- Step 1: ARM forward the request to healthapi frontend service.
- Step 2: Frontend service forward the request to resource provider. RP require token for itself.
- Step 3: RP worker/service call FHIR service to provision/list/delete... anonymized view. Follow async/sync pattern.

![E2E-flow.jpg](/.attachments/E2E-flow-a0df1642-0d49-4cc7-b261-ba400cea80eb.jpg)

[Note]: Here we regard anonymized view as an internal sub resource in FHIR API service. For future improvement, we may need thought to support it as a sub resource and can be deployed by template.

# Impact components
- FHIR service
    - New anonymized view management API. Support provision/list/delete... anonymized view. 
    - New or update operation store to support different type async backend tasks.
    - Update request processing flow to support URL rewrite.
    - Update Paas RBAC authorization logic to support sub resource scope check.
    - Update OSS RBAC authorization logic to support resource scope with role.
    - Update export logic to support export from different anonymized views.
    - Update data store component to support operation by COSMOS collection.
- Resource provider
    - New API for anonymized view management API in RP service.
    - New task for provision anonymized view in RP worker.
- Azure Resource Manager
    - Need to register new anonymized view management API on ARM.

# Open Questions
## How to Role based access check for OSS version
Different from PaaS version, OSS use standrad OAuth2 authentication which usually not contains sub resources information. 
We might use claims to store both role and resource scope information to authenticate. Might introduce extra complexity.

## How to manage compute or storage resource for requests from different anonymized views
Requests target anonymized view share the same FHIR service and data store. We might need to thought isolation solution for both compute and storage resource.

## Performance about provision new anonymized view
Currently we only have create single FHIR resource implementation in FHIR service. 
Batch import should be improve performance a lot for provision a new anonymized view. 

# Work items and cost estimation
1. Anonymized view CRUD API implementation and test at FHIR service side. 
    - Provision and cancel operation implementation, test and review. 2 weeks
    - List, Get, Delete anonymized implementation, test and review. 1 week
    - Integration with provision job and resource provider. 1 week 
2. Anonymized view FHIR API implementation and test at FHIR service side. 
    - OSS change for data store and export logic, implementation, review and test. 1 week
    - Paas support anonymized view FHIR API, implementation, review and test. 1 weeks
3. RBAC Authentication support. 
    - Security review on thread modeling if needed. 1 week, could be longer if we need several rounds discussion with security team.
    - Implement, review and test. 1 week
    - Integration with resoruce provider and frontend. 1 week
4. Resource provider change for anonymized management API. 
    - Support Provision APIs on service and worker. Implementation, review and test. 2 weeks.
    - Support other anonymized APIs on service. Implementation, review and test. 1 week
    - Intrgration with FHIR service and ARM. 1 week
5. Extra engineering cost: deployment, standrad process. 
    - Work with ARM team to onboard management APIs. 1 week (may wait weeks for ARM process and not sure there're maybe some more review discussions on the change)
    - Design review with Paas team. 1 week
    - Deployment, CI, code review...
# Dicom Cast - Resource Provider design

## Purpose

The purpose of this document is to outline the proposed design of azure resource provisioning for dicom cast. This includes the ARM resource definition and outlined endpoints, the resource document to be used in the global DB, and an outline of the work required to achieve this. 

## ARM

### Proposed hierarchy
```
Microsoft.HealthcareApis
	/workspaces
		/dicomservices
			/dicomcasts
		/fhirservices
		/iotconnectors
			/fhirdestinations
```
Where `dicomcasts` is a proxy resource

Pros:
- Has implicit source destination information, since it is a child resource
- Keeps dicom related items together
- Allows for a simple 1:many relationship
- Similar to iot's relationship between iot-connector and fhir-destination

Cons:
- has to be created as a proxy resource, since ARM only allows 2 levels of tracked resources. https://armwiki.azurewebsites.net/rp_onboarding/tracked_vs_proxy_resources.html
    - We need to manage cascading deletes
    - We need to handle potential request conflicts (ex:deletion of parent & update to child)
    - Not searchable by default. Extra work is needed to make this happen

### Endpoints

| API                              | Method         | Description |
|--------------------------------------|--------------|-|
| https://manangement.azure.com/subscriptions/{subscriptionId}/resourceGroups/{rgName}/providers/Microsoft.HealthcareApis/workspaces/{workspaceName}/dicomservices/{dicomServiceName}/dicomcasts?api-version=[version]  | Get | Lists all Dicom casts for a given Dicom service.|
| https://manangement.azure.com/subscriptions/{subscriptionId}/resourceGroups/{rgName}/providers/Microsoft.HealthcareApis/workspaces/{workspaceName}/dicomservices/{dicomServiceName}/dicomcasts/{dicomCastName}?api-version=[version]  | Get |Gets a specific Dicom cast description
| https://manangement.azure.com/subscriptions/{subscriptionId}/resourceGroups/{rgName}/providers/Microsoft.HealthcareApis/workspaces/{workspaceName}/dicomservices/{dicomServiceName}/dicomcasts/{dicomCastName}?api-version=[version]  | Put |Creates or Updates a Dicom cast resource.|
| https://manangement.azure.com/subscriptions/{subscriptionId}/resourceGroups/{rgName}/providers/Microsoft.HealthcareApis/workspaces/{workspaceName}/dicomservices/{dicomServiceName}/dicomcasts/{dicomCastName}?api-version=[version]  | Patch |Updates a Dicom cast resource.|
| https://manangement.azure.com/subscriptions/{subscriptionId}/resourceGroups/{rgName}/providers/Microsoft.HealthcareApis/workspaces/{workspaceName}/dicomservices/{dicomServiceName}/dicomcasts/{dicomCastName}?api-version=[version]  | Delete |Deletes the specified Dicom cast resource.|

## Naming

** follow up with Chami ** 

Cast is a verb in "Dicom Cast", where all of the other ARM names are nouns (Dicom Service, Iot Connector, etc). Pluralizing the verb to "dicomcasts" doesn't make sense.

We might also want to consider being specific in the name to support future growth of dicom cast. For example, Iot initally had it's child resource named "destinations", but updated it to be called "fhirDestinations" to allow for other connections in the future to different sources (blob, AI pipelines, etc).

Naming options:

- dicomcast / dicomcasts (original)
- dicomcaster / dicomcasters
- dicomconnector / dicomconnectors
- fhircastdestination / fhircastdestinations

## Global DB resource document

Dicom cast:
```
{
    "resource": {
        "dicomCastConfiguration": {
            ...
        },
        "dicomCastProvisionedResources": [ // used to keep track of resources to deprovision
            {
                "resourceType": "TableStorage",
                "subscriptionId": "<subId>",
                "resourceGroupName": "<rg>",
                "parentResourceIdOrName": <storage account>,
                "resourceIdOrName": "<table name>"
            },
            {
                "resourceType": "ManagedIdentity", // hold identity for table storage
                "subscriptionId": "<subId>",
                "resourceGroupName": <rg>,
                "parentResourceIdOrName": null,
                "resourceIdOrName": "<name>"
            },
            {
                "resourceType": "KubernetesResource", // provisioning / deprovisioning this resource will have to edit the dicom crd. Request conflicts must be considered
                "subscriptionId": "<subId>",
                "resourceGroupName": <rg>,
                "parentResourceIdOrName": null,
                "resourceIdOrName": "<name>"
            }
        ],
        "internalServiceName": "<name>",
        "tableEndpointKey": "https://<name>.table.core.windows.net/",
        "fhirServiceResourceId": "<resourceId of fhir service>",
        ...
        <default properties - name, parent name, last updated time, provisioned state, etc.>
        ...
    },
    "partitionKey": "<guid>",
    "name": "<workspace-name>/<dicom-service-name>/<dicom-cast-name>",
    "type": "workspaces/dicomservices/dicomcasts",
    "searchIndex": {
        ...
    },
    "id": "<name>",
}
```
Duplicate information should not be stored on DicomService resource document or DicomCast resource document. They can dynamically find their parent/child when needed. 

## Required Configs

(can be pulled in from workspace-platform)

Config:
- Geneva
    - MetricAccountName
    - MetricNamespace
    - Enabled
- Billing
    - GenevaMetricAccountName
    - GenevaMetricNamespace
- TableStore
    - ConnectionString
- Fhir
    - Endpoint
- DicomWeb
    - Endpoint
- DicomCastWorker
    - PollInterval
- ConsoleLogging
    - IncludeScopes

## Notes from the design meeting

- The connected FHIR resource should be restricted to be from the same workspace (IOT also has this restriction)
- What happens if the fhir resource is deleted?
    - IotConnector/FhirDestination has a health check that will monitor whether the fhir service exists or not. If not, the resource is put into a non-critical error state, and the error is sent to the Azure Portal as a telemetry event to surface it to customers
    - Another scenario to consider is where RBAC permissions have not been set or have been deleted. This should behave in a similar way to the fhir service being deleted - the resource is in an error state and another telemetry event is sent to the Azure Portal
- There are a few places where we could have possible race conditions:
    - Updating dicom crd via dicomcast update & dicomservice update at the same time
    - Getting data from the parent/child resource document when it is in the middle of being updated
    - Possible solutions:
        - idempotent updates with kubernetes. Do a get, then do a put. The put should fail if it has been updated since the get
        - synchronous updates. If dicom resources document is in a provisioning / updating (non-stable) state, do not do any operations on dicom cast.

## Breakdown of the work

Create resource in ARM  
1. Add boilerplate classes to ARMResourceProvider.Service.Workplace
2. Define data objects in global db
    - resource document
    - operation documents
3. Define data providers and handlers
    - hande resource cleanup (cascading deletes)
    - handle request conflicts
4. Test RP via Integration tests
    - ARMResourceProvider IT > ResourceHandler
4. Define provisioning code
    - provision & deprovision commands
    - k8s provisioning
    - azure provisioning (managed identity, storage account)
5. Test RPWorker via templates in dogfood
    - Create ARM template
    - Use powershell script for testing provisioning (in wiki) or use df portal
6. Onboard to ARM APIs 
    - Update API version
    - Update swagger

Update frontend to support provisioning on UI (Still need to investigate how much work this is)
- https://microsofthealth.visualstudio.com/Health/_git/health-paas-portal 

Do work to make the resource searchable via ARM (Still need to investigate how much work this is)

## Questions

- Will we allow multiple dicom-casts to be created for a single dicom service?
    - Answered in design discussion: first iteration will only allow 1 dicom cast per dicom service
- Billing /changefeed
    - This step: Poll for batch of changes: DICOM Cast polls for any changes via Change Feed, which captures any changes that occur in your Medical Imaging Server for DICOM.
    - Currently we bill for changefeed, apirequests & egress. Should we block these coming from dicom cast?
    - I assume the same problem exists with billing fhir api requests
        - Answered in design discussion: the cost of dicom cast will be the cost of the api requests here, so this is ok.
- Should we also create geneva actions for failover & reprovision?
    - Answered in design discussion: yes, this + other requirements (disaster recovery, security, etc.) will be necessary before releasing it to public preview or GA

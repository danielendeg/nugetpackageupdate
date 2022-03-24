<!-- vscode-markdown-toc -->
* 1. [Summary](#Summary)
* 2. [ Mapping JSON](#MappingJSON)
* 3. [ Emitting mapping data to Geneva IFX Pipeline](#EmittingmappingdatatoGenevaIFXPipeline)
* 4. [ Scenarios to create IFX event logs](#ScenariostocreateIFXeventlogs)
* 5. [ Implementation](#Implementation)
* 6. [Dicom/IoT](#DicomIoT)
* 7. [ One Time Import of Existing Data](#OneTimeImportofExistingData)
* 8. [TL;DR](#TLDR)

<!-- vscode-markdown-toc-config
	numbering=true
	autoSave=true
	/vscode-markdown-toc-config -->
<!-- /vscode-markdown-toc -->

<b style='color:red'>NOTE: </b>
Lockbox feature was supposed to give a break-glass access to each database. However, due to short comings of SQL Server not able to provide database level access control when databases are in Elastic pool, lockbox is not able to provide this break-glass access to the database. Instead, the approval for an access request to a single database will grant access to all SQL Servers in the subscription or to all databases within an Sql Server. Therefore, since currently the lockbox HOBO does not provide the intended functionality, we are out of scope for now.  

##  1. <a name='Summary'></a>Summary
In order to investigate issues or IcMs that involve customer data, there is a need to get approval from customers before accessing thier data. Currently, we get a written consent from customer to access thier data in the database.
Azure lockbox provides an automated service, where an engineer can make a JIT request to access customer's data, and the lockbox will send an email to the customer with a link to approve or deny the request.
The purpose of this spec to create a design document that will be implemented to enable us to integrate with lockbox.
In our scenario, the customer database is in Microsofts internal subscription (HOBO, hosting on behalf of). Therefore, there is a need to create a resource mapping between the database resource in Microsoft subscription to customer's provisioning subscription and publish it to Geneva IFX, where it is picked up the lockbox system
##  2. <a name='MappingJSON'></a> Mapping JSON
The json data that will be published to IFX log should have the following format as per the dictated by [lockbox team](https://nam06.safelinks.protection.outlook.com/ap/w-59584e83/?url=https%3A%2F%2Fmicrosoft.sharepoint.com%2F%3Aw%3A%2Fr%2Fteams%2FAzureLockboxvTeam%2F_layouts%2F15%2FDoc.aspx%3Fsourcedoc%3D%257B2439CCFD-2DD7-4945-8C2A-2F631E6AB914%257D%26file%3DLockboxHostedOnBehalfOf_PartnerOnboarding.docx%26action%3Ddefault%26mobileredirect%3Dtrue%26share%3DIQH9zDkk1y1FSYwqL2MearkUAfx3MdEoHM58AKt2_DhgNls&data=04%7C01%7Cdhunegnaw%40microsoft.com%7C66013886140540996bb908da013cd7f2%7C72f988bf86f141af91ab2d7cd011db47%7C1%7C0%7C637823658117829069%7CUnknown%7CTWFpbGZsb3d8eyJWIjoiMC4wLjAwMDAiLCJQIjoiV2luMzIiLCJBTiI6Ik1haWwiLCJXVCI6Mn0%3D%7C3000&sdata=HU5tgEozfxFZkvCMcbHwCIjidQ%2FmDw1LuTCBMyWgajw%3D&reserved=0). Each specific property corresponding to each fhir accountswith sql database. If dicom/iot uses sql databases, the mapping should follow a similar pattern
```JSON
{
	"Version": "1.0",
	"HoboMappingEventData": {
		"CorrelationId": "acd73db1-7799-45c1-8c1d-856a835d56a6",
		"EventTimestamp": "2022-03-023T21:03:48.9848076Z",
		"EventType": "Create",
		"CustomerResource": {
			"SubscriptionId": "8d2f7ee8-8846-48a7-86c5-a9690987b668",
			"ResourceType": "Microsoft.HealthcareApis/FhirServices",
			"ResourceName": "fhir-for-hospital",
			"PartitionName": null
		},
		"HostedOnbehalfResource": {
			"SubscriptionId": "9ae87f73-37e6-470a-aa00-25f05a75a049",
			"ResourceType": "Microsoft.SqlServer/database",
			"ResourceName": "sdb-jxhfasdfjklal5345n",
			"PartitionName": null
		}
	}
}
```
When this json is written to the IFX pipeline , it should be serialized as in:
```JSON
{
	"ver": "1.0",
	"data": "{
		\"cid\": \"acd73db1-7799-45c1-8c1d-856a835d56a6\",
		\"ts\": \"2022-03-023T21:03:48.9848076Z\",
		\"et\": \"Create\",
		\"cr\": {
			\"sid\": \"a8d2f7ee8-8846-48a7-86c5-a9690987b668\",
			\"rt\": \"Microsoft.HealthcareApis/FhirServices\",
			\"rn\": \"fhir-for-hospital\",
			\"pn\": null
		},
		\"hr\": {
			\"sid\": \"9ae87f73-37e6-470a-aa00-25f05a75a049\",
			\"rt\": \"Microsoft.SqlServer/database\",
			\"rn\": \"sdb-jxhfasdfjklal5345n\",
			\"pn\": null
		}
	}"
}
```
##  3. <a name='EmittingmappingdatatoGenevaIFXPipeline'></a> Emitting mapping data to Geneva IFX Pipeline

An mds table with name **LockboxHoboMappingEventTable** will be created in Microsoft.Healthcareapis namespace and the logs will be published to this table with schema as in described in this lockbox [documentation](https://microsoft.sharepoint.com/:w:/r/teams/AzureLockboxvTeam/_layouts/15/Doc.aspx?sourcedoc=%7B2439ccfd-2dd7-4945-8c2a-2f631e6ab914%7D&action=view&wdAccPdf=0&wdparaid=5700C7CB)

For ```TagName``` use the value "LockboxHoboMappingEvent"

##  4. <a name='ScenariostocreateIFXeventlogs'></a> Scenarios to create IFX event logs

There are two kinds of events that can happen with respect resource mapping.
    1. <b>Create</b>: event type whena new hobo resource is created (provisioned, subscription state change to registered in our specific case) and mapped to a resource
    2. <b>Delete</b>: event typpe whena service resource or a hobo is deleted; for Azure HealthCareAPI this corresponds to when an account is deprovisioned, or subscription state is changed to warned/suspended
   
**Scenarios 1): On Provisioning Complete**
1. Customer provisiones Fhir Account (Say A) in their subscription (S1)
2. Azure HealthCare API creates Sql Database (say B) in its internal hobo subscription (S2)
   
Event Here
*  Azure HealthCare API publishes a new event with below info to IFX Logs
   a) Eventtype : Create
   b) CustomerResource: A
   c) HostedOnBehalfResource: B

**Scenarios 2): Deprovisioning Complete**
1. Customer deprovisions Fhir Account (Say A) in their subscription (S1).
2.  Azure HealthCare API deleted the SQL Database (Say B) in its internal hobo subcription (S2)

Events:
 *  Azure HealthCare API publishes a new event with below info to IFX Logs
   a) Eventtype : Delete
   b) CustomerResource: A
   c) HostedOnBehalfResource: B

##  5. <a name='Implementation'></a> Implementation

The following class will be implemented to create a mapping of resources and publish it to geneva IFX pipeline.
```csharp
public class LockboxDataLogger:  ILockboxDataLogger
{
   public async Task Log(ResourceDocumentModel<FhirServiceResource> account, EventType event,CancellationToken token)
   {
      //1. Create Mapping between customer's subcription and their database resource
      //2. publish serialize and publish the data to IFX
   }
}

```
The ```ILockboxDataLogger``` is constructor injected into the ```FhirServiceDeprovisionCommand``` to publish lockbox data create event 

```csharp
 public FhirServiceProvisionCommand(
            IOptions<ResourceProviderServiceEnvironment> serviceEnvironment,
            ISubscriptionRepository subscriptionRepository,
            ISecretMetadataRepository secretMetadataRepository,
            IResourceManagementRepository resourceRegistryManagementRepository,
            IClustersProvider clustersProvider,
            IWorkspaceFhirServiceFabricProvisioningProvider serviceFabricProvisioningProvider,
            ITrafficManagerProvisioningProvider trafficManagerProvisioningProvider,
            IServiceDnsProvisioningProvider dnsProvisioningProvider,
            IFhirEndpointTestProvider endpointTestProvider,
            IResourceGroupProvisioningProvider resourceGroupProvisioningProvider,
            IApplicationInsightsProvisioningProvider applicationInsightsProvisioningProvider,
            IClusterSelector clusterSelector,
            IAzureProvider azureProvider,
            ISubscriptionProvider subscriptionProvider,
            ISqlServerLocator sqlServerLocator,
            IAccountRegistryManagementRepository accountRegistryManagementRepository,
            Func<DateTimeOffset> utcNowFunc,
            ISqlDatabaseConnectionInfoProvider sqlDatabaseConnectionInfoProvider,
            ISqlDatabaseProvisioningOrchestrator sqlDatabaseProvisioningOrchestrator,
            IExternalManagedIdentityOperationHandler externalManagedIdentityOperationHandler,
            IImportRunningTaskStatusChecker importRunningTaskChecker,
            ILockboxDataLogger lockboxDataLogger
            ILogger<FhirServiceProvisionCommand> logger)
            : base(serviceEnvironment, resourceRegistryManagementRepository, logger)
        {
        }
```

The ```ILockboxDataLogger``` is constructor injected into the ```FhirServiceDeprovisionCommand``` to publish lockbox data delete event

```csharp

 public FhirServiceDeprovisionCommand(
            IOptions<ResourceProviderServiceEnvironment> serviceEnvironment,
            IResourceManagementRepository resourceRegistryManagementRepository,
            IClustersProvider clustersProvider,
            IWorkspaceFhirServiceFabricProvisioningProvider serviceFabricProvisioningProvider,
            ITrafficManagerProvisioningProvider trafficManagerProvisioningProvider,
            IServiceDnsProvisioningProvider dnsProvisioningProvider,
            IResourceGroupProvisioningProvider resourceGroupProvisioningProvider,
            IApplicationInsightsProvisioningProvider applicationInsightsProvisioningProvider,
            IExternalManagedIdentityOperationHandler managedIdentityOperationHandler,
            IAzureProvider azureProvider,
            IMonitorManagementClientProvider monitorManagementClientProvider,
            TaskProcessorFactory taskProcessorFactory,
            ISqlDatabaseProvisioningProvider sqlDatabaseProvisioningProvider,
            IAccountRegistryManagementRepository accountRegistryManagementRepository,
            ISqlDatabaseConnectionInfoProvider sqlDatabaseConnectionInfoProvider,
            IMetadataObjectRepository metadataDocumentRepository,
            ILockboxDataLogger lockboxDataLogger
            ILogger<IResourceCommand<FhirServiceOperation>> logger)
            : base(serviceEnvironment, resourceRegistryManagementRepository, logger)
        {
        }
```


A new property will be added the fhir resource document to capture. This property might not be needed if the lockbox does not use the correlationId from Create Event to for deletion during Delete Event.
 ```JSON
   "lockbox" : {
       "correlationId": "78ec3ecc-6b4c-4d89-af63-b8529a4ef941",
       "timestamp": "2022-03-023T21:03:48.9848076Z"
   }
 ```
 
##  6. <a name='DicomIoT'></a>Dicom/IoT
Dicom and Iot also should follow similar pattern to create the mapping between customer's subscriptions and customer data resource.

##  7. <a name='OneTimeImportofExistingData'></a> One Time Import of Existing Data
Lockbox support to the existing fhir accounts can be done via Geneva action. The following pseudo code will be implemented in the PaaS and executed from Geneva action once.
```
      Do
        accounts -> GetPagedFhirAccounts()
          ForEach account in accounts
                ILockboxDataLogger.Log(:account)
          End ForEach
      While(There are more pages)
   ```

##  8. <a name='TLDR'></a>TL;DR

Customer Lockbox for Microsoft Azure provides an interface for customers to review and approve or reject customer data access requests. It is used in cases where a Microsoft engineer needs to access customer data, whether in response to a customer-initiated support ticket or a problem identified by Microsoft.
In Azure HealthCare API, we host customer's data in Microsoft's subscriptions, HOBO (hosting on behalf of). Thus, in order to support lockbox, there is a need to create mapping between customer's subscription and their database name in Sql Server and feed this mapped data stream to IFX geneva pipeline.
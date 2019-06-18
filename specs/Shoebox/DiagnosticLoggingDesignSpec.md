# Design for Diagnostic Logging for Shoebox

This spec covers both the OSS and PaaS implementations. The goal is to allow the below mentioned Scenarios to be logged and available from the Azure Portal. In the case of OSS the logs would be available after turning "App Service logs" on with appropriate settings. For PaaS you can enable them via "Diagnostic Settings" where you can set the destination of the logs (blob storage, event hub and/or Log Analytics), they can also be viewed in near realtime using Azure Monitor.

[[_TOC_]]

# Scenarios
Below are the diagnostics logs from the PM spec that we need to support.

* Diagnostics logs:
    + Security Logs (Data plane): - P1
        - CRUD operations on the service (Read, Write, Update, Delete)
    + Application Logs:
        - Critical, Error, Warning, Information - P1
        - As we cannot expose stack traces for our internal code we can use friendly messages for exceptions for customers can figure out what went wrong and file a support ticket if necessary. They will need enough information to have an idea of what went wrong.

# Metrics

The number of customer's that have opted into diagnostic logging. This being tracked on the Azure side as they use this information if we enable a bloom filter in our Derived Event queries. There is an Azure Portal Telemetry Kusto Cluster that we can request access too for ths information.

# Design

## OSS
Since the FHIR server in our OSS implementation is deployed as an Azure Web App, Azure has guidance on surfacing diagnostic logs in this case. The recommendation is to add the Microsoft.Logging.Extensions.Logging.AzureAppServices nuget package and add use ILoggingBuilder.AddAzureWebAppDiagnostics() to add a FileLoggerProvider and BlobLoggerProvider.  You can customize a few options by adding the below logger option objects to the service collection. Then once the customer deploys the FHIR service they can go to the "App Service Logs" tab and add the information for their blob storage account and/or enable File system logging. This will log all current logger entries. 

We could go with a similar approach to the PaaS implementation and have an IDiagnosticLogger interface that is implemented as a singleton and the customer can choose what they specifically want to log. We will most likely align to the design used for the PaaS implementation. Note: that file system logging will turn off after 12 hours automatically. This is only meant to be used to diagnose an active issue. All logs can be downloaded from an FTP site listed in the Diagnostic Settings tab.

``` C#
services.Configure<AzureFileLoggerOptions>(options =>
{
    // FileName prefix.  Default = diagnostics-
    options.FileName = "azure-diagnostics-";

    // Max log size in bytes. No more logs will be appended when limit is reached. NULL = no limit. Default is 10 MB
    options.FileSizeLimit = 50 * 1024;

    // Default = 2. NULL = all files kept.
    options.RetainedFileCountLimit = 5;
});

services.Configure<AzureBlobLoggerOptions>(options =>
{
    // Gets or sets the last section of log blob name. Default = applicationLog.txt
    options.BlobName = "azureLog.txt";
});
```

## PaaS
For PaaS we will be surfacing logs to the customer using Shoebox. This includes a few different systems. From a code perspective we will need to upgrade nuget packages for Application Insights to support the use of multiple telemetry channels. This will allow us to send data to both AI and Geneva using AI telemetry libraries. Geneva is the important channel in this case. To capture events we need to create Derived Events in the MDS config and then, in the same config, set up an Event Stream with the <OnBehalf> tag. These logs will be sent to our warm path account and the Shoebox service will pick them up and send them to the correct customer using the Azure Resource Id that is required to be a part of the message. We will need to update our code to make sure we are including the Shoebox log required properties (See below). AI allows adding custom properties to a log and exposes them as their own column. This is the same way we are currently adding our ServiceFabricContext properties and ServiceEnvironment properties. This will make it much easier to write the Derived event queries. 

We have two options for implementation and this is mostly determined by how we want to manage the logs we send to end customers and whether it bothers us to have duplicate logging statements. We can use an ILogger implementation that uses TelemetryConfiguration.TrackTrace and TelemetryConfiguration.TrackException underneath the covers, so that any ILogger logs we already emit can be sent to Shoebox and we can filter in the MDS config for the logs we want. The other option is to still use the TelemetryConfiguration methods to emit the logs but instead of an ILogger implementation we model it after our IAuditLogger and implement it as a singleton and explicitly log data that will be sent to the customer. The later would make it easier for code reviewing, but would duplicate logging in a lot of places.

### Required Properties for shoebox logs
Note: This log schema is the Azure diagnostic logs schema. https://docs.microsoft.com/en-us/azure/azure-monitor/platform/diagnostic-logs-schema 
| Name           | Datatype            | Description                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
|----------------|---------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| time           | timestamp           | The timestamp (UTC) of the event.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| resourceId     | String (UPPERCASE)  | The resource id of the resource that emitted the event. Must be in all upper-case letters.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| operationName  | string              | The name of the operation represented by this event. If you are using data-plane RBAC, you must use the RBAC operation name in this field (e.g. Microsoft.Storage/storageAccounts/blobServices/blobs/Read). If not, there should be a limited number of operation names for your service, ideally modeled in the form of an ARM operation, even if they are not actual ARM operations (Microsoft.<providerName>/<resourceType>/<subtype>/<Write/Read/Delete/Action>)                                                                                                                                        |
| category       | string              | The log category of the event. Category is the granularity at which a customer can enable or disable logs on a particular resource – they can individually opt in or out of particular log categories. The properties that appear within the properties bag (part B) of an event should be the same within a particular log category. Typical log categories are “Audit” “Operational” “Execution” and “Request” IMPORTANT Note: Category names can only contain English letters and numbers. They cannot contain any special characters such as blank space, parenthesis, ampersand, period, hyphen, etc.  |
| properties     | string              | Any extended properties related to this particular category of events. All custom/unique properties must be put inside this “Part B” of the schema.                                                                                                                                                                                                                                                                                                                                                                                                                                                         |

### Optional Properties

| Name               | Datatype  | Description                                                                                                                                                                                                                                                                                                                                                                                      |
|--------------------|-----------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| operationVersion   | String    | The api-version associated with the operation, if the operationName was performed using a customer-facing API (e.g. http://myservice.windowsazure.net/object?api-version=2016-06-01). If there is no customer-facing API for the operationName, this version represents the version of that operation, in case you wanted to revise the properties associated with the operation in the future.  |
| resultType         | String    | The status of the event. Typical values include Started, In Progress, Succeeded, Failed, Active, and Resolved.                                                                                                                                                                                                                                                                                   |
| resultSignature    | String    | The sub status of the event. If this operation corresponds to a REST API call, this should be the HTTP status code of the corresponding REST call.                                                                                                                                                                                                                                               |
| resultDescription  | String    | The static text description of this operation, e.g. “Get storage file.” (This would be where our message strings goes.)                                                                                                                                                                                                                                                                                                                         |
| durationMs         | Int (ms)  | The duration of the operation in milliseconds.                                                                                                                                                                                                                                                                                                                                                   |
| callerIpAddress    | String    | The caller IP address, if the operation corresponds to an API call that would come from an entity with a publicly-available IP address (do not use if the caller IP address would be Microsoft internal).                                                                                                                                                                                        |
| correlationId      | String    | A GUID used to group together a set of related events. Typically, if you are emitting the same operationName with two different statuses (e.g. “Started” and “Succeeded”), they will share the same correlation ID. You may optionally use the same correlation ID to represent other relationships between events as well (e.g. event1 caused event2).                                          |
| traceContext       | String    | If the service is instrumented with W3C distributed tracing protocol, JSON blob that represents W3C trace-context: ‘traceId’, ‘spanId’, ‘parentId’, ‘tracestate’ and ‘traceFlags’. It may also include additional distributed tracing properties.                                                                                                                                                |
| identity           | String    | A JSON blob that describes the identity of the user or application that performed the operation. Typically, this will include the authorization and claims / JWT token from Active Directory.                                                                                                                                                                                                    |
| Level              | String    | The severity level of the event. Must be one of Informational, Warning, Error, or Critical.                                                                                                                                                                                                                                                                                                      |
| location           | String    | The geo of the resource emitting the event, e.g. “East US” or “France South”                                                                                                                                                                                                                                                                                                                     |
| uri                | String    | Absolute request URI. If this operation corresponds to an incoming or outgoing call, this should be the destination URI of this call.                                                                                                                                                                                                                                                            |
| properties         | String    | Any extended properties related to this particular category of events. All custom/unique properties must be put inside this “Part B” of the schema.                                                                                                                                                                                                                                              |

### Example MDS Config
``` XML
<?xml version="1.0" encoding="utf-8"?>  
<MonitoringManagement 
    version="1.0"  
    namespace="WATask"  
    timestamp="2015-07-02T21:18:00.0000000Z"  
    eventVersion="3">  
  <Accounts>  
    <!-- This is the moniker you created    

         RECOMMENDATION : You should create separate moniker for the events which will be enabled for onbehalf of logging.  
                          This will prevent the problem of any unforeseen xstore throttling if the same account is used to upload other event streams.  
         -->  
    <Account moniker="storagewestus" isDefault="true" />  
  </Accounts>  
  <Management  
        eventVolume="Medium"   
        <!-- Required -->
        onBehalfIdentity="WATask"  
        <!-- Required: these tags represent columns for OnBehalfFields resourceid, category -->
        onBehalfFieldTags="tag1,tag2" 
        <!-- Optional: Used in EventStream for replacing resourceid in this case --> 
        onBehalfReplaceTags="tag1">  
    <Identity type="TenantRole" /> 
    <AgentResourceUsage diskQuotaInMB="20000" />  
  </Management>  
  <Events>  
    <EtwProviders>  
      <!-- Required duration="PT1M" for ETWProvider to be able to process logs as quickly as possible to get them to the customer. -->
      <EtwProvider  
            name="TestShoeboxEventSource" 
            format="EventSource"  
            storeType="CentralBond"  
            duration="PT1M">  
        <Event id="2" eventName="TestEvent1" />  
        <!-- For testing replaceTags -->  
        <Event id="3" eventName="TestEvent2" /> 
      </EtwProvider> 
    </EtwProviders>  
    <!-- Required duration="PT1M" and storeType="CentralBond" for DerivedEvents to be able to process logs as quickly as possible to get them to the customer. -->
    <DerivedEvents>   
        <DerivedEvent source="TestEvent1"  
                eventName="TestShoeboxEvents1" 
                storeType="CentralBond" 
                whereToRun="Local"  
                duration="PT1M"  
                priority="Normal"> 
            <!-- Required resourceId, category, operationName, properties. This must be selected properties from the log as they are required fields for Shoebox logs-->
            <Query>  
               let time = TIMESTAMP  
               let category = "AuditEvent"  
              select Tenant,  
                Role,  
                RoleInstance,  
                time,  
                resourceId, 
                category,  
                operationName, 
                properties  
            </Query>  
        </DerivedEvent>  
    <!-- Required duration="PT1M" and storeType="CentralBond" for DerivedEvents to be able to process logs as quickly as possible to get them to the customer. -->
    <DerivedEvent source="TestEvent2" 
                eventName="TestShoeboxEvents2"       
                storeType="CentralBond"  
                whereToRun="Local" 
                duration="PT1M"  
                priority="Normal"> 
             <!-- Required resourceId, category, operationName, properties. This must be selected properties from the log as they are required fields for Shoebox logs-->
             <!-- Required ApplyBloomFilter used to filter logs for customers that have not opted in for diagnostics logs-->
             <Query>  
               where ApplyBloomFilter("SHOEBOX_BLOOM_FILTER", resourceId) 
                let time = TIMESTAMP  
                let category = "StoredProcedures"
               select Tenant,  
                Role,  
                RoleInstance,  
                time,  
                resourceId,  
                category,  
                operationName,  
                properties  
            </Query> 
         </DerivedEvent> 
    </DerivedEvents>  
  </Events>  
  <EventStreamingAnnotations>   
        <!-- Required onBehalfFields="resourceId,category", primaryPartitionField="resourceId", containerSuffix="$category"-->
       <EventStreamingAnnotation name="^TestShoeboxEvents1$">   
             <OnBehalf>
                 <Content>
                      <![CDATA[<Config  
                        onBehalfFields="resourceId,category"   
                        priority="Normal"  
                        primaryPartitionField="resourceId"                                      
                        containerSuffix="$category"/>]]>  
                   </Content>   
             </OnBehalf>   
       </EventStreamingAnnotation>  
       <!-- Required onBehalfFields="resourceId,category", primaryPartitionField="resourceId", containerSuffix="$category"-->
       <!-- Optional onBehalfReplaceFields="resourceId", validJsonColumns="properties", excludeFields="Cluster,Role,RowKey"-->
       <EventStreamingAnnotation name="^TestShoeboxEvents2$">   
             <OnBehalf>   
                 <Content>   
                  <![CDATA[<Config  
                     onBehalfFields="resourceId,category" 
                     priority="Normal"  
                     primaryPartitionField="resourceId"  
                     containerSuffix="$category" 
                     onBehalfReplaceFields="resourceId"  
                     validJsonColumns="properties"  
                     excludeFields="Cluster,Role,RowKey"/>]]>   
                   </Content>   
             </OnBehalf>   
        </EventStreamingAnnotation>  
    </EventStreamingAnnotations>   
</MonitoringManagement>
```

### Bloom Filter
Taken from Azure Monitoring Logs Onboarding documentation:
To reduce logging traffic for customers that have not enabled diagnostic settings, we strongly recommend configuring the bloom filter (Wikipedia). The bloom filter only emits logs for customers that have enabled a diagnostic setting, reducing log volume to Geneva and reducing COGS for everyone. Without a bloom filter, it will upload all the data for all customers to Geneva, and then Shoebox will filter on the backend. This has major cost & resource implications for the Agent, moniker, and shoebox service. Please consult with shoebox team if you are not enabling bloom filter so we can provide appropriate support. 

With bloom filter enabled, the MA talks to Shoebox servers every five minutes to get updated configuration, so whenever a new customer is onboarded, the filter will have this information in approx. 5 minutes. Every time MA runs your derived event for shoebox event, if you have used query construct ApplyBloomFilter on resourceId column, it will discard the data for the customers who have not enabled diagnostics logging and upload data to first party account for only those customers who have onboarded. 

For filtering on the node (bloomfilter) feature you need to pass an extra command line argument to Geneva monitoring agent. 

Decide on the certificate you want agent to use to communicate with central service.  

Add below command line argument while launching monitoring agent [detailed info on what this means is here: Diagnostic Logs Filtering (OneNote) ] 

-OnbehalfInfo \<endpoint\> \<Thumbprint\> \<Cert Store\> \<Geneva warm path account name\> \<Geneva environment\> 

    Endpoint: Geneva warm path endpoint [For test use ppe.warmpath.cloudes.com, for production use prod.warmpath.cloudes.com ] 

    Thumbprint: Certificate thumbprint you want to use to authenticate with central service 

    Cert Store: LOCAL_MACHINE\MY or CURRENT_USER\MY depending on where cert is installed. 

    Geneva warm path account: Account name of your Geneva warm path account. 

    Geneva Environment: For test, Test, for all other production public cloud environments use DiagnosticsProd. 

You should whitelist your certificate. Use below Jarvis help link to whitelist your certificate 

https://jarvis-west.dc.ad.msft.net/?page=documents&section=1363da01-b6ed-43d4-970e-f89b011d591f&id=3d5a17c8-3565-44d5-8cdb-e33223a0d20c#/ 

# Test Strategy

Diagnostic Logging is not available in the Dogfood environment. Shoebox uses a "Test in Production" environment, in Brazil US region, for testing shoebox logging. We will need to look into deploying our service in this environment and running tests, like the one mentioned below. As a possible alternative after we are setup up we can have event streams in the ResoluteNonProd config using the same derived event queries and view the captured logs in their own table. So then we can see what is getting logged specifically for shoebox.

For an E2E test we would need a service running with that resource opted into diagnostic logs. We would run into timing issues from when we emit the log from the service to when it would show up on in whatever diagnostic log output we chose. There is no specific SLA in regards to logs showing up. It is best effort.

# Security

We need to make sure we don't leak any internal information that the custom does not need to see as part of their diagnostic logs. If for some reason we start to log a field that may contain PII then we do have the options to either filter it or if the customer requires this information then we can restrict access to the Geneva Warmpath tables where these logs will be stored.

# Other

We may need to filter out specific properties from the required properties column using the excludeField with a list of comma separated property names as part of the EventStream we create for Shoebox events.  We need to make sure any columns we do keep have information that is only for the customer's instances. We should write up an SOP for reviewing and testing new logs we surface to the customer to make sure it only contains the data we want.

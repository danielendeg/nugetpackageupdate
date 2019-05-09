# Migration of logging to Geneva from Application Insights

## Description

The purpose is to have all logging captured by the Geneva Monitoring Agent rather than having some logs captured by Application Insights and some captured by the Monitoring Agent.  Having logs available in Geneva is a current Azure Security requirement as well as needed by Customer Service to debug current issues and file appropriate tickets. 

## High-level Design

We will do this work in phases.  For a time, we will have logs from both AI and the Monitoring Agent.  We will send the monitoring agent logs (where applicable) to their own tables so we do not make it look like there are twice as many exceptions.  Once we are satisfied that we are capturing what we need, we can start to add our ICM alerting, adjust or move queries to use the new tables and set up new synthetic transactions.  The final phase will be to remove the Application Insights logging.

### Phase 0
In this phase we will we prototyping a few things to make sure it aligns with what we need for PaaS and OSS.  
* Kestrel Logging and turning it into metrics. 
* IFX Custom Event logging and the ILogger implmentation.

### Phase 1
In this phase the goal is to reach 100% parity with what we are currently logging for Application Insights.  This includes ETW logs, Perf Counters, health reporting and web requests.  We will tackle a single service at a time in this phase.  Once all services have reached an appropriate level of parity we will move to Phase 2.

### Phase 2
Here we will create our ICM Alerting correlation rules based on the new tables in Geneva.  Set up the connector to stream logs from Geneva to Kusto and then from Kusto to PowerBI.  Then update all our existing Kusto and PowerBI queries.

### Phase 3
We will remove AI logging from the code, clean up the AI connector and AI tables from Geneva, remove AI ICM alerts where applicable and clean up any necessary Azure assests for AI and update deployment scripts and ARM templates where needed.

### ETW logging
If we choose to go the route of custom ETW events then we can use the Ifx user defined events. For these you would not need to install the custom schema for each event on the Windows machine you are running on.  You need to intialize an ifx session in order to emit the logs from your service to ETW. This allows IFX and MA to know what the generated eventIds will be by allowing them to access a shared memory space to hand off the ids.  We currently do this IFX initialization for the ifx audit logger.  For the custom properties in an event you would create a .bond file, in your source project, which contains structs used to define what properties will be added to the event.  There is a standard set of properties that will be added to the event as well.  Those schemas are defined here https://microsoft.sharepoint.com/teams/WAG/EngSys/Monitor/AmdWiki/Common%20Schema.aspx The "sessionName" in the mds config file is the same name you will use when initializing the IFX logger.  We need to create an event structure that we can safely use in the OSS implementaion of the FHIR Server.  We can have some common columns between the two implementations and leave free form json column for custom things.  That would allow us to quickly query some things and then parse the custom column when we need more information.

We can create an IfxEventLogger class implemented as an ILogger implementation.  We can use the custom events with the Audit schema if we find that useful.  The Ifx Correlation Context can set an activity id based on the asp .net correlation id we generate for a particular web request to associate these ETW logs.  The correlation context api allows us to pass the context across thread, process and machine boundaries by saving the context to a blob and then retreiving it on the other side.  For Audit Logs we should be able to set the Activity Id and other Event Schemas have a Correlation Vector. The caveat is that the tooling used to decode these correlation vectors are no longer being developed with new features, but the tools and API are not being decomissioned.  Geneva mentioned using the Codex package (https://codex.microsoft.com) which is IFX logging compatible and has great tooling around using the correlation vectors for tracing and debugging.  However, the financial cost for the Codex service and what they offer is more than what we need and does not fit as a great option.  A note here is that there are no plans to remove the tooling or the Correlation Context API as larger teams like Azure Compute are heavily reliant on this api.  If the tooling has the type of data we need and we don't care about new features then we would still use this.

Bond file example
``` C#
import "Ifx.bond"

namespace MyNamespace
struct MyOperationEventPartC:Ifx.OperationSchema
{
	10: required wstring PartCField1;
	20: required wstring PartCField2;
};

struct MyPartAEventPartC:Ifx.PartASchema
{
	10: required wstring PartCField1;
	20: required wstring PartCField2;
};
```
C# Usage
``` C#
using MyNamespace;
public class IfxEventLogger : ILogger
{
	public IfxEventLogger()
	{
		IfxInitializer.Initialize("AuditLog");
	}

	...

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
	{
		// Use the state to parse Key values pairs and assign them to the columns or have 
		// all the common properties passed into the IfxEventLogger when it gets instantiated
		var stateDictionary = state as IReadOnlyList<KeyValuePair<string, object>>;

		MyNamespace.MyOperationEventPartC myOpEvent = new MyNamespace.MyOperationEventPartC
		{
			PartCField1 = "PartC Field 1",
			PartCField2 = "PartC Field 2"
		};

		MyNamespace.MyPartAEventPartC myPartAEvent = new MyNamespace.MyPartAEventPartC
		{
			PartCField1 = "PartC Field 1",
			PartCField2 = "PartC Field 2"
		};

		/*
		* Instrumenting an internal call.
		*/

		// The default operation type of a newly created operation is Internal call.
		// Once we exit the using statement the log is emitted to the ETW
		// Duration and operation end time for this specific operation are also added.  
		// However, this will not be useful in an ILogger implementation
		using (ExtendedOperation<MyNamespace.MyOperationEventPartC> operation = new ExtendedOperation<MyNamespace.MyOperationEventPartC>("Internal Call"))
		{
			// Set PartC.
			operation.PartC = myOpEvent;
			// Set result for the operation.
			operation.SetResult(OperationResult.Success);
		}

		// Or is you wanted to pass in duration and operation end time for the event you could do the below.
		// However, you cannot use the PartC Schema and you add everything as one string in the resultDescription.

		Operation.Log(
			"Test Operation", // Operation Name
			"1.0", // Operation version
			"123.123.123.123", // Caller IP Address
			null, // target endpoint address
			OperationApiType.InternalCall,
			OperationResult.Success,
			"Read 1000 bytes", // resultSignature
			"File: abc.txt", // resultDescription
			new DateTime(2017, 12, 31, 14, 15, 35), // Operation end time
			111222, // duration in milliseconds
			false, // does not impact qos i.e. emit operation event
			"some resource type",
			"some resource id");

		// You can log a non-operation event if you use just PartA Schema instead of the Operation Schema
		IfxEvent.Log(myPartAEvent);
	}
```

Example MDS Config (This is a snipet of what would be added for the events in the bond file above)

``` XML
<Events>
    <IfxEvents sessionName="ifxsession">
      <Event
          id="Ifx.PartASchema/MyNamespace.MyOperationEventPartC"
          eventName="MyOperationEventPartC" />
    </IfxEvents>
  </Events>
  <!-- Populate IFx PartA fields with provided values -->
  <EnvelopeSchema>
    <Field name="AppVer">"My_AppVer"</Field>
    <Field name="AppId">"My_AppId"</Field>
    <Field name="IKey">"My_IKey"</Field>
    <Extension name="Cloud">
      <Field name="Name">GetEnvironmentVariable("MONITORING_TENANT")</Field>
      <Field name="Role">GetEnvironmentVariable("MONITORING_ROLE")</Field>
      <Field name="RoleVer">"My_Cloud_RoleVer"</Field>
      <Field name="RoleInstance">GetEnvironmentVariable("MONITORING_ROLE_INSTANCE")</Field>
      <Field name="Environment">"My_Environment"</Field>
      <Field name="Location">"My_Region"</Field>
      <Field name="DeploymentUnit">"My_Cloud_DeploymentUnit"</Field>
    </Extension>
  </EnvelopeSchema>
```

### Performance Counters
We are not using any custom performance counters currently.  There are some counters which are not a part of the .Net Core CLR.  For any exsiting counters, if we can see the perf counters using Perf Mon then the Monitoring Agent can read those and we only need to add them to the MDS config to start consuming them.  

If we expose any perf counters via ETW, EventSource or Event Tracing those can also be picked up with the Monitoring agent and pushed to the Warm path.  We could then set up a connector with conversion rules for each log we want to turn into metrics.  These metrics would be availalble in the Hot path in 5-20 minutes after being emitted for the monitoring agent to collect.  Not ideal as you can no longer rely on those metrics for ICM events.  The Hot Path will only take new data up to 20 minutes old.  If the data is delayed for processing and ends up out of that window then the data is ignored in the Hot Path.

If we want counters like the error rate then we could consume the ILogger entries for Kesteral and use those to create metrics.  Then use the above process to allow emitting those metrics to the Hot Path.  I also did some digging in the Kestrel source code to see where they are logging to.  They do have an EventSource logger with the event name of "Microsoft-AspNetCore-Server-Kestrel".  It is more efficient to emit Ifx metrics from our code directly, but since kestrel is outside our code base this is an alternative.  There is a performance hit for collecting these metrics using Derived Events as it may overload the monitoring agent if we have too many Event rules or logs to process and we could end up losing logs as the Derived Event processing is low priority for the monitoring agent.  Using CentralBond, instead of locally on the machine, to do the Derviced Events is even worse and cannot handle much of a load for this process.


Here are the current Perf Counters we currently collect and push to Geneva Hot Path.  

``` JSON
"PerfCounter": {
        "Enabled": true,
        "MachinePerfCounters": {
            "\\Memory\\Available MBytes": "Available MBytes",
            "\\Paging File(_Total)\\% Usage": "% Total Paging File Usage",
            "\\Processor(_Total)\\% Processor Time": "% Total Processor Time"
        },
        "ProcessPerfCounters": {
            "\\Process(*)\\ID Process": "ID Process",
            "\\Process(*)\\% Processor Time": "% Processor Time",
            "\\Process(*)\\% Private Bytes": "% Private Bytes",
            "\\Process(*)\\IO Data Bytes/sec": "IO Data Bytes/sec",
            "\\Process(*)\\Thread Count": "Thread Count",
            "\\Process(*)\\Working Set": "Working Set",
            "\\Process(*)\\Working Set - Private": "Working Set - Private"
        }
    }
```

Here are some of the counters we are collecting via Warm Path (System Counters table)
``` XML
<!-- System Counters: this counter-set section collects the system counters for the node where service fabric is running. -->
        <!-- system CPU counters -->
        <Counter>\Processor(_Total)\% Processor Time</Counter>
        <Counter>\System\Processor Queue Length</Counter>
        <!-- system network counters -->
        <Counter>\TCPv4\Connections Active</Counter>
        <Counter>\TCPv6\Connections Active</Counter>
        <Counter>\TCPv4\Connections Passive</Counter>
        <Counter>\TCPv6\Connections Passive</Counter>
        <Counter>\TCPv4\Segments Sent/sec</Counter>
        <Counter>\TCPv6\Segments Sent/sec</Counter>
        <Counter>\TCPv4\Segments Received/sec</Counter>
        <Counter>\TCPv6\Segments Received/sec</Counter>
        <Counter>\TCPv4\Segments Retransmitted/sec</Counter>
        <Counter>\TCPv6\Segments Retransmitted/sec</Counter>
        <Counter>\TCPv4\Connection Failures</Counter>
        <Counter>\TCPv6\Connection Failures</Counter>
        <Counter>\TCPv4\Connections Reset</Counter>
        <Counter>\TCPv6\Connections Reset</Counter>
        <!-- system memory counters -->
        <Counter>\Memory\Available Bytes</Counter>
        <Counter>\Memory\Pages/sec</Counter>
        <Counter>\Memory\Page Reads/sec</Counter>
        <Counter>\Memory\Free System Page Table Entries</Counter>
        <Counter>\Memory\Committed Bytes</Counter>
        <!-- system disk counters -->
        <Counter>\PhysicalDisk(_Total)\Avg. Disk Bytes/Read</Counter>
        <Counter>\PhysicalDisk(_Total)\Avg. Disk Bytes/Write</Counter>
        <Counter>\PhysicalDisk(_Total)\Avg. Disk Read Queue Length</Counter>
        <Counter>\PhysicalDisk(_Total)\Avg. Disk Write Queue Length</Counter>
        <Counter>\PhysicalDisk(_Total)\Avg. Disk Queue Length</Counter>
        <Counter>\PhysicalDisk(_Total)\Avg. Disk sec/Read</Counter>
        <Counter>\PhysicalDisk(_Total)\Avg. Disk sec/Write</Counter>
        <Counter>\PhysicalDisk(_Total)\Disk Writes/sec</Counter>
        <Counter>\PhysicalDisk(_Total)\Disk Reads/sec</Counter>
        <Counter>\PhysicalDisk(_Total)\Disk Write Bytes/sec</Counter>
        <Counter>\PhysicalDisk(_Total)\Disk Read Bytes/sec</Counter>
        <!-- ETW counters -->
        <Counter>\Event Tracing for Windows Session(FabricTraces)\Events Lost</Counter>
```

In Geneva we are not able to log the following Perf Counters as they are not available as part of .Net Core
``` XML
        <Counter>\ASP.NET Applications(__Total__)\Requests/Sec</Counter>
        <Counter>\ASP.NET Applications(__Total__)\Request Wait Time</Counter>
        <Counter>\ASP.NET Applications(__Total__)\Requests Failed</Counter>
        <Counter>\ASP.NET Applications(__Total__)\Requests Rejected</Counter>
        <Counter>\ASP.NET Applications(__Total__)\Requests Total</Counter>
        <Counter>\ASP.NET Applications(__Total__)\Errors Total/Sec</Counter>
```

### ICM Logging
We can create correlation rules in ICM to replace the runbooks and AI Connectors we are using to trigger ICM alerts from Application Insights, as the data we need will exist in Geneva and ICM can work directly with this source.

### Data Aggregation and Existing queries
We are using two different solutions for data analytics PowerBI and Kusto.  We can continue to use Kusto after moving away from Application Insights.  There is a connector in Geneva we can use to stream logs to Kusto
https://genevamondocs.azurewebsites.net/connectors/Geneva%20to%20Kusto/overview.html 
Currently for PowerBI we use the Application Insights REST API to run Kusto queries for the data we need.  If we continue to use Kusto and stream the logs we want from Geneva, we can connect PowerBI to Kusto to create the dashboards that we need. https://docs.microsoft.com/en-us/azure/kusto/tools/powerbi 

### System Availability and Health
Right now, this is handled by the Cluster Agent service.  After the Monitoring Agent was setup, there are a lot of statndard health events collected for service fabric.  

Service Fabric Health Monitoring tables in Geneva
``` XML
<EtwProviders>
      <EtwProvider name="Microsoft-ServiceFabric-Monitoring-Health" format="EventSource" storeType="CentralBond">
        <!-- This section defines how the MA collects the EventSource events emitted by the MonitoringService.
             The ID for each event must match the value emitted from the service. -->
        <!-- HealthState events -->
        <Event id="1" eventName="ClusterHealthState"/>
        <Event id="2" eventName="AppHealthState"/>
        <Event id="3" eventName="NodeHealthState"/>
        <Event id="4" eventName="ServiceHealthState"/>
        <Event id="5" eventName="PartitionHealthState"/>
        <Event id="6" eventName="ReplicaHealthState"/>
        <Event id="7" eventName="DeployedApplicationHealthState"/>
        <Event id="8" eventName="DeployedServicePackageHealthState"/>
        <!-- HealthEvent events -->
        <Event id="9" eventName="ClusterHealthEvent"/>
        <Event id="10" eventName="AppHealthEvent"/>
        <Event id="11" eventName="NodeHealthEvent"/>
        <Event id="12" eventName="ServiceHealthEvent"/>
        <Event id="13" eventName="PartitionHealthEvent"/>
        <Event id="14" eventName="ReplicaHealthEvent"/>
        <Event id="15" eventName="DeployedApplicationHealthEvent"/>
        <Event id="16" eventName="DeployedServicePackageHealthEvent"/>
      </EtwProvider>
```

### Synthetic Web Tests and Alerts
We should use the Geneva Runner with for our external web tests.  We do not host the runner, we only upload it to Geneva after we write the code for it.  The Geneva Runner is .Net based and does not support .Net Core.  We can set up a runner instance in multiple regions and have it ping a list of endpoints.  We will register the namespace (What we want to name the runner as it will be the same as the project name as well) with our warm path so when we deploy it to Geneva the correct account to push the data can be determined.  To look up the data once it is processing we will need to go to "Logs" select the "Namespace" for that runner and look at the "RunnerCentralEventTable".  We can deploy this Runner to multiple different regions (Geneva has 40 some are backups and some are for specific implementations) so we can still ping from at least 16 different instances like we are currently doing for AI.

To get the active endpoints we want to run tests on, we could create an API to get the endpoints that have been created for that environment, for the runner to access, and have a service to periodically update the list.  Then the Runner services can contact the API to get the list of endpoints they need to ping.  Or we can just have a service that does the aggregation of the active endpoints and places the list in KeyVault.  Then the monitor can connect to KeyVault and grab the list periodically.  Say every 5 or 10 minutes.  We will have to think about what we want to do for deprovisioning to reduce the number of false positives for ICM alerting.  We could decrease the time inbetween recreation of the endpoint list and if we get an endpoint failure on the Runner side we can have it grab the list again to see if that endpoint stills exists before reporting it as an actual failure.

### Logging table names
There is a decision to make here.  We can either reuse the Application Insights table names we have in Geneva right now or use the new ones we will create for the new logs we pick up using the Monitoring Agent.  Since we are planning on doing a multi-phase approach, I would suggest using the new ones we create.  Otherwise, we will need to update the ICM correlation rules, Kusto queries and anything else that is reliant on the table names.  There also could be a column name conflict, with the old tables, given how the new logs will be captured.

### Web Requests
Currently we are logging to both AI and using the IFX Audit logger for Geneva.  AI will log to the “Requests” table and the IFX logger will log to "AsmIfxAuditApp" and "AsmIfxAuditDiag".  For other services they are logging only using Application Insights.  We can use IFX Operational logging to track requests for success and failures.  The correlation Context is built into the IFX Operational logger at the thread level and we can use the IFX Correlation Context to pass between processes or machine boundaries to track the request all the way through.  To get the debug information we can extend the Operational Schema with IFX custom events in order to add stack traces and other properties we want.

For the correllation Id we associate web requests and audit logs using Applications Insights libraries to handle this.  We can use the IFx CorrelationContext API from genevea to pass around a context to be able to associate our logs.  We can set a correlation Id we create and send with the intial request as the System.Diagnostics.Trace.CorrelationManager.ActivityId property. The API can then save the context to a serialized blob and return that context with the ActivityId which can be passed through the header of our web calls.  Then the receiving service can call to deserialize and use that context given the ActivityId.  Then that context information can be added to the logs we generate where needed.
https://genevamondocs.azurewebsites.net/collect/references/ifxref/ifxCorrelationContext.html 

### Items of note about the current implementation for logging

The AI Connector for Geneva is not supported for the Test Geneva endpoint.  So we can only send the Prod AI logs to Geneva, which can be annoying having to look in a different place for logs in the test environment.

For the IFX Audit logger we only have a single column to use which makes querying the data more of a chore and slower with all the parsing of the data in the column.  The audit logger was not meant for debugging logs.  We should use custom IFX events for debugging data.  This would allow us to create custom events and give us more control over the content of the log. See ETW Logging section above.

### Common Properties for new Event Schema Logs
* Standard set of environment information for all logs
    - ServiceFabric ServiceTypeName
    - ServiceFabric ServiceName
    - ServiceFabric ServiceManifestVersion
    - ServiceFabric ReplicaOrInstanceId
    - ServiceFabric NodeType
    - ServiceFabric NodeName
    - ServiceFabric PublishAddress
    - ServiceFabric ClusterName
    - ServiceEnvironment GroupName
    - ServiceEnvironment Name
    - ServiceEnvironment AzureSubscriptionName
* Custom Dimensions field to contain a key:value json string
    - This will contain any data specific to a particular service that is useful.
    - This will include the original log message, containing the key-value pairs, and the format string used for the log.

## Test Strategy
We can create separate tables in the Dgrep so we can compare data flowing in from Application Insights and what we are capturing through the Monitoring Agent.  Once confirmed we can turn off the Application Insights logging.

For ICM alerting we can set the new alerts to Sev 4 just so we can check we are getting the same type of alerts that are triggered from AI.
 
## Security
We should make sure that we are still filtering PII data from the logs the same way we have been doing for Application Insights.  Geneva offers the ability to scrub PII from logs in multiple ways before sending data to Cosmos and Kusto (https://genevamondocs.azurewebsites.net/connectors/cosmos/piiscrubbing.html ).  We also can lock down specific tables to be read-only for certain groups, if necessary.  
Also, we should be using security groups in Geneva to limit access for read, write and admin privileges.
 
## Other
We are only changing the mechanism used to capture the logs.  The logs are still going to Geneva as before little to no impact on SOPs.  There will be some minor documentation updates in the architecture document regarding logging and telemetry.  There will also be some small changes to deployment to prevent spinning up of AI assets that we will no longer need.


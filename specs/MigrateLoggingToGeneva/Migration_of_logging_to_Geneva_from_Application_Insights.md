# Migration of logging to Geneva from Application Insights

## Description

The purpose is to have all logging captured by the Geneva Monitoring Agent rather than having some logs captured by Application Insights and some captured by the Monitoring Agent.  Having logs available in Geneva is a current Azure Security requirement as well as needed by Customer Service to debug current issues and file appropriate tickets. 

## High-level Design

We will do this work in phases.  For a time, we will have logs from both AI and the Monitoring Agent.  We will send the monitoring agent logs (where applicable) to their own tables so we do not make it look like there are twice as many exceptions.  Once we are satisfied that we are capturing what we need, we can start to add our ICM alerting, adjust or move queries to use the new tables and set up new synthetic transactions.  The final phase will be to remove the Application Insights logging.

### Phase 0
In this phase we will be prototyping a few things to make sure it aligns with what we need for PaaS and OSS.  
* Kestrel Logging and turning it into metrics. 
* IFX Custom Event logging and the ILogger implmentation.
* 1DS SDK and new Microsoft.Logging.Extensions.ApplicationInsights LoggingProvider.

### Phase 1
In this phase the goal is to reach 100% parity with what we are currently logging for Application Insights.  This includes ETW logs, Perf Counters, health reporting and web requests.  We will upgrade the current Application Insights nuget packages to add support for using telemetry channels for both Geneva and AI. This will allow using the current AI hooks, but also emitting that data directly to the Geneva monitoring agent.  This is helpful as in phase 3 we can just remove the AI channel from all the projects to stop emitting to AI in Azure. After the upgrade, we will tackle a single service at a time, by adding both channels and the trackTrace and trackException code (as an ILogger implementation). This is done to get the custom properties from our enrichers as the new AI Logging Provider does not support adding a modified TelemetryConfiguration, which is where our custom properties are stored and this would allow only supporting the default AI in memory channel.  Once all services have reached an appropriate level of parity we will move to Phase 2.

### Phase 2
Here we will create our ICM Alerting correlation rules based on the new tables in Geneva.  Set up the connector to stream logs from Geneva to Kusto and then from Kusto to PowerBI.  Then update all our existing Kusto and PowerBI queries. We will also replace the current AI synthetic web tests with Geneva Synthetics.

### Phase 3
We will remove the AI channel from all projects, clean up the AI connector and AI tables from Geneva, remove AI ICM alerts where applicable and clean up any necessary Azure assests (runbooks, webtests, and Icm Connector) for AI and update deployment scripts and ARM templates where needed.

### ETW logging
There are several different approaches here.  There is instrumenting with IFX, logging straight to EventSource, or using the 1DS SDK. After prototyping all 3 options using the 1DS SDK (updated Application Insights nugets to use telemetry channels) was chosen.  This would allow using the existing Application Insights code and adding telemetry channels for emitting the data.  We will add a channel for AI and one for Geneva.  There is some code updating as some of the AddApplicationsInsights extensions are being removed in favor of a new extension.  This is due partially because the AI Logging provider has been split off into the Microsoft.Logging.Extensions namespace.  The new provider does not use any modified TelemetryConfiguration, it creates a default configuration that uses the AI in memory channel only and this behavior cannot be overridden. As an alternative, we will implement a LoggingProvider that uses TelemetryConfiguration.TrackTrace and TelemetryConfiguration.TrackException.  This will allow us to use our existing Logger messages and will allow us to use the logger enrichers and filters we already have for telemetry.  The custom properties will show up each as their own column in the DGrep TracingTelemetry and ExceptionTelemetry respectively.

IFX was not chosen as it is not as actively being worked on as Geneva seems to be pushing people to use 1DS to instrument if they are not already using IFX. Also, the IFX Correlation API does not have any tools available to support using the correlation vector that is generated.  The alternative is to use Codex which is expensive and overkill for our use of correlation vector and correlation id.  IFX does offer creating a custom event schema, but would require us to create new enrichers and log filters to work with IFX events and operations.  Also, maintaining a bond file for custom event and operation schemas can get annoying as any new common property we want to add will require modification to both code and the bond file.

For EventSource we would have to capture all events from the Applications event log table and filter by the event names we want, either at the Monitoring agent config level or in DGREP when querying the tables.  EventSource has a standard schema which is a plus and minus. The downside is we only get one field which is a standard string and we would have to format our messages to make them more easily parsable. 

Example MDS Config (This is a snipet of what would be added for the events captured by the Geneva channel using the 1DS SDK)

``` XML
<Events>
    <OneDSProviders>
      <OneDSProvider name="Microsoft.OndeDSProviderTest" storeType="CentralBond">
        <DefaultEvent eventName="OtherEvents" />
        <Event eventName="AvailabilityTelemetry" />
        <Event eventName="DependencyTelemetry" />
        <Event eventName="EventTelemetry" />
        <Event eventName="ExceptionTelemetry" />
        <Event eventName="MetricTelemetry" />
        <Event eventName="PageViewTelemetry" />
        <Event eventName="RequestTelemetry" />
        <Event eventName="TraceTelemetry" />
      </OneDSProvider>
    </OneDSProviders>
  </Events>
```

### Performance Counters
We are not using any custom performance counters currently.  There are some counters which are not a part of the .Net Core CLR.  For any exsiting counters, if we can see the perf counters using Perf Mon then the Monitoring Agent can read those and we only need to add them to the MDS config to start consuming them.  

We can use the TelemetryConfiguration.GetMetric to create and track metrics.  These metrics can be emitted to the monitoring agent and pushed to the HotPath as long as the MDM Account is configured in the MDS Config.

An alternative and less efficient eay is to expose any perf counters via ETW, EventSource or Event Tracing those can also be picked up with the Monitoring agent and pushed to the Warm path.  We could then set up a connector with conversion rules for each log we want to turn into metrics.  These metrics would be availalble in the Hot path in 5-20 minutes after being emitted for the monitoring agent to collect.  Not ideal as you can no longer rely on those metrics for ICM events.  The Hot Path will only take new data up to 20 minutes old.  If the data is delayed for processing and ends up out of that window then the data is ignored in the Hot Path.

As for trying to capture and use the Kestrel ILogger logs to create metrics it would be better to track these using TelemetryConfiguration.GetMetric, which is the most efficient way to track these metrics.  At least for the metrics that the AI telemtry modules are not already emitting.

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
We can create correlation rules in ICM to replace the runbooks and AI Connectors we are using to trigger ICM alerts from Application Insights, as the data we need will exist in Geneva and ICM can work directly with this source. We can still keep the IcmAlert = true in our logs for logs we know we want to alert on immediately and require no aggregation of errors over a certain period. It is a pure preference how we chose to manage it.

### Data Aggregation and Existing queries
We are using two different solutions for data analytics PowerBI and Kusto.  We can continue to use Kusto after moving away from Application Insights.  There is a connector in Geneva we can use to stream logs to Kusto
https://genevamondocs.azurewebsites.net/connectors/Geneva%20to%20Kusto/overview.html 
Currently for PowerBI we use the Application Insights REST API to run Kusto queries for the data we need.  If we continue to use Kusto and stream the logs we want from Geneva, we can connect PowerBI to Kusto to create the dashboards that we need. https://docs.microsoft.com/en-us/azure/kusto/tools/powerbi 

### System Availability and Health
Right now, this is handled by the Cluster Agent service.  After the Monitoring Agent was setup, there are a lot of standard health events collected for service fabric.  

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
We should use the Geneva Synthetics (formally Geneva Runner) for our external web tests.  We do not host the runner, we only upload it to Geneva after we write the code for it.  The Geneva Runner is .Net based and does not support .Net Core.  We can set up a runner instance in multiple regions and have it ping a list of endpoints.  We will register the namespace (What we want to name the runner as it will be the same as the project name as well) with our warm path so when we deploy it to Geneva the correct account to push the data can be determined.  To look up the data once it is processing we will need to go to "Logs" select the "Namespace" for that runner and look at the "RunnerCentralEventTable".  We can deploy this Runner to multiple different regions (Geneva has 40 some are backups and some are for specific implementations) so we can still ping from at least 16 different instances like we are currently doing for AI.

To get the active endpoints we want to run tests on, we could create an API to get the endpoints that have been created for that environment, for the runner to access, and have a service to periodically update the list.  Then the Runner services can contact the API to get the list of endpoints they need to ping.  Or we can just have a service that does the aggregation of the active endpoints and places the list in KeyVault.  Then the monitor can connect to KeyVault and grab the list periodically.  Say every 5 or 10 minutes.  We will have to think about what we want to do for deprovisioning to reduce the number of false positives for ICM alerting.  We could decrease the time inbetween recreation of the endpoint list and if we get an endpoint failure on the Runner side we can have it grab the list again to see if that endpoint still exists before reporting it as an actual failure.

### Logging table names
There is a decision to make here.  We can either reuse the Application Insights table names we have in Geneva right now or use the new ones we will create for the new logs we pick up using the Monitoring Agent.  Since we are planning on doing a multi-phase approach, I would suggest using the new ones we create.  Otherwise, we will need to update the ICM correlation rules, Kusto queries and anything else that is reliant on the table names.  There also could be a column name conflict, with the old tables, given how the new logs will be captured.

### Web Requests
Currently we are logging to both AI and using the IFX Audit logger for Geneva.  AI will log to the “Requests” table and the IFX logger will log to "AsmIfxAuditApp" and "AsmIfxAuditDiag".  For other services they are logging only using Application Insights.  We can continue to use the same AI telemetry modules we are currently using for web requests as those will be captured and emitted to the Geneva channel and picked up by the monitoring agent.

For the correllation Id and correlation vector we continue to use the same logic AI is using.

### Items of note about the current implementation for logging

The AI Connector for Geneva is not supported for the Test Geneva endpoint.  So we can only send the Prod AI logs to Geneva, which can be annoying having to look in a different place for logs in the test environment.

For the IFX Audit logger we only have a single column to use which makes querying the data more of a chore and slower with all the parsing of the data in the column.  The audit logger was not meant for debugging logs. See ETW Logging section above for what we plan to do regarding diagnostic logs.

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


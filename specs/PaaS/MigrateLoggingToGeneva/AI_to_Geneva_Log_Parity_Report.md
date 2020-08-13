# Application Insights to Geneva Log Parity Report

## Purpose
This report will detail the type of information that we want to represent from the Geneva logs in order to claim we have achieved parity between the two sources. This does not mean that everything we log with AI will need to be moved over to Geneva as some information is not useful from a business or support perspective.  

## ICM Alerting
We currently trigger IcM alerts off of the below data or scenarios.  I believe some of these alerts will never trigger due to a bug, in the case of log triggered alerts.  We have an ICM Connector that polls through the logs every 5 minutes and looks for an environment setting "AlertingEnabled", if true it will create an ICM alert for any log that contains "IcM alert: true" .  The ResourceProviderWorker has this backwards. 

### Log Triggered ICM Alerts
* Cosmos DB account {ServiceEndpoint} write region failover detected from {FromWriteEndpoint} to {ToWriteEndpoint}. IcM alert: {AlertIcM}
* Cosmos DB account {ServiceEndpoint} read region failover detected from {FromReadEndpoint} to {ToReadEndpoint}. IcM alert: {AlertIcM}.
* Cosmos DB collection {Collection} in account {Account} has conflict feed entries. IcM alert: {AlertIcM}
* ResourceProviderWorker: There are now {{resourceCount}} '{azureLimit.ResourceType}'. Alerting is set to trigger at {{alertThreshold}}. Look in the TSG for increasing the subscription's quota. Alert IcM: {{AlertIcM}}

### Web Test Triggered Alerts
These alerts ping a url from 16 different Geo locations every 5 minutes.  The expectations is a 200 response from the "health/check" url with a max timeout for the request of 120 seconds. If more than 5% of the locations fail then an ICM alert is triggered.  
* FHIR Server per cluster
* Traffic Manager. For verifying we are routing to the correct domain
* ARM Resource Provider Service

## Dashboard Data

### Power BI
Currently in our PowerBI dashboards we provide the following.
* Account by Date
* Account by Hour
* Account Mapping
* Exceptions by Cluster
* Requests by Account by Hour
* Performance bucket by Hour
* Operations by Hour
* Exceptions over last day with our stack
* Exceptions over last day with our stack that we have not seen
* Exceptions over last day not in our stack
* Requests by HTTP status
* Requests by State Or Province
* Provision Operations by Hour
* Provision Worker

### Kusto Queries
* Exceptions in the last day where our code is in the stack trace in the Test Environment
* Exceptions that started occurring in the last day where our code is in the stack trace in the Test Environment

### Metric Counters
```XML
        <!-- System counters -->
        <Counter>\Memory\Available MBytes</Counter>
        <Counter>\System\System Calls/sec</Counter>
        <Counter>\Processor(_Total)\% Processor Time</Counter>
        <Counter>\Process(_Total)\Working Set</Counter>
        <Counter>\LogicalDisk(*)\% Free Space</Counter>
        <Counter>\LogicalDisk(*)\% Disk Read Time</Counter>
        <Counter>\LogicalDisk(*)\% Disk Write Time</Counter>
        <!-- Azure Worker -->
        <Counter>\Process(WaWorkerHost*)\Elapsed Time</Counter>
        <Counter>\Process(WaWorkerHost*)\% Processor Time</Counter>
        <Counter>\Process(WaWorkerHost*)\Thread Count</Counter>
        <Counter>\Process(WaWorkerHost*)\IO Read Bytes/sec</Counter>
        <Counter>\Process(WaWorkerHost*)\IO Write Bytes/sec</Counter>
        <Counter>\Process(WaWorkerHost*)\Working Set</Counter>
        <Counter>\Process(WaWorkerHost*)\Private Bytes</Counter>
        <Counter>\Process(WaWorkerHost*)\Virtual Bytes</Counter>
        <Counter>\Process(WaWorkerHost*)\Page Faults/sec</Counter>
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
        <!-- ASP.Net Core Counters -->
        <Counter>\ASP.NET Applications(__Total__)\Requests/Sec</Counter>
        <Counter>\ASP.NET Applications(__Total__)\Request Wait Time</Counter>
        <Counter>\ASP.NET Applications(__Total__)\Requests Failed</Counter>
        <Counter>\ASP.NET Applications(__Total__)\Requests Rejected</Counter>
        <Counter>\ASP.NET Applications(__Total__)\Requests Total</Counter>
        <Counter>\ASP.NET Applications(__Total__)\Errors Total/Sec</Counter>
```

## Service Health
* Health\Check results per deployed resource:
    - node
    - cluster
    - application
    - service
    - partition
    - replica

## Diagnostic Information
* Stack traces from failed operations and requests when available
* Correlation Ids for all logs
* Standard set of environment information for all logs
    - ServiceFabricServiceTypeName
    - ServiceFabricServiceName
    - ServiceFabricServiceManifestVersion
    - ServiceFabricReplicaOrInstanceId
    - ServiceFabricNodeType
    - ServiceFabricNodeName
    - ServiceFabricPublishAddress
    - ServiceFabricClusterName
    - ServiceEnvironmentGroupName
    - ServiceEnvironmentName
    - ServiceEnvironmentAzureSubscriptionName
* Custom Dimensions field to contain a key:value json string
    - This will contain any data specific to a particular service that is useful.
    - This will include the original log message, containing the key-value pairs, and the format string used for the log.  
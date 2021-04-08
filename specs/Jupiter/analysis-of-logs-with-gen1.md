# Geneva telemetry between Gen 1 and Jupiter

Currently the Gen 1 infrastructure holds both provisioning code as well as customer compute. Logs are created and stored within Geneva to account for this. Jupiter introduces significant differences to this approach:

- Customer compute for DICOM and IomT is now hosted on AKS infrastructure versus Service Fabric. Fhir related instances are still hosted in Service Fabric.
- Provisioning code is separate and continues to live on Service Fabric
- AKS Infrastructure is Linux-based
- Logging for customer compute and AKS infrastructure is captured inside of a separate Geneva Namespace

This document details the logs used for Gen 1 and attempts to guide to an Jupiter equivalent. Where application it attempts to answer:

- What is the equivalent log table in Jupiter?
- If there is not an equivalent table are there plans to port the table to Jupiter?

## Geneva Namespaces

Jupiter logs exist inside of the following Account/Namespace within Geneva

| Environment | Account Name    | Namespace      |
|-------------|-----------------|----------------|
| Dev/Test    | ResoluteNonProd | MSHApi2NonProd |
| Prod        | ResoluteProd    | MSHApi2Prod    |

## AKS Telemtry

All telemetry produced by AKS is stored within Azure Monitor. Telemetry for individual pods/containers are stored using Container Insights which is also located in Azure Monitor. 
At this time, the telemety contained within Azure Monitor is not present inside of Geneva.

[Azure Monitor (Dev/Test Environments)](https://ms.portal.azure.com/#blade/Microsoft_Azure_Monitoring/AzureMonitoringBrowseBlade/containerInsights)

### Container logs

All logs written to the output or error stream from within a container get stored within the **KubernetesContainers** table within the Jupiter Geneva Namespace.

## Service Fabric and AKS

It should be noted that a large amount of telemetry in Gen 1 comes from Service Fabric. Service Fabric and AKS are different PaaS offerings and as such produce different telemetry and logs. There may not be an equivalent between a Service Fabric log and what is produced by AKS. In general, when attempting to find a similar log, look inside of Azure Monitor.

## Gen 1 Telemetry to Jupiter

For further details into the nature of the Gen 1 tables please see this [document](https://microsoft.sharepoint.com/teams/msh/_layouts/OneNote.aspx?id=%2Fteams%2Fmsh%2FShared%20Documents%2FNotebooks%2FResolute%20Engineering&wd=target%28Operations%2FHow%20to.one%7CD4D99CD6-8FB8-492B-ADE2-F4FCCDCDD552%2FHow%20to%20query%20in%20Geneva%20Dgrep%20Logs%7C7A46E197-4BB9-45A0-AFF1-7E9F53787C91%2F%29).

**Note**: The advice to look at Azure Monitor for a specific Gen 1 table does not guarantee that the telemetry will actually exist. Differences between Service Fabric and AKS mean that like telemetry may be note produced, or that Azure Monitor may not surface the data if it does exist.

**Note** AzSecPack related tables are not listed here. They are being collected inside of the Jupiter Geneva Namespace in tables of the form LinuxASM*XXX*

| Gen 1 Log Table                     | Equivalent Jupiter Table | Notes                                                                                                |
|-------------------------------------|--------------------------|------------------------------------------------------------------------------------------------------|
| Account Telemetry                   | N/A                      | RP Worker related telemetry. Will not port                                                           |
| AppHealthEvent                      | N/A                      | Azure Monitor                                                                                        |
| AppHealthSate                       | N/A                      | Azure Monitor                                                                                        |
| ApplicationCounters                 | N/A                      | Azure Monitor                                                                                        |
| ApplicationEvents                   | N/A                      | Windows Event logs. Will not port                                                                    |
| AsmIfxAuditApp                      | N/A                      | Fhir Server Audit logs. Will not port                                                                |
| AzureMonitorAudit                   | N/A                      | Currently in development                                                                             |
| AzureMonitorAuditShoebox            | N/A                      | Currently in development                                                                             |
| ClusterHealthEvent                  | N/A                      | Azure Monitor                                                                                        |
| ClusterHealthState                  | N/A                      | Azure Monitor                                                                                        |
| CounterTable                        | N/A                      | Azure Monitor                                                                                        |
| DependencyTelemetry                 | LinuxDependencyTelemetry |                                                                                                      |
| DiscreteDiskCounters                | N/A                      | Azure Monitor                                                                                        |
| ExceptionTelemetry                  | LinuxExceptionTelemetry  |                                                                                                      |
| FabricComponentCounters             | N/A                      | Service Fabric Specific. Will Not Port                                                               |
| FabricCounters                      | N/A                      | Service Fabric Specific. Will Not Port                                                               |
| IISCounters                         | N/A                      | Will Not Port.                                                                                       |
| IISLogs                             | N/A                      | Will Not Port                                                                                        |
| LocallyAggregatedMdmMetricsV1       | N/A                      | Will Not Port                                                                                        |
| MaCounterSummary                    | N/A                      | [Windows only](https://genevamondocs.azurewebsites.net/collect/manage/agentdata.html). Will Not Port |
| MaErrorsSummary                     | N/A                      | [Windows only](https://genevamondocs.azurewebsites.net/collect/manage/agentdata.html). Will Not Port |
| MaHeartBeats                        | N/A                      | [Windows only](https://genevamondocs.azurewebsites.net/collect/manage/agentdata.html). Will Not Port |
| MaQosSummary                        | N/A                      | [Windows only](https://genevamondocs.azurewebsites.net/collect/manage/agentdata.html). Will Not Port |
| LocallyAggregatedMdmMetricsV1       | N/A                      | Will Not Port                                                                                        |
| MetricTelemetry                     | LinuxMetricTelemetry     |                                                                                                      |
| NodeHealthEvent                     | N/A                      | Azure Monitor                                                                                        |
| NodeHealthState                     | N/A                      | Azure Monitor                                                                                        |
| PartitionHealthEvent                | N/A                      | Azure Monitor                                                                                        |
| PartitionHealthState                | N/A                      | Azure Monitor                                                                                        |
| PerfCounters                        | N/A                      | Azure Monitor                                                                                        |
| ReplicatHealthEvent                 | N/A                      | Azure Monitor                                                                                        |
| ReplicatHealthState                 | N/A                      | Azure Monitor                                                                                        |
| RequestMetrics                      | N/A                      | RP Worker related telemetry. Will not port                                                           |
| RequestTelemetry                    | LinuxRequestTelemetry    |                                                                                                      |
| ServiceFabricMonitoringServiceEvent | N/A                      | Service Fabric Specific. Will Not Port                                                               |
| ServiceFabricOperationalEvent       | N/A                      | Service Fabric Specific. Will Not Port                                                               |
| ServiceFabricReliableServiceEvent   | N/A                      | Service Fabric Specific. Will Not Port                                                               |
| ServiceHealthEvent                  | N/A                      | Azure Monitor                                                                                        |
| ServiceHealthState                  | N/A                      | Azure Monitor                                                                                        |
| SystemCounters                      | N/A                      | Azure Monitor                                                                                        |
| SystemEvents                        | N/A                      | Azure Monitor                                                                                        |
| TraceTelemetry                      | LinuxTraceTelemetry      |                                                                                                      |

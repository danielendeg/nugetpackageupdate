*This is an inventory of loggers used in PaaS*

[[_TOC_]]

# Business Justification

This is used to document the loggers used in PaaS. Ideally we can all agree on what we should use as a team. We will list out all the loggers and their current use / requirements, and then decide on what makes sense.

# Scenarios
 The there are currently logs and metrics that are available internally for troubleshooting/DRI (available in Application Insights, Geneva, Kusto). There are also logs and metrics exposed to customers (available in Azure Monitor (aka Shoebox)). We should think about can consolidating the logging mechanisms across DICOM, IoT, and FHIR.
- Metrics (Internal and Customer facing)
- Audit Logs (Internal and Customer facing)
- Diagnostics Logs (Internal and Customer facing)
- Trace Telemetry (Internal)
- Exception Telemetry (Internal)
- Request Telemetry (Internal)

# Inventory of loggers

**Audit Loggers used in PaaS**

| Inventory Item                              | Repo                    | Package                                   | Notes                            | Link               |
|---------------------------------------------|-------------------------|-------------------------------------------|----------------------------------|--------------------|
| IfxAuditLogger / ILogAudit                  | workspace-platform      | Microsoft.IFxAudit                        | DICOM audit logging              | [IfxAuditLogger](https://microsofthealth.visualstudio.com/Health/_git/workspace-platform?path=%2Fshared-packages%2Fifxauditlogger%2FIfxAuditLog%2FIfxAuditLogger.cs&_a=contents&version=GBmain) [ILogAudit](https://microsofthealth.visualstudio.com/Health/_git/workspace-platform?path=%2Fshared-packages%2Fifxauditlogger%2FIfxAuditLog%2FILogAudit.cs&_a=contents&version=GBmain)
| IfxAuditLogger / ILogAudit                  | health-paas             | Microsoft.Cloud.InstrumentationFramework  | Azure API for FHIR audit logging | [IfxAuditLogger](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fsrc%2FCommon%2FServicePlatform%2FAudit%2FIfxAuditLogger.cs&_a=contents&version=GBmaster) [ILogAudit](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fsrc%2FCommon%2FServicePlatform%2FAudit%2FILogAudit.cs&_a=contents&version=GBmaster)

Note: IfxAuditLogger is for internal security audits. There is a required schema and audit logs must be emitted using the IfxAudit library.


**Diagnostic Loggers used in PaaS**

| Inventory Item                                            | Repo                    | Package                            | Notes                                   | Link               |
|-----------------------------------------------------------|-------------------------|------------------------------------|-----------------------------------------|--------------------|
| AzureMonitorDiagnosticLogger / ILogDiagnostic             | health-paas             | Microsoft.Extensions.Logging       | Azure API for FHIR customer facing logs | [AzureMonitorDiagnosticLogger](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fsrc%2FCommon%2FServicePlatform%2FDiagnostics%2FAzureMonitorDiagnosticLogger.cs) [ILogDiagnostic](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fsrc%2FCommon%2FServicePlatform%2FDiagnostics%2FILogDiagnostics.cs)
| OneSdkLogger                                              | health-paas             | 1DS (Microsoft.ApplicationInsights)| Azure API for FHIR  internal logs       | [OneSdkLogger](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fsrc%2FCommon%2FServicePlatform%2FLogging%2FOneSdkLogger.cs)
| IomtShoeboxTelemetryLogger                                | health-paas             | 1DS (Microsoft.ApplicationInsights)| IoT Connector internal logs             | [IomtShoeboxTelemetryLogger](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fsrc%2FIomtConnector%2FIomtConnector%2FLogging%2FIomtShoeboxTelemetryLogger.cs)

todo: document what Dicom uses for application logging and metrics. I believe it is 1SDK. [docs](https://microsofthealth.visualstudio.com/Health/_git/workspace-platform?path=%2Fdocs%2Fk8s-logs-metrics-investigation.md&_a=preview&version=GBmain)


**These are the current Metrics Loggers used in PaaS**

| Inventory Item                                      | Repo                    | Notes                              | Link               |
|-----------------------------------------------------|-------------------------|------------------------------------|--------------------|
| IMetricLogger / IMetricLoggerFactory                | health-paas             | Azure API for FHIR metrics         | Example: CosmosDbStorageSizeMetricLogger
| IBackendServiceRequestMetricLogger                  | health-paas             | Azure API for FHIR service metrics |                                 |
| IServiceMetricLoggerFactory                         | health-paas             | Azure API for FHIR service metrics |                                 |
| ITotalMetricsLoggerFacadeFactory                    | health-paas             | Azure API for FHIR Shoebox Metrics | [TotalMetricsShoeboxMetricLoggerFactory](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fsrc%2FFrontendApplication%2FFrontendService%2FMetrics%2FTotalMetricsShoeboxMetricLoggerFacadeFactory.cs&_a=contents&version=GBmaster) [TotalMetricsShoeboxMetricLoggerFacade](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fsrc%2FFrontendApplication%2FFrontendService%2FMetrics%2FTotalMetricsShoeboxMetricLoggerFacade.cs&_a=contents&version=GBmaster)
| IShoeboxMetricLoggerFactory                         | health-paas             | Azure API for FHIR Shoebox Metrics | [TotalMetricsShoeboxMetricLoggerFactory](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fsrc%2FFrontendApplication%2FFrontendService%2FMetrics%2FTotalMetricsShoeboxMetricLoggerFacadeFactory.cs&_a=contents&version=GBmaster) [TotalMetricsShoeboxMetricLoggerFacade](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fsrc%2FFrontendApplication%2FFrontendService%2FMetrics%2FTotalMetricsShoeboxMetricLoggerFacade.cs&_a=contents&version=GBmaster)
| IomtShoeboxTelemetryLogger                          | health-paas             | IoT Connector Logging and Metrics  | [IomtShoeboxTelemetryLogger](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fsrc%2FIomtConnector%2FIomtConnector%2FLogging%2FIomtShoeboxTelemetryLogger.cs)

todo: are there more?

**Platform metrics collectors used in PaaS**

todo: document how platform metrics are collected (service fabric, aks, etc)

# Other
It would be nice to unify logging and metrics implementations across Dicom, IoT, and FHIR.

These are a few things that we would need to work through. Will create a separate more detailed spec with the following items.

| Item                   | Notes                                                                          |
|------------------------|--------------------------------------------------------------------------------|
| .NET Version           | .NET 3.1 vs .NET 5.0. Currently IoT is limited in OSS to .NET Core 3.1 due to Azure Functions only supporting the 3.1 runtime. |
| Logging Package(s)     | Decide on package(s) (1DS/IFx/ETW). 1DS seems to be used for metrics for all platforms, IFx seems to be used by Dicom for Audit logs in PaaS (unsure about non-audit logs), Event Source/ETW seems to be used FHIR using for logs in PaaS. 1DS seems to have a limitation with only being able to create logs with TraceTelemetry. How to selectively send only some TraceTelemetry to Shoebox. |
| Framework Location     | Should it live in OSS or be internal? Should certain parts live in OSS, and other parts be internal (e.g. Shoebox)? What about the OSS healthcare-shared-components? |
| Logging Requirements   | Instrumentation of logging/metrics in code, timers, properties to be included in logs, metrics dimensions  |
| Internal vs. External  | Need a common way to determine if a metric or log should be able to be viewed by customers, or if it should be internal only |
| Metrics definitions    | Need a common way for metrics and dimensions to be defined and logged. How to log in OSS and also in PaaS with required Shoebox dimensions |

# Possible Solutions for Jupiter

Note: ideally these classes will be put in their own repo and we release nuget packages for them.

| Item                   | Audience               | Package                                                                                  |
|------------------------|------------------------|------------------------------------------------------------------------------------------|
| Metrics                | Internal and Customer  | 1SDK with an extension to set properties required for Shoebox (resourceIds, categories, etc)
| Audit Logs             | Internal and Customer  | IfxAudit (required to use this package by auditing)
| Diagnostic Logs        | Internal and Customer  | Custom Ifx implementation (AzureMonitorShoeboxLogger). Allows us to easily differentiate between internal only logs (logged by 1SDK) and logs intended for customer based on the event source.
| Internal Logs          | Internal               | 1SDK does a good job with (TraceTelemetry, RequestTelemetry, ExceptionTelemetry)   
| Platform telemetry     | Internal               | Dependent on platform stack.

# Links
- [Jupiter workspace logs and metrics design - In Progress](https://microsofthealth.visualstudio.com/Health/_git/health-paas-docs?path=%2Fspecs%2FShoebox%2Fjupiter-shoebox.md)
- [Gen 1 FHIR diagnotic logs/shoebox design](https://microsofthealth.visualstudio.com/Health/_wiki/wikis/Resolute.wiki/43/DiagnosticLoggingDesignSpec)
- [Gen 1 FHIR shoebox logs design](https://microsofthealth.visualstudio.com/Health/_git/health-paas-docs/pullrequest/13471?path=%2Fspecs%2FShoebox%2FShoebox%20-%20Application%20log%20Design.md)
- [Gen 1 FHIR AuditLogs Log Analytics Schema](https://microsofthealth.visualstudio.com/Health/_wiki/wikis/Resolute.wiki/56/LASchema)

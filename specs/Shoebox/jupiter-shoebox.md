# Logging and Shoebox for Healthcare APIs

## Enable customers to configure and export audit logs and metrics

# Scenario Contacts 

**Program Manager (PM):** Benjamin Xue, Chami Rupasinghe

**Software Engineer (Dev):** Scott Taladay, Dustin Burson

# Why There is a Gap Today? (PM) 
Section status: Draft

Date reviewed: 05/17/21

## Problem Statement 
Customers must be able to query logs to understand how their instance of Azure Healthcare APIs has been used. They would query their logs to derive insights like, the types and frequency of errors, who transacted with the data, and understand basics about the service like size of their DB, latency, and number of requests made through each end-point. 

Azure Healthcare APIs has evolved to a platform that hosts a collection of data services, FHIR, DICOM, and IoT Connector, each generating their own log data. It is highly likely that many customers will transact with multiple or all data services that are hosted. It is therefore prudent to ensure a consistent experience when customers are querying logs or viewing "shoebox" metrics. A consistent experience will require a consistent naming convention for equivalent attributes in log tables and a set of common metrics across data services. In addition to common attributes names and common metrics, data services may also have their own table attributes and metrics unique to their own service.

Azure Healthcare APIs will use the new UX shown below, common across all Azure services, for allowing customers to select and display metrics at the Workspace or Data Service level.

* Image of Metrics UX

Guidelines for Azure Monitor Metrics Onboarding: https://1dsdocs.azurewebsites.net/articles/shoebox/shoebox-metrics.html

Exerpts from the guidelines documented in the link above:
All Healthcare API services must follow the Shoebox metrics guidelines outlined below.

-   Services must emit at least one metric that falls into each of the categories, including Latency, Traffic, Errors, Saturation, Availability.

-   Metrics in a category should have the listed set of minimum dimensions, including Operation, Authentication, Protocol, StatusCode, StatusCodeClass, StatusText and support the listed aggregation types.

-   Azure services must request explicit exceptions if for some reason they believe a particular category and dimension are not applicable. Send mail to
    [**shoeboxcore@microsoft.com**](mailto:shoeboxcore@microsoft.com) to request exceptions.

One customer requirement on resource logd or metrics is that we provide invididual numbers rather than the averages or sums. For example, customers want to see how long each request takes in milliseconds, and how much the transaction costs.  While it is possible to combine the new fields for individual values with audit logs and metrics, it may be difficult for customer to parse the info. So it may work better that we provide a new log category to capture individual values and make the option configurable.

Below are audit logs and metrics we support today and can be used as reference.

Metrics are measures that customers use to configure their shoebox view. Find those metrics [here](https://microsoft.sharepoint.com/:x:/t/msh/EerhQ4x7vr5Am-lM02kwVs4BacEnZMew93lLAsgtUO5_kg?e=5gfAQg)

Audit logging is required by Azure Security and certain attributes are required in this logging. Find those attributes [here](https://microsoft.sharepoint.com/:x:/t/msh/EerhQ4x7vr5Am-lM02kwVs4BacEnZMew93lLAsgtUO5_kg?e=frgDv7)

Diagnostic logging is mostly to the product teams' discretion. However, when certain features are implemented Diagnostic logs may be a requirement by Azure. For example: In the case of CMK, diagnostic logs are required for all 403 errors caused by configuration issues for CMK. This is an Azure Security requirement. Find those attributes [here](https://microsoft.sharepoint.com/:x:/t/msh/EerhQ4x7vr5Am-lM02kwVs4BacEnZMew93lLAsgtUO5_kg?e=frgDv7)

Azure Healthcare APIs has a workspace concept with child services similar to that of Synapse and the metrics and logging experience will take guidance from the user experience created by Synapse.  

## Supporting Customer Insights
#### Cigna

Cigna, one of the largest customers, requested that we help address the
following logging related issues while they worked on a service
throttling issue.

-   Remove the durationMS field from the audit logs or populate real
    data. An internal review showed that the metric which was not
    implemented and therefore always returned "0”. The zero-value issue
    "messes with aggregate statistics and complicates our log roll-up."

-   Add “request charge” to the logs, matching "x-ms-request-charge"
    header so that service side behavior can be better monitored without
    controlling all of the consuming clients.

## Related Work 
The Azure API for FHIR service supports the shoebox feature, allowing customers to configure diagnostic settings and export audit logs with
20+ fields, and metrics with 12+ fields to Storage, Log Analytics Workspace or Event Hubs. More details can be found from the documentation, “[Enable Diagnostic Logging in Azure API for FHIR](https://docs.microsoft.com/en-us/azure/healthcare-apis/enable-diagnostic-logging)”.

## What is Being Proposed? 
Define the naming conventions, common attributes, and common metrics for all Healthcare API services.

## Elevator Pitch / Press Release 
## Justification, Expected Business Impact, and Value Proposition 
This feature enables customers to query and analyze, view predefined metrics, and export logs to meet their business needs. The customer feedback
received so far has clearly demonstrated that it is an important feature to customers and that we must support it for all Healthcare API services.

We can expand on the work already done for Azure API for FHIR to guide the logs and metrics for all new services in Azure Healthcare APIs. It is important to take a step back and look at what has worked well in Gen 1 and what can be improved.

Logging and creating shoebox metrics are a PLR requirement for any Azure service going public preview. 

## Target User / Persona 
The feature is used by all users, including but are not limited to, IT
administrators, business analysts, and developers.

## Existing Solutions and Compete Info 
### GCP

Google Cloud Healthcare API provides [audit
logs](https://cloud.google.com/healthcare/docs/how-tos/audit-logging)
created by Cloud Healthcare API as part of Cloud Audit Logs.

-   Cloud Healthcare API writes Admin Activity audit logs, which record
    operations that modify the configuration or metadata of a resource.
    You can't disable Admin Activity audit logs.

-   Only if explicitly enabled, Cloud Healthcare API writes Data Access
    audit logs. Data Access audit logs contain API calls that read the
    configuration or metadata of resources, as well as user-driven API
    calls that create, modify, or read user-provided resource data.

-   Cloud Healthcare API doesn't write System Event audit logs.

-   Cloud Healthcare API doesn't write Policy Denied audit logs.

Audit log entries can be viewed in Cloud Logging using the Logs Viewer,
the Cloud Logging API, or the gcloud command-line tool. You can export
audit logs in the same way that you export other kinds of logs.

Cloud Logging does not charge you for audit logs that cannot be
disabled, including all Admin Activity audit logs. Cloud Logging charges
you for Data Access audit logs that you explicitly request.

In addition, Google Cloud Healthcare API allows you to receive
notifications using Pub/Sub when any of the following clinical events
occur:

-   A DICOM instance is stored in a DICOM store

-   A FHIR resource is created, updated, or deleted in a FHIR store.
    However, notifications are not sent when a FHIR resource is imported
    from Cloud Storage.

-   An HL7v2 message is ingested or created in an HL7v2 store

### AWS

Amazon HealthLake is integrated with [AWS
CloudTrail](https://docs.aws.amazon.com/healthlake/latest/devguide/logging-using-cloudtrail.html),
a service that provides a record of actions taken by a user, role, or an
AWS service in HealthLake.

CloudTrail captures all API calls for HealthLake as events.

-   If you create a trail, you can enable continuous delivery of
    CloudTrail events to an Amazon S3 bucket, including events for
    HealthLake.

-   If you don't configure a trail, you can still view the most recent
    events in the CloudTrail console in **Event history**.

Using the information collected by CloudTrail, you can determine the
request that was made to HealthLake, the IP address from which the
request was made, who made the request, when it was made, and additional
details.

When activity occurs in HealthLake, that activity is recorded in a
CloudTrail event along with other AWS service events in Event history.
You can view, search, and download recent events in your AWS account. 

CloudTrail log files aren't an ordered stack trace of the public API
calls, so they don't appear in any specific order.

## Customers/Partners Interaction Log
N/A

# APPROVAL GATE - WHY
Complete a review and get Director approval to continue.

# User-Facing Feature Design 
Section status: Draft

Date reviewed: 05/18/2021

## Terminology (PM/Dev) 

| Term                          | Definition                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| ----------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| The Shoebox Project           | The shoebox project was launched in 2014 to provide a common metric and logging mechanism for the Azure platform to solve the problems where each team had built their own custom logging pipeline to meet customer requirements. The term shoebox, coined by Azure CTO Mark Russinovich and used internally. The Shoebox project is now part of the “Azure Monitor” product family. See attached doc for more info on Shoebox roadmap.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| Shoebox Onboarding Process    | With the Shoebox project, Azure service teams are only responsible for emitting the telemetry into the Shoebox pipeline and no longer having to solve the last mile problem to connect the data to end customers. The Shoebox onboarding process involves several steps. Once onboarded and deployed, the service can then send telemetry data to the Azure Monitor pipeline. Doing so enables customers to experience fast, simple and standardized access to monitoring data from the RP. Check the example of Log Analytics onboarding checklist.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| Resource Logs or Shoebox Logs | Also known as shoebox logs and diagnostic logs, these are the data-plane operations from your resource. These logs depend on the Geneva Logs (aka Warm Path / MDS) and make use of the OnBehalfOf (OBO) service to give customers the option to route log and/or metric data to a customer storage account, Event Hub, or Log Analytics workspace. Metrics depend on the Geneva-MDM pipeline and become available to the customer in the Azure Portal filled via a consistent metric REST API. The new manifest file for metrics is based per resource type instead of per resource provider. Once the service team completes metrics onboarding, the resource type’s metrics will be available behind the new public metrics REST API. This is a public API behind ARM. External customers have access to the metrics API without any opt-in. Customers can also opt-in to export the metric data to customer storage account, EventHub, or Log Analytics workspace. Customers can create alerts and notifications on these or stream/archive them to storage accounts, event hubs, Log Analytics or to 3rd party services. |
| Activity Logs                 | Provides insight into the operations on each Azure resource in the subscription from the outside (the management plane) in addition to updates on Service Health events. Use the Activity Log, to determine the what, who, and when for any write operations (PUT, POST, DELETE) taken on the resources in your subscription. There is a single Activity log for each Azure subscription.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| Azure Active Directory Logs   | Contains the history of sign-in activity and audit trail of changes made in the Azure Active Directory for a particular tenant.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| Geneva                        | Geneva is a 1st party monitoring platform which enables services to do Monitoring, Diagnostics, and Analytics to support the requirement of a service built on different environments. Geneva maximizes the availability and performance of applications and services with a comprehensive solution for collecting, analyzing, and acting on telemetry across your cloud and on-premises environments. Large parts of the Geneva infrastructure (e.g. Agents, Metrics, Health System, Pipeline) are utilized to power our external monitoring offering - Azure Monitor. Currently, the agents, configuration services, and pipelines used in Geneva and Azure Monitor are separate but managed by the same teams. The PIE Observability team within Microsoft is working to create a converged data collection platform that merges the existing data collection platforms and modernizes to leverage forward-looking platforms like ARM and Azure Policy where appropriate.                                                                                                                                                 |
| Geneva Actions                | Geneva Actions is a secure, auditable, and compliant gateway to your production APIs. You can publish an extension (using C# code or swagger) on Geneva Actions to access your production endpoints through a unified web portal, PowerShell or REST API. It's designed for access by on-call engineers, customer support (CSS) teams, or other audiences that you want to provide access to your management operations. Geneva Actions is only a gateway for operations that you author in C# or swagger and chose to expose. It does not have any built-in operations to restart VMs, connect to your cluster, database or whatever else you want to do. All Geneva Actions extensions and operations are available to execute via the Jarvis portal or PowerShell.

## Goals (PM/Dev)
- Align across teams on if audit logging is going to be a single table or multiple tables.
- Align across teams on naming conventions for table attributes and metrics
- Define and emit audit logs and metrics for FHIR, DICOM, and IoT
- Define metrics at various scopes (Workspace, FHIR, DICOM, and IoT)
- Enable Diagnostic Setting for the workspace and each data service
- Enable exporting data when Private Link is enabled for the FHIR service

## Non-Goals (PM/Dev) 
| Non-Goal              | Mitigation                                                                                                                                                                                   |
|-----------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Metrics for Cosmos DB | While keeping existing metrics (and improving them based on customer demands) related to Cosmos DB for Gen 1 prior to Jupiter release, we will not include metrics for Cosmos DB in Jupiter. |
|                       |                                                                                                                                                                                              |
|                       |                                                                                                                                                                                              |

## Scenarios and Use Cases (PM/Dev) 
| Scenario / Use Case                                                                                                                     | Steps to fulfill the scenario                                                                                                         | Priority |
|-----------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------|----------|
| The user views logging data through the portal.                                                                                         | Go to the portal, select desired scope, metric(s), and aggregation to view the logging data in the browser.                                                                            | P0       |
| The user configures the Diagnostic Settings to download audit logs and metrics to a storage account, Event Hubs or Analytics Workspace. | Open the Diagnostic Settings blade, select audit logs and/or metrics, and specify export location. The logs that you could export could be different at the workspace level and at the dataset level.                                   | P0       |
| The user receives alerts or notifications.                                                                                              | Open the Azure Monitor blade, configures alerts and notifications based on rules, for example, database size in % or fixed number.    | P0       |
| The user exports data after DR failover.                                                                                                | Export data from the portal normally. Reconfig Private Link if necessary.                                                             | P0       |
|



## Feature Dependencies (PM/Dev) 
Logging enabled by FHIR, DICOM, and IoT Connector

## Customer Validation Plan (PM) 

*Guidance: This section gives details on how we plan on engaging with
customers to validate our assumptions and design.*

### Customer Research Required 

### Criteria for Customer Selection 

### Customers Selected 

## User Interface (PM) 

The Diagnostic Settings blade will be enabled at the child service level and not at the workspace level to allow the customer to have more granualrity for what they would like to monitor.
### Storyboard 

*Guidance: This section is for features with a UI/UX component.
Alternatively, you can also create Storyboard in PowerPoint and provide
link to the PPT in this section.*

### Usability Validation 

*Guidance: This section defines the usability labs required to validate
the user interface design.*

## End User Troubleshooting (PM) 

*Guidance: This section describes what we provide to the customer in
order to enable them to troubleshoot issues with the feature. Customer
Metrics and logging to be provided by Azure Insights unless otherwise
noted.*

### Azure Monitor Metrics 

| Metric Name | Display Name | Description | Dimension | Metric Unit | Aggregation Type | Proposed Alert Rule | Time to Detect |
|-------------|--------------|-------------|-----------|-------------|------------------|---------------------|----------------|
|             |              |             |           |             |                  |                     |                |
|             |              |             |           |             |                  |                     |                |

### Logging 

| Log Category | Category Display Name | Log Event | Log Event Display Name | Proposed Alert Rule |
|--------------|-----------------------|-----------|------------------------|---------------------|
|              |                       |           |                        |                     |
|              |                       |           |                        |                     |

### Naming Conventions

#### Logging

The recommendation is that we use separate tables for all resource types even for logs types that are similar in nature (e.g. Audit Logs). This was deemed necessary as some schemas no matter how tightly controlled will ultimately diverge between resource types. The same log category names can be used and defined for each resource type as long as the "dataTypeOverride=" is defined in the MDS config to make sure that the log category is prefixed with the name of the resource type (i.e. dataTypeOverride=”Microsoft.HealthcareApis_DicomAuditLogs”). This is due to a limititaion on how Log Analytics determines the mapping of the log to the correct transform to use for exporting to a Log Analytics table. By default the RP namespace_LogCategory is used and would cause a conflict if we did not make this change in the MDS config for the monitoring agent.

Any changes to table names or column names will require a 3 year deprication notfication cycle and we will reference the new column name that will replace the deprecated one. So we must be absolutely sure for naming before venturing forward.

#### Metrics Naming
We will not prefix metric names. Instead we will separate the metrics by namespaces named after their respective resourceType from the ARM manifest, within the shoebox specific regional MDM accounts. The namespaces are only internal for MDM. The MDM account would have the prefix of “MicrosoftHealthcareApis2Shoebox” for Jupiter shoebox so they can be separate from Gen1. 

MDM Account Name format: MicrosoftHealthcareApis2Shoebox{regionName}
Namespace names
•	FhirServices
•	DicomServices
•	IotConnectors
•	IotConnectorsDestinations

#### Metrics Dimensions
All metrics will need to contain ResourceName and ResourceType dimensions that he customer can filter on. This will allow them to do queries in Log Analytics where they want to target a specific resource.

### Troubleshooting guidance 

*Guidance: This section describes the steps customers should take to
troubleshoot common errors. This will be used to populate the
documentation*

### Troubleshooting in the Azure Portal 

*Guidance: This section describes the troubleshooting guidance that is
populated in the Azure portal.*

| Problem Type (if new) | Problem Category | Troubleshooting Guidance |
|-----------------------|------------------|--------------------------|
|                       |                  |                          |
|                       |                  |                          |

## Proposed release plan (PM/Dev) 

*Guidance: This section is particularly important if running a private
preview as part of the release. Use it to align collateral releases.*

### Private Preview 

#### Target date

#### Goals for Release 

\[e.g. to validate assumptions\]

#### Features for Release 

\[List of features\]

#### Collateral Required 

\[List of collateral, e.g. docs, marketing, etc.\]

#### Success criteria 

\[List\]

#### Customers Involved 

\[List\]

### Public Preview 

#### Goals for Release 

\[e.g. to validate assumptions\]

#### Features for Release 

\[List of features\]

#### Collateral Required 

\[List of collateral, e.g. docs, marketing, etc.\]

#### Success criteria 

\[List\]

#### Customers Involved 

\[List\]

### General Availability 

#### Target date

Initial rollout: 06/30/2021

GA: 

#### Goals for Release 

\[e.g. to validate assumptions\]

#### Features for Release 

\[List of features\]

#### Collateral Required 

\[List of collateral, e.g. docs, marketing, etc.\]

#### Success criteria 

\[List\]

#### Customers Involved 

\[List\]

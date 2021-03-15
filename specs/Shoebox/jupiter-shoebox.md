# Shoebox for Healthcare APIs

## Enable customers to configure and export audit logs and metrics

# Scenario Contacts 

**Program Manager (PM):** Benjamin Xue

**Software Engineer (Dev):** Scott Taladay, Dustin Burson

# How to Use This Document

*This is a living document tracking the specification for your feature.
It follows the lifecycle of the feature and should be written and
reviewed in four stages.*

*1. Justifying the work, in which the feature is greenlit for
engineering resources. **This portion must be completed and achieve
Director approval before committing engineering resources.***

*2. User-facing feature design, which goes into detail about how a
customer interacts with the feature.*

*3. Implementation design, which describes the work the team is doing.*

*4. Release activities, including documentation, demos, and field
readiness.*

*Not all sections may be relevant to your feature, and that’s okay.
Leave unused sections empty – do not delete them!*

**Note:** Not everything described in this document is part of the
solution scope. Some information is provided to help understand the
overall problem domain and should be kept in mind while designing a
solution. Scenarios are yet to be prioritized.

# Why There is a Gap Today? (PM) 

*Guidance: This section is used to build consensus around the need for
work to be done in a specific feature area and is equivalent to a “one
pager”. This section is likely to be 2-3 pages when completed. When
pitching the idea via a PowerPoint presentation, make sure all these
items are included in your presentation.*

*This section is **required** for assignment of engineering resources.*

Section status: \[draft\]

Date reviewed: \[Date\]

## Problem Statement 

*Guidance: State the problem or challenge in a way that ties back to the
target user. What is their goal? Why does this matter to them? Can be of
the form, “Customers have a hard time doing FOO, I know this because I
heard it from X, Y, Z.”*

As we work toward Jupiter release, not only must we continue to support
the required Shoebox feature for Azure API for FHIR in Azure, but
also expand the support for new services such as DICOM and IoT.

While the underlying technologies vary for the Healthcare API services,
it is important that we define a set of common audit logs and resource logs or metrics
for all services, along with unique logs and metrics for each service,
and develop a framework that enables easy plugging in for future
services.

All Healthcare API services must follow the Shoebox metrics guidelines
outlined below.

-   Services must emit at least one metric that falls into each of the
    categories, including Latency, Traffic, Errors, Saturation,
    Availability.

-   Metrics in a category should have the listed set of minimum
    dimensions, including Operation, Authentication, Protocol,
    StatusCode, StatusCodeClass, StatusText. and supported the listed
    aggregation types.

-   Azure services must request explicit exceptions if for some reason
    they believe a particular category and dimension are not applicable.
    Send mail to
    [**shoeboxcore@microsoft.com**](mailto:shoeboxcore@microsoft.com) to
    request exceptions.

Also, we should plan for the Shoebox onboarding process, which took quite some time for Gen 1, and start it as soon as we can while providing required information, including documents and packages.

One customer requirement on resource logd or metrics is that we provide invididual numbers rather than the averages or sums. 
For example, customers want to see how long each request takes in milliseconds, and how much the transaction costs.  While it is possible
to combine the new fields for individual values with audit logs and metrics, it may be difficult for customer to parse the info. 
So it may work better that we provide a new log category to capture individual values and make the option configurable. 

Below are audit logs and metrics we support today and can be used as reference.

**The Azure API for FHIR service includes the following fields in the audit log.**

| Field Name             | Type     | Notes                                                                          |
|------------------------|----------|--------------------------------------------------------------------------------|
| CallerIdentity         | Dynamic  | A generic property bag containing identity information                         |
| CallerIdentityIssuer   | String   | Issuer                                                                         |
| CallerIdentityObjectId | String   | Object_Id                                                                      |
| CallerIPAddress        | String   | The caller’s IP address                                                        |
| CorrelationId          | String   | Correlation ID                                                                 |
| FhirResourceType       | String   | The resource type for which the operation was executed                         |
| LogCategory            | String   | The log category (we are currently returning ‘AuditLogs’ LogCategory)          |
| Location               | String   | The location of the server that processed the request (e.g., South Central US) |
| OperationDuration      | Int      | The time it took to complete this request in seconds                           |
| OperationName          | String   | Describes the type of operation (e.g. update, search-type)                     |
| RequestUri             | String   | The request URI                                                                |
| ResultType             | String   | The available values currently are Started, Succeeded, or Failed               |
| StatusCode             | Int      | The HTTP status code. (e.g., 200)                                              |
| TimeGenerated          | DateTime | Date and time of the event                                                     |
| Properties             | String   | Describes the properties of the fhirResourceType                               |
| SourceSystem           | String   | Source System (always Azure in this case)                                      |
| TenantId               | String   | Tenant ID                                                                      |
| Type                   | String   | Type of log (always MicrosoftHealthcareApisAuditLog in this case)              |
| ResourceId             | String   | Details about the resource                                                     |

The metrics for Azure API for FHIR and IoT include the following fields.

| Metric                                     | Exportable via Diagnostic Settings? | Metric Display Name             | Unit         | Aggregation Type | Description                                                                                                                                           | Dimensions                                                                                     |
|--------------------------------------------|-------------------------------------|---------------------------------|--------------|------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------|
| Availability                               | Yes                                 | Availability                    | Percent      | Average          | The availability rate of the service.                                                                                                                 | No Dimensions                                                                                  |
| CosmosDbCollectionSize                     | Yes                                 | Cosmos DB Collection Size       | Bytes        | Total            | The size of the backing Cosmos DB collection, in bytes.                                                                                               | No Dimensions                                                                                  |
| CosmosDbIndexSize                          | Yes                                 | Cosmos DB Index Size            | Bytes        | Total            | The size of the backing Cosmos DB collection's index, in bytes.                                                                                       | No Dimensions                                                                                  |
| CosmosDbRequestCharge                      | Yes                                 | Cosmos DB RU usage              | Count        | Total            | The RU usage of requests to the service's backing Cosmos DB.                                                                                          | Operation, ResourceType                                                                        |
| CosmosDbRequests                           | Yes                                 | Service Cosmos DB requests      | Count        | Sum              | The total number of requests made to a service's backing Cosmos DB.                                                                                   | Operation, ResourceType                                                                        |
| CosmosDbThrottleRate                       | Yes                                 | Service Cosmos DB throttle rate | Count        | Sum              | The total number of 429 responses from a service's backing Cosmos DB.                                                                                 | Operation, ResourceType                                                                        |
| IoTConnectorDeviceEvent                    | Yes                                 | Number of Incoming Messages     | Count        | Sum              | The total number of messages received by the Azure IoT Connector for FHIR prior to any normalization.                                                 | Operation, ConnectorName                                                                       |
| IoTConnectorDeviceEventProcessingLatencyMs | Yes                                 | Average Normalize Stage Latency | Milliseconds | Average          | The average time between an event's ingestion time and the time the event is processed for normalization.                                             | Operation, ConnectorName                                                                       |
| IoTConnectorMeasurement                    | Yes                                 | Number of Measurements          | Count        | Sum              | The number of normalized value readings received by the FHIR conversion stage of the Azure IoT Connector for FHIR.                                    | Operation, ConnectorName                                                                       |
| IoTConnectorMeasurementGroup               | Yes                                 | Number of Message Groups        | Count        | Sum              | The total number of unique groupings of measurements across type, device, patient, and configured time period generated by the FHIR conversion stage. | Operation, ConnectorName                                                                       |
| IoTConnectorMeasurementIngestionLatencyMs  | Yes                                 | Average Group Stage Latency     | Milliseconds | Average          | The time period between when the IoT Connector received the device data and when the data is processed by the FHIR conversion stage.                  | Operation, ConnectorName                                                                       |
| IoTConnectorNormalizedEvent                | Yes                                 | Number of Normalized Messages   | Count        | Sum              | The total number of mapped normalized values outputted from the normalization stage of the the Azure IoT Connector for FHIR.                          | Operation, ConnectorName                                                                       |
| IoTConnectorTotalErrors                    | Yes                                 | Total Error Count               | Count        | Sum              | The total number of errors logged by the Azure IoT Connector for FHIR                                                                                 | Name, Operation, ErrorType, ErrorSeverity, ConnectorName                                       |
| ServiceApiErrors                           | Yes                                 | Service Errors                  | Count        | Sum              | The total number of internal server errors generated by the service.                                                                                  | Protocol, Authentication, Operation, ResourceType, StatusCode, StatusCodeClass, StatusCodeText |
| ServiceApiLatency                          | Yes                                 | Service Latency                 | Milliseconds | Average          | The response latency of the service.                                                                                                                  | Protocol, Authentication, Operation, ResourceType, StatusCode, StatusCodeClass, StatusCodeText |
| ServiceApiRequests                         | Yes                                 | Service Requests                | Count        | Sum              | The total number of requests received by the service.                                                                                                 | Protocol, Authentication, Operation, ResourceType, StatusCode, StatusCodeClass, StatusCodeText |
| TotalErrors                                | Yes                                 | Total Errors                    | Count        | Sum              | The total number of internal server errors encountered by the service.                                                                                | Protocol, StatusCode, StatusCodeClass, StatusCodeText                                          |
| TotalLatency                               | Yes                                 | Total Latency                   | Milliseconds | Average          | The response latency of the service.                                                                                                                  | Protocol                                                                                       |
| TotalRequests                              | Yes                                 | Total Requests                  | Count        |                  |                                                                                                                                                       |                                                                                                |

## Supporting Customer Insights

*Guidance: This section should include direct quotes from customers,
direct quotes from the field, and summaries of interactions with
customers in which they describe the problem they are having.*

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

*Guidance: What other features are related to this work? Please include
links.*

The Azure API for FHIR service supports the shoebox feature, allowing
customers to configure diagnostic settings and export audit logs with
20+ fields, and metrics with 12+ fields to Storage, Log Analytics
Workspace or Event Hubs. More details can be found from the
documentation, “[Enable Diagnostic Logging in Azure API for
FHIR](https://docs.microsoft.com/en-us/azure/healthcare-apis/enable-diagnostic-logging)”.

## What is Being Proposed? 

*Guidance: In 20 words or less, describe the proposed solution.*

Define shoebox metrics and enable exporting metrics for all Healthcare
API services.

## Elevator Pitch / Press Release 

*Guidance: Create a story for your scenario – detail out the customer,
their problem and/or goal, and then specific outcomes the customer will
achieve or how success would be measured. Avoid implementation details.
Think of this as the blog post announcing this feature. 500 words max.*

## Justification, Expected Business Impact, and Value Proposition 

*Guidance: Why are we tackling this scenario? What is the expected
impact? What’s the value proposition of this work?*

The shoebox feature enables customers to export and analyze audit logs
and metrics to meet their business needs. The customer feedback we
received so far has clearly demonstrated that it is such an important
feature to them that we must support for all Healthcare API services.

We can leverage the engineering work for Gen 1 and expand the support
for all new services. It is important that before focusing on
engineering work, we take a step back looking at what has worked well
and what can be improved.

## Target User / Persona 

*Guidance: Specify the target user/persona(s).*

The feature is used by all users, including but are not limited to, IT
administrators, business analysts and developers.

## Existing Solutions and Compete Info 

*Guidance: List the various ways in which a user may currently handle
this problem/challenge. With what expectations will customers approach
our solution? What are our competitors doing in this space?*

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

*Guidance: What customer have voiced and validated the specific problem
statements? Did you discuss the elevator pitch and the potential
solutions (under NDA)? Are they candidates for continued follow-up and
participation in our early access program? This should be a list of the
different customers you have talked to. Repeated interactions with the
same customer, such as via private preview customers, should be tracked
elsewhere.*

| Customer/Partner Name | Conversation Details / Specific Requirements | Last Contact | Private Preview Candidate |
|-----------------------|----------------------------------------------|--------------|---------------------------|
|                       |                                              |              |                           |
|                       |                                              |              |                           |

# APPROVAL GATE - WHY

Complete a review and get Director approval to continue.

# User-Facing Feature Design 

*Guidance: This section describes all aspects of the feature with a
user-facing component, including customer use cases, metrics, and
scenario KPIs. This section is more than just UI!*

*This section is **required** for all user-facing features. Features
with no user impact, for example improvements to the service
implementation, may treat this section as **optional**. You probably
can’t skip this section.*

Section status: \[draft, review, accepted\]

Date reviewed: \[Date\]

## Terminology (PM/Dev) 

*Guidance: This section defines terms used in the rest of the spec. The
terms may feed into public docs and blogs as be used to define metric
names and logging categories.*

<table>
<thead>
<tr class="header">
<th>Term</th>
<th>Definition</th>
</tr>
</thead>
<tbody>
<tr class="odd">
<td>The Shoebox Project</td>
<td>The shoebox project was launched in 2014 to provide a common metric and logging mechanism for the Azure platform to solve the problems where each team had built their own custom logging pipeline to meet customer requirements. The term shoebox, coined by Azure CTO Mark Russinovich and used internally. The Shoebox project is now part of the “Azure Monitor” product family. See attached doc for more info on Shoebox roadmap. </td>
</tr>
<tr class="even">
<td>Shoebox Onboarding Process</td>
<td><p>With the Shoebox project, Azure service teams are only responsible for emitting the telemetry into the Shoebox pipeline and no longer having to solve the last mile problem to connect the data to end customers.</p>
<p>The Shoebox onboarding process is a lengthy internal process and involves several steps. Once is onboarded and deployed, the service can then send telemetry data to the Azure Monitor pipeline. Doing so enables customers to experience fast, simple and standardized access to monitoring data from the RP. Check the example of Log Analytics onboarding <a href="https://1dsdocs.azurewebsites.net/articles/loganalytics-onboarding-guide/onboarding-checklist.html">checklist</a>.</p></td>
</tr>
<tr class="odd">
<td>Resource Logs or Shoebox Logs</td>
<td><p>Also known as <a href="http://aka.ms/shoeboxlogs">shoebox logs</a> and diagnostic logs, these are the data-plane operations from your resource. These logs depend on the Geneva Logs (aka Warm Path / MDS) and make use of the OnBehalfOf (OBO) service to give customers the option to route log and/or metric data to a customer storage account, Event Hub, or Log Analytics workspace.</p>
<p><a href="https://1dsdocs.azurewebsites.net/articles/shoebox/shoebox-metrics.html">Metrics</a> depend on the Geneva-MDM pipeline and become available to the customer in the Azure Portal filled via a consistent metric REST API.</p>
<p>The new manifest file for metrics is based per resource type instead of per resource provider. Once the service team completes metrics onboarding, the resource type’s metrics will be available behind the new public metrics REST API. This is a public API behind ARM. External customers have access to the metrics API without any opt-in.</p>
<p>Customers can also opt-in to export the metric data to customer storage account, EventHub, or Log Analytics workspace. Customers can create alerts and notifications on these or stream/archive them to storage accounts, event hubs, Log Analytics or to 3rd party services.</p></td>
</tr>
<tr class="even">
<td>Activity Logs</td>
<td>Provides insight into the operations on each Azure resource in the subscription from the outside (the management plane) in addition to updates on Service Health events. Use the Activity Log, to determine the what, who, and when for any write operations (PUT, POST, DELETE) taken on the resources in your subscription. There is a single Activity log for each Azure subscription.</td>
</tr>
<tr class="odd">
<td>Azure Active Directory Logs</td>
<td>Contains the history of sign-in activity and audit trail of changes made in the Azure Active Directory for a particular tenant.</td>
</tr>
<tr class="even">
<td>Geneva</td>
<td><p>Geneva is a 1st party monitoring platform which enables services to do Monitoring, Diagnostics, and Analytics to support the requirement of a service built on different environments.</p>
<p>Geneva maximizes the availability and performance of applications and services with a comprehensive solution for collecting, analyzing, and acting on telemetry across your cloud and on-premises environments. Large parts of the Geneva infrastructure (e.g. Agents, Metrics, Health System, Pipeline) are utilized to power our external monitoring offering - Azure Monitor.</p>
<p>Currently, the agents, configuration services, and pipelines used in Geneva and Azure Monitor are separate but managed by the same teams. The PIE Observability team within Microsoft is working to create a converged data collection platform that merges the existing data collection platforms and modernizes to leverage forward-looking platforms like ARM and Azure Policy where appropriate.</p>
<p>More info on Geneva <a href="https://genevamondocs.azurewebsites.net/getting_started/New%20Getting%20Started/overview.html">here</a>.</p></td>
</tr>
<tr class="odd">
<td>Geneva Actions</td>
<td><p>Geneva Actions is a secure, auditable, and compliant gateway to your production APIs. You can publish an extension (using C# code or swagger) on Geneva Actions to access your production endpoints through a unified web portal, PowerShell or REST API.</p>
<p>It's designed for access by on-call engineers, customer support (CSS) teams, or other audiences that you want to provide access to your management operations.</p>
<p>Geneva Actions is only a gateway for operations that you author in C# or swagger and chose to expose. It does not have any built-in operations to restart VMs, connect to your cluster, database or whatever else you want to do. All Geneva Actions extensions and operations are available to execute via the Jarvis portal or PowerShell.</p></td>
</tr>
</tbody>
</table>

## Branding (PM) 

*Guidance: This section discusses branding decisions such as
product/feature names. Note that all branding decisions **require**
sign-off by the Product Marketing Manager.*

## Detailed Feature Description (PM/Dev) 

*Guidance: This section describes, at a high level, what the feature is
and is not to the target customer and how we measure success.*

## Goals (PM/Dev) 

*Guidance: This section describes the goals for how the feature is to be
used.*

| Goal                                                                    | Target Release | Priority |
|-------------------------------------------------------------------------|----------------|----------|
| Provide workspace audit logs                                            | 4/30/2021      | P0       |
| Start Shoebox onboarding Process                                        | 5/3/2021       | P0       |
| Provide audit logs and metrics for FHIR                                 | 5/31/2021      | P0       |
| Provide audit logs and metrics for DICOM, including DICOM Cast          | 5/31/2021      | P0       |
| Provide audit logs and metrics for IoT                                  | 5/31/2021      | P0       |
| Enable Diagnostic Setting on the Azure Portal                           | 6/30/2021      | P0       |
| Enable exporting data when Private Link is enabled for the FHIR service | 6/30/2021      | P0       |
|                                                                         |                |          |
|                                                                         |                |          |
|                                                                         |                |          |
|                                                                         |                |          |
|                                                                         |                |          |
|                                                                         |                |          |

## Non-Goals (PM/Dev) 

*Guidance: This section describes the topical customer goals that this
feature is specifically not addressing, and why.*

| Non-Goal              | Mitigation                                                                                                                                                                                   |
|-----------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Metrics for Cosmos DB | While keeping existing metrics (and improving them based on customer demands) related to Cosmos DB for Gen 1 prior to Jupiter release, we will not include metrics for Cosmos DB in Jupiter. |
|                       |                                                                                                                                                                                              |
|                       |                                                                                                                                                                                              |

## Scenarios and Use Cases (PM/Dev) 

*Guidance: This section describes the customer scenarios that this
feature is designed to address. Include how the feature is used to solve
the scenario/use case. Following these steps should be used to validate
the feature.*

| Scenario / Use Case                                                                                                                     | Steps to fulfill the scenario                                                                                                         | Priority |
|-----------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------|----------|
| The user browses logging data through the portal.                                                                                       | Enable the portal experience by integrating all Healthcare API services with Shoebox.                                                 | P0       |
| The user configures the Diagnostic Settings to download audit logs and metrics to a storage account, Event Hubs or Analytics Workspace. | Enable the portal experience by integrating all Healthcare API services with Shoebox.                                                 | P0       |
| The user configures alerts and notifications based on rules, for example, database size exceeding a % or fixed number.                  | Provide user interface to allow configuration for alerts and notifications. Integration all Healthcare API services with Azure Alert. | P1       |
| The user tries to export data after Private Link has been configured and enabled for the FHIR service                                   | Export data from the portal normally.                                                                                                 | P1       |
|                                                                                                                                         |                                                                                                                                       |          |
|                                                                                                                                         |                                                                                                                                       |          |
|                                                                                                                                         |                                                                                                                                       |          |
|                                                                                                                                         |                                                                                                                                       |          |
|                                                                                                                                         |                                                                                                                                       |          |
|                                                                                                                                         |                                                                                                                                       |          |

## Scenario KPIs (PM) 

*Guidance: These are the measures presented to the feature team, e.g.
number of FHIR endpoints, total data storage size.*

<table>
<thead>
<tr class="header">
<th>Type<br />
[Biz | Cust | Tech]</th>
<th>Outcome</th>
<th>Measure</th>
<th>Target</th>
<th>Priority</th>
</tr>
</thead>
<tbody>
<tr class="odd">
<td></td>
<td></td>
<td></td>
<td></td>
<td></td>
</tr>
<tr class="even">
<td></td>
<td></td>
<td></td>
<td></td>
<td></td>
</tr>
</tbody>
</table>

## What’s in the Box? (PM) 

*Guidance: This section lists everything the customer gets in the end.
Is there a new service? Templates? Samples? SDK?*

## Feature Dependencies (PM/Dev) 

*Guidance: This section describes both the dependencies this feature has
on other areas as well as other areas impacted by this work. Examples of
areas which may be impacted: Persistence Provider, FHIR API.*

### Dependencies this design has on other features 

| Feature Name | Nature of dependency | Mitigation/Fallback | PM  | Dev |
|--------------|----------------------|---------------------|-----|-----|
|              |                      |                     |     |     |
|              |                      |                     |     |     |
|              |                      |                     |     |     |

### Features that have a dependency on this design 

| Team Name | Contacts | PM  | Dev |
|-----------|----------|-----|-----|
|           |          |     |     |
|           |          |     |     |

## Customer Validation Plan (PM) 

*Guidance: This section gives details on how we plan on engaging with
customers to validate our assumptions and design.*

### Customer Research Required 

### Criteria for Customer Selection 

### Customers Selected 

## User Interface (PM) 

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

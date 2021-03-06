# SQL Persistence Provider

## Provision SQL Databases and Balance Database Workloads

# Scenario Contacts 

**Program Manager (PM):** Benjamin Xue

**Software Engineer (Dev):** Jack Liu, Shawn Martelock, Deepak Bansal

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

As we continue to expand the healthcare API platform and add new
services such as DICOM and IoT in the Jupiter release, we’ve made a
technical decision to move away from using Cosmos DB to using Azure SQL
database as the data store. This transition will enable us to support
critical features such as transactions and additional search
capabilities for FHIR data that were challenging to implement in Cosmos
DB.

As we make this transition, it is important that we consider customer
requirements upfront while provisioning the database layer and enable
subsequent changes when necessary. One piece of this is to ensure that
customers can load as much data as they need, both now and in the future
as their data size grows. We also need to ensure that we can meet all
required Azure security benchmarks such as customer-managed keys (CMK).

This spec applies to the FHIR service and the DICOM service, including
DICOM Cast, because both services require SQL database as data store. It
does not apply to IoT because it does not use SQL database.

## Supporting Customer Insights 

*Guidance: This section should include direct quotes from customers,
direct quotes from the field, and summaries of interactions with
customers in which they describe the problem they are having.*

#### Cigna

Cigna has 50TB, which includes 7 years of data, mostly prescription
claims and provider directory data, small resource size but large
volume, 3.5 billion, with 8 million provider resources. In addition,
Cigna processes 7 million claims/hour.

#### Northwell

Northwell is running the FHIR server in Azure using an open-source
deployment. However, they’ve expressed a strong interest in moving to
the managed service once we support key features such as bundle
transactions. Their current data size is about 10TB.

#### Prospective Customers in India

Interestingly we’ve received requests from accounts teams in India that
we provide the FHIR service there even though India is not included in
our current geo expansion plan. By estimate these future customers have
data sizes within the 4-100 TB range.

| **Customer Name** | **Data Volume** | **API Calls/month** | **Note**                                            |
|-------------------|-----------------|---------------------|-----------------------------------------------------|
| Dr Lal Path Labs  | 20 TB           | TBD                 | Evaluating Datalake (Azure/AWS/GCP)                 |
| Max Healthcare    | TBD             | TBD                 | NDHM (National Digital Health Mission) and datalake |
| Apollo            | 17TB            |                     | NDHM and Data lake                                  |
| Fortis            |                 |                     | NDHM                                                |
| HCG               | 10TB            |                     | Data lake                                           |
| Narayana Health   | 10TB            |                     | NDHM                                                |
| WNS HealthHelp    | 25TB            | TBD                 |                                                     |
| Citius Tech       | 5TB             | TBD                 | Analytics IP – H-Scale and BI-Clinical              |

## Related Work 

*Guidance: What other features are related to this work? Please include
links.*

Provisioning SQL database is not something new to us. Though the managed
service uses Cosmos DB as data store, the open-source deployment allows
customers to choose either Cosmos DB or SQL database.

However, there are big differences between Gen 1 and Jupiter on what
database technologies are used.

-   We will introduce the Workspace concept which serves the logical
    container for all services and some of their settings including
    database.

-   We will work with different database tiers, specifically Azure SQL
    Elastic Pools which support multiple databases and SQL Database
    Hyperscale

## What is Being Proposed? 

*Guidance: In 20 words or less, describe the proposed solution.*

Provision SQL databases to meet immediate and future customer
requirements and balance database workloads as needed.

## Elevator Pitch / Press Release 

*Guidance: Create a story for your scenario – detail out the customer,
their problem and/or goal, and then specific outcomes the customer will
achieve or how success would be measured. Avoid implementation details.
Think of this as the blog post announcing this feature. 500 words max.*

## Justification, Expected Business Impact, and Value Proposition 

*Guidance: Why are we tackling this scenario? What is the expected
impact? What’s the value proposition of this work?*

Unlike Cosmos DB which provides unlimited data store size, the two SQL
database services we are targeting pose technical limits: max 4 TB for
each SQL Elastic Pool database and max 100 TB for each Hyperscale
database. Given that customer data sizes range from &lt;1TB to
&lt;100TB, Jupiter GA rollout must support all customers and therefore
both SQL Elastic Pool and SQL Hyperscale.

If we do not provide support for customers with data larger than 4TB by
Jupiter GA release, our business will be negatively affected.

-   At best existing customers will not migrate to Jupiter services,
    thus hindering us from providing latest features to them.

-   At worst we will lose existing and new customers to our competitors.

## Target User / Persona 

*Guidance: Specify the target user/persona(s).*

The feature, though it is backend service and does not involve end users
directly, will affect all customers.

## Existing Solutions and Compete Info 

*Guidance: List the various ways in which a user may currently handle
this problem/challenge. With what expectations will customers approach
our solution? What are our competitors doing in this space?*

### GCP

Google Cloud Healthcare APIs are available in 14 regions in 5
geographies, North America, South America, Europe, Asia, and Australia.
Google Cloud Healthcare API data model is made up of projects, datasets,
and data stores which are NoSQL databases.

The underlying infrastructure for the GCP’s healthcare API is built on
Spanner, although you won’t find it in the public documentation,
according to one Google engineer’s blog
[post](https://medium.com/@vneilley/are-all-fhir-apis-the-same-48be75ac4ac5).

Spanner shards the data automatically based on request load and size of
the data, and claims 99.999% availability. One
[stackoverflow](https://stackoverflow.com/questions/63836530/how-big-can-you-go-with-cloud-spanner)
post response states that “basically the \[size\] limit is on the
billing amount for you.”

### AWS

Amazon announced [HealthLake](https://aws.amazon.com/healthlake/) in
December of 2020, which is a HIPAA-eligible service that enables
healthcare providers, health insurance companies, and pharmaceutical
companies to store, transform, query, and analyze health data at
petabyte scale.

Amazon HealthLake supports FHIR CRUD (Create/Read/Update/Delete) FHIR
Search operations, and bulk import and export. It also transforms
unstructured data using specialized ML models.

Amazon HealthLake is available in US East (N. Virginia) only. Customer
data is stored in S3, DynamoDB, and the Amazon Elasticsearch Service
(AES). The statement in the documentation, “Amazon HealthLake enables
customers to bulk export their FHIR data from the HealthLake Data Store
to an S3 bucket”, suggests that HealthLake uses DynamoDB as underlying
data store, as does FHIR Works on AWS.

With DynamoDB, you can create database tables that can store and
retrieve any amount of data and serve any level of request traffic.
There is no limit to the amount of data you can store in a DynamoDB
table, and the service automatically allocates more storage.

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

| Term                                 | Definition                                                                                                                                                                                                                                                                                                                                                                            |
|--------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Azure SQL Database                   | Azure SQL Database is based on SQL Server Database Engine architecture that is adjusted for the cloud environment.                                                                                                                                                                                                                                                                    |
| SQL Database Elastic Pool            | A collection of databases with a shared set of resources managed via a logical SQL server.                                                                                                                                                                                                                                                                                            |
| SQL Database Hyperscale Service Tier | The newest service tier in the vCore-based purchasing model for Azure SQL Database. This service tier is a highly scalable storage and compute performance tier that leverages the Azure architecture to scale out the storage and compute resources for an Azure SQL Database substantially beyond the limits available for the General Purpose and Business Critical service tiers. |
|                                      |                                                                                                                                                                                                                                                                                                                                                                                       |
|                                      |                                                                                                                                                                                                                                                                                                                                                                                       |

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

| Goal                                                                   | Target Release | Priority |
|------------------------------------------------------------------------|----------------|----------|
| Provision SQL Database for FHIR and DICOM to support data up to 4 TB   | 3/31/21        | P0       |
| Support security benchmarks e.g. CMK                                   | 3/31/21        | P0       |
| Deprovision database when the service instance is deleted.             | 3/31/21        | P0       |
| Provision SQL Database for FHIR and DICOM to support data up to 100 TB | 5/31/21        | P0       |
| Enable SQL database load balance                                       | 5/31/21        | P0       |
| Support high availability for database                                 | 7/31/21        | P0       |
| Support disaster recovery for database                                 | 7/31/21        | P0       |
| Rotate database credentials periodically                               | 7/31/21        | P0       |
| Provide audit logs and metrics                                         | 7/31/21        | P0       |
|                                                                        |                |          |
|                                                                        |                |          |
|                                                                        |                |          |
|                                                                        |                |          |

## Non-Goals (PM/Dev) 

*Guidance: This section describes the topical customer goals that this
feature is specifically not addressing, and why.*

| Non-Goal                            | Mitigation                                                                                                                                                                     |
|-------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Use Cosmos DB or Azure SQL Database | We have decided to use SQL Database Elastic Pool as data store.                                                                                                                |
| Use Azure Storage or Synapse        | No decision has been made to use Azure Storage or Synapse. Any change to data store, e.g. support for large dataset exceeding 100 TB, will be addressed in a separate PM spec. |
| Provision SQL Database for IoT      | Not required because IoT does not use SQL Database.                                                                                                                            |

## Scenarios and Use Cases (PM/Dev) 

*Guidance: This section describes the customer scenarios that this
feature is designed to address. Include how the feature is used to solve
the scenario/use case. Following these steps should be used to validate
the feature.*

| Scenario / Use Case                                                                  | Steps to fulfill the scenario                                                                                                                | Priority |
|--------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------|----------|
| The user provisions a FHIR or DICOM service.                                         | Go to Azure portal or run PowerShell or CLI.                                                                                                 | P0       |
| The user specifies CMK requirement during initial provision.                         | Create a database accordingly to support CMK.                                                                                                | P0       |
| The user enables CMK requirement post deployment from the portal.                    | Modify customer data store to support CMK. Warn and notify users if service is disrupted.                                                    | P0       |
| The user requests to store more than 4 TB from the portal.                           | Modify customer data store to support data growth. Warn and notify the user if the service is disrupted.                                     | P0       |
| The user enables high availability from the portal.                                  | Notify users when completed, or if not supported.                                                                                            | P0       |
| The user enables disaster recovery from the portal.                                  | Notify users when completed, or if not supported.                                                                                            | P0       |
| The system or internal service detects workloads that are unevenly distributed.      | Move workloads accordingly and notify customers that are impacted by the re-allocation/balancing.                                            | P1       |
| The system or internal service alerts that database credentials are about to expire. | Rotate database keys.                                                                                                                        | P0       |
| The user views logging data and downloads it.                                        | Provide user interface to allow the user to browse logging data visually from the portal, and an option to download data in a common format. | P1       |
| The user deletes a service instance that is associated with a SQL database.          | Remove the database as part of the service or account de-commission process.                                                                 | P0       |

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

| Feature Name                                | Nature of dependency                                      | Mitigation/Fallback                                                                                                                                                                                                                                                                                                                     | PM           | Dev      |
|---------------------------------------------|-----------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|--------------|----------|
| SQL Server and Elastic Pool                 | Prerequisite to creating databases in the specified pool. | Create SQL Server and Elastic Pool on the fly or on demand.                                                                                                                                                                                                                                                                             | Benjamin Xue | Jack Liu |
| Workspace and underlying service instances. | Both FHIR and DICOM use SQL databases as data stores.     | Create a SQL database and assigned it to the customer at the provision time. To accelerate database provisioning, databases may be created before hand. However, doing so will result in database costs before they are assigned to a customer. Also, it requires that the data persistence provider manages active and idle databases. | Benjamin Xue | Jack Liu |
|                                             |                                                           |                                                                                                                                                                                                                                                                                                                                         |              |          |

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

Initial rollout: 3/31/2021

GA: 4/30/2021

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

# FHIR Disaster Recovery Gen 1

## Enable customers to restore and run FHIR service quickly when disaster happens

# Scenario Contacts 

**Program Manager (PM):** Benjamin Xue

**Software Engineer (Dev):** Deepak Bansal, Scott Taladay, Abhijeet
Thacker

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

[Azure API for
FHIR](https://azure.microsoft.com/en-us/services/azure-api-for-fhir/)
based on HL7 FHIR spec was officially launched in October 2019. Since
then, more than 1000 customers have signed up for the managed service,
and the number is growing. Among them there are large enterprise
customers running business or mission critical workloads. While the
service is designed with security and scalability in mind, an increasing
number of customers have asked us to provide protection to their data
through geographical replication and disaster recovery (DR) offering. It
is time to make critical features like DR available to all customers
without any further delay.

**Phase I - Limited Release. Available to customers per request, mid-April, 2021**
	 
Features and limits include:
	 
1. Data replication is available in an Azure paired region or a specific region where applicable.
1. Recovery Point Objective (RPO) is 15 minutes; Recovery Time Objective (RTO) is 120 minutes.
1. Database failover takes place when the disaster happens.  Automatic failover is enabled per Cosmos DB documentation.
1. A one-time support ticket is required to enable the feature
	 
**Phase II - Generally available, mid-May, 2021**
	 
Features and limits include:
	 
1. A secondary region including compute and database is available. Phase II is built on top of Phase I.
1. Recovery Point Objective (RPO) is 15 minutes; Recovery Time Objective (RTO) is 60 minutes.
1. The DR option is NOT available through the Azure portal. Support ticket is required to enable the DR option. This is a change to the initial plan.
1. The secondary compute environment is for DR only, not accessible to users.

It is important to note the difference between HA and DR. HA is about
eliminating single points of failure through hardware and software
redundancy, whereas DR is about restoring the service following a
disaster that interrupts the service. DR essentially picks up where HA
left and depends on two different objectives, Recovery Point Objective
(RPO) and Recovery Time Object (RTO). The goals of DR are have low or
even zero RPO and RTO.

This document focuses on the DR for Gen 1, not high availability.

## Supporting Customer Insights 

*Guidance: This section should include direct quotes from customers,
direct quotes from the field, and summaries of interactions with
customers in which they describe the problem they are having.*

#### Walgreens

Walgreens is rolling out a Covid-19 vaccination program leveraging Azure
API for FHIR and scheduled to showcase the service at the “Good Morning
America” show. They reported some performance issues in February 2021
related to 429 errors, or “too many requests,” due to high transaction
volumes. We have since addressed the performance issue by increasing
their compute replicas and database RU/s. The customer anticipates
increasing workloads and has specifically asked for the DR service
offering among other things. Our leadership team has met with the
customer and agreed that we will make the DR offering available as
quickly as we can.

#### Walmart

On the list of FHIR requirements is “API FHIR: Multi-region data
replication and failover”. The customer has been asking for an ETA of
the feature and escalated the issue through our support channel.

#### AXA

The German customer plans to launch their solution in April 2021 and has
communicated to us through our Smokejumpers team that DR is one of the
requirements they must meet for the launch. We have discussed with them
about our DR plan and asked for their RPO and RTO requirements. While
the customer did not share any specific RPO and RTO numbers, they stated
that they expected an automated DR experience.

## Related Work 

*Guidance: What other features are related to this work? Please include
links.*

The DR offering depends on both frontend compute and backend database.
One related work item is our annual Business Continuity and Disaster
Recovery test. On July 10, 2020, we performed a failover test to
evaluate Azure API for FHIR’s BCDR readiness. The test outcome was
captured in an internal document titled “Project Resolute FY21 BCDR
Test”.

## What is Being Proposed? 

*Guidance: In 20 words or less, describe the proposed solution.*

Provide the disaster recovery offering to all customers for Azure API
for FHIR Gen 1

## Elevator Pitch / Press Release 

*Guidance: Create a story for your scenario – detail out the customer,
their problem and/or goal, and then specific outcomes the customer will
achieve or how success would be measured. Avoid implementation details.
Think of this as the blog post announcing this feature. 500 words max.*

## Justification, Expected Business Impact, and Value Proposition 

*Guidance: Why are we tackling this scenario? What is the expected
impact? What’s the value proposition of this work?*

Disaster recovery capability is one of the critical requirements for any
business/mission critical systems. By enabling the feature, we will
effectively address one of the feature gaps that some large customers
have demanded. Doing so will not only help improve customer satisfaction
with our service but also improve our competitive edge as a healthcare
service provider.

## Target User / Persona 

*Guidance: Specify the target user/persona(s).*

The solution will be used by system administrators and business analysts
in healthcare organizations.

## Existing Solutions and Compete Info 

*Guidance: List the various ways in which a user may currently handle
this problem/challenge. With what expectations will customers approach
our solution? What are our competitors doing in this space?*

Our key competitors all have a better story around this currently.

### GCP

Google Cloud Healthcare APIs are available in 14 regions in 5
geographies, North America, South America, Europe, Asia, and Australia.
Google Cloud Healthcare API data model is made up of projects, datasets,
and data stores which are NoSQL databases.

According to Google documentation, datastore automatically handles
sharding and replication. However, there is nothing specific to DR
offering or pricing for FHIR. In fact, Once created datasets cannot be
moved from one location to another.

It’s worth mentioning that the underlying infrastructure for the GCP’s
healthcare API is built on Spanner, although you won’t find it in the
public documentation, according to one Google engineer’s blog
[post](https://medium.com/@vneilley/are-all-fhir-apis-the-same-48be75ac4ac5).
Among key features Spanner comes on-demand backup and restore feature
for data protection.

### AWS

Amazon
[introduced](https://aws.amazon.com/about-aws/whats-new/2020/12/introducing-fhir-works-on-aws/)
FHIR Works on AWS in October of 2020. [FHIR Works on
AWS](https://aws.amazon.com/solutions/implementations/fhir-works-on-aws/)
currently supports over 120 FHIR resources (out of approximately 140),
with strong support in the clinical and financial context. Support for
additional **\[**resources is being added over time. Users are
responsible for their DR implementation.

Amazon announced [HealthLake](https://aws.amazon.com/healthlake/) in
December of 2020, which is a HIPAA-eligible service that enables
healthcare providers, health insurance companies, and pharmaceutical
companies to store, transform, query, and analyze health data at
petabyte scale.

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

| Term                           | Definition                                                                                                                                                                                                                                                                                                         |
|--------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| High Availability (HA)         | High Availability ensures that the service is high available by eliminating single points of failure through hardware redundancy, software redundancy, and in cloud provider scenarios [environmental](https://www.wintellect.com/high-availability-vs-disaster-recovery/) redundancy e.g. data center redundancy. |
| Disaster Recovery (DR)         | Disaster Recovery refers to the process of restoring the service following disaster that interrupts the service.                                                                                                                                                                                                   |
| Recovery Point Objective (RPO) | Recovery Point Objective is a measure of the maximum tolerable amount of data that the business can afford to lose during a disaster.                                                                                                                                                                              |
| Recovery Time Objective (RTO)  | Recovery Time Objective is a measure of maximum time it takes to recover the service and its infrastructure following a disaster to ensure business continuity.                                                                                                                                                    |

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


| Goal                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   | Target Release | Priority |
| -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------- | -------- |
| Customer opens a support ticket request to enable DR on their Azure API for FHIR Account                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               | 12-Apr         | P0       |
| [We create and enable a Cosmos DB read replica in Azure Paired region or a specific region based on customer request. The read replica is for Geo replication only and not accessible to the customer. Note that the read replica will be become primary read/write data. (https://docs.microsoft.com/en-us/azure/best-practices-availability-paired-regions)](https://nam06.safelinks.protection.outlook.com/?url=https%3A%2F%2Fdocs.microsoft.com%2Fen-us%2Fazure%2Fbest-practices-availability-paired-regions&data=04%7C01%7CBenjamin.Xue%40microsoft.com%7C6f8dba8c4e0e45dd5f7f08d8d519ae7e%7C72f988bf86f141af91ab2d7cd011db47%7C1%7C0%7C637493653563885410%7CUnknown%7CTWFpbGZsb3d8eyJWIjoiMC4wLjAwMDAiLCJQIjoiV2luMzIiLCJBTiI6Ik1haWwiLCJXVCI6Mn0%3D%7C1000&sdata=DZoSfwb5N9%2Bsa%2F067dLTQ6V1DYSmbyrZy5lbMFVt04E%3D&reserved=0) | 12-Apr         | P0       |
| [We follow Cosmos DB process for failover and failback. https://docs.microsoft.com/en-us/azure/cosmos-db/high-availability](https://docs.microsoft.com/en-us/azure/cosmos-db/high-availability)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        | 12-Apr         | P0       |
| We offer Recovery Point Objective (RPO) is 15 minutes; Recovery Time Objective (RTO) is 120 minutes.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   | 12-Apr         | P0       |
| We create a secondary environment of compute in an Azure paired region or specific region for customers with DR enabled and enable all customer settings if applicable. The secondary compute environment is for DR only, not accessible to users.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     | 17-May         | P0       |
| At GA we update our billing (and pricing page) reflecting the cost of enabling DR on the account.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      | 17-May         | P0       |
| We offer Recovery Point Objective (RPO) is 15 minutes; Recovery Time Objective (RTO) is 60 minutes. https://docs.microsoft.com/en-us/azure/cosmos-db/consistency-levels                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      | 17-May         | P0       |
| We do not expose the DR setting in the Azure Portal and through CLI. The DR option is only available to customers through a support ticket.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  | 17-May         | P1       |
| We add the DR audit trail to audit logs for export when the customer enables/disables the DR option.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   | 17-May         | P1       |



## Non-Goals (PM/Dev) 

*Guidance: This section describes the topical customer goals that this
feature is specifically not addressing, and why.*

| Non-Goal                                                                           | Mitigation                                                     |
|------------------------------------------------------------------------------------|----------------------------------------------------------------|
| As a user I want to have an active/active HA configuration with two write replicas | Out of scope. The replica in the secondary region is read-only |
| As a user I can enable or disable the DR option through the portal                 | The full DR experience will be addressed in Jupiter release    |

## Scenarios and Use Cases (PM/Dev) 

*Guidance: This section describes the customer scenarios that this
feature is designed to address. Include how the feature is used to solve
the scenario/use case. Following these steps should be used to validate
the feature.*

| Scenario / Use Case                                                                                                         | Steps to fulfill the scenario                                                                                                                                                                                 | Priority |
|-----------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------|
| As a service administrator I can request a data replica for the Azure API for FHIR                                        | Open a SR support ticket through the portal or with CSS to request creating a secondary data replica for Azure API for FHIR. This step is only required before the DR feature is generally available.                                                                      | P0       |
| As a Service administrator, I can request a specific region (different from Azure paired regions) for the seconary data replica for Azure API for FHIR                     | Specify an Azure region in the SR support ticket. The customer can choose an Azure paired region, or any region, not limited by single region data residency requirements, as long as it is a supported region.   | P0       |
| As a Service administrator, I can request disaster recovery for Azure API for FHIR through a support ticket | Create a support ticket to request to enable or disable the disaster recovery option. Expect initial response time as outlined in Azure support and responsiveness document.                                                                                                       | P0       |

## Scenario KPIs (PM) 

*Guidance: These are the measures presented to the feature team, e.g.
number of FHIR endpoints, total data storage size.*

| Type<br> \[Biz \| Cust \| Tech\] | Outcome | Measure | Target | Priority |
| -------------------------- | ------- | ------- | ------ | -------- |
|                            |         |         |        |          |
|                            |         |         |        |          |


## What’s in the Box? (PM) 

*Guidance: This section lists everything the customer gets in the end.
Is there a new service? Templates? Samples? SDK?*

## Feature Dependencies (PM/Dev) 

*Guidance: This section describes both the dependencies this feature has
on other areas as well as other areas impacted by this work. Examples of
areas which may be impacted: Persistence Provider, FHIR API.*

### Dependencies this design has on other features 

| Feature Name                | Nature of dependency                                                                                                                  | Mitigation/Fallback                                                                                                                                                                                  | PM  | Dev |
|-----------------------------|---------------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----|-----|
| Cosmos DB High Availability | One secondary, read region through Cosmos DB data replication.                                                                                  | Built-in                                                                                                                                                                                             |     |     |
| Azure Traffic Manager       | Routing traffic from the primary region to the secondary region, which then becomes the write region, as part of DR operation.                                                                                  | We need to investigate if/how Traffic Manager plays a role when the read-only option is enabled                                                                                                      |     |     |
| Billing                     | Enabling one read region doubles the Cosmos DB cost and requires a different billing meter. Also, standard data transfer rates apply. | Apply a multiplier of 2 for customers with the DR feature enabled. In the long run we should be able to add support for different billing rates for all underlying billable services, e.g. database. |     |     |
| Cosmos DB automatic failover| Enabling Cosmos DB failover automatically                                | | |
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

Initial rollout: 04/12/2021

GA: 05/17/2021

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

# FHIR Autoscaling Gen 1

## Autoscale Up and Down Compute Instances and Cosmos DB

# Scenario Contacts 

**Program Manager (PM):** Benjamin Xue

**Software Engineer (Dev):** Deepak Bansal, Abhijeet Thacker

# Why There is a Gap Today? (PM) 

*This section is **required** for assignment of engineering resources.*

Section status: \[draft\]

Date reviewed: \[Date\]

## Problem Statement 

The Azure API for FHIR runs in a shared environment,
currently a Service Fabric cluster, in each Azure region. Each
customer account or application is provisioned with default
configuration settings, which includes the compute environment with 2 replicas
(instances) and 5 concurrent sessions for each instance and a Cosmos DB 
database. It is currently a manual process to adjust the settings, 
which has presented a big challenge for customers and for the Microsoft support team.

As transaction volumes increase, customers can adjust the max database
throughput from the default 400 RU/s up to 10k RU/s through the portal, to 
meet the peformance demands. A customer support ticket is required to raise
the max throughput value beyond 10k RU/s. 
The preliminary performance numbers show that at 10k RU/s 
the service can process 225-400 requests per second. Any higher volumes
can result in slow reponse times and 429 errors or “too many requests”.

Another option is to adjust the number of instances and the number of concurrent sessions, 
especially the database throughput RU/s has been significantly increased, 
to support increased requests/second at peak times and reduce the
response times. While it is
a general consensus that the number of compute instances and the underlying concurrent sessions 
should be proportional to the database throughput, the optimal ratios between them can vary 
significantly depending on the operation types, 
simple reads, complex reads or queries, and writes. 

As we continue to expand our customer base and onboard customers with
large production workloads, we have seen a few major support incidents
related to performance issues, mainly 429 errors, among other issues. 
To help customers resolve these issues, we ended up with
increasing max database throughput, the number of instances, the number
of concurrent sessions or a combination of them.

To better serve our customers and meet their business needs, we must
look for ways to support autoscaling as we continue to 
focus on Jupiter release.
Fortunately, both Service Fabric and Cosmos DB provide 
built-in autoscaling capabilities.

Our goals are to enable compute autoscaling and database autoscaling 
for all customers, preferably through the Azure portal or through the 
support team as an alternative, 
to provide better experinces with our healthcare APIs platform 
and reduce support incidents especially those related to 
service peroformance.

**Supporting Customer Insights**

#### Cigna

Cigna, one of the large customers on our healthcare data platform, has
accumulated approximagtely 80 TB over the past sever years or so, mostly prescription claims and provider
directory data, which consists of small resource size but large number of resources, 
3.5 billion resources and 8 million provider resources. In
addition, Cigna processes 7 million claims/hour. They experienced 429
errors and performance throttling issues recently. To support such a
large number of requests/second, we granted them with 1million
RU/s, 45 instances with 25 concurrent sessions for each instance.

#### Walgreens

Walgreens rolledd out a Covid-19 vaccination program on our platform in early 2021. They
recently reported some performance issues due to high transaction
volumes. We worked with the internal support team and addressed the performance issue by increasing
their compute replicas to 16 with 25 concurrent sessions for each, and increased 
database RU/s to 50k.

#### Humana

Humana complained about lack of autoscaling and on getting 429s, and escalated the issue. They indicated taht 
autoscaling on CosmosDB would resolve their issue most likely. As a direct response to the customer request,
we enabled Cosmos DB autocaling manually for the customer in early April. We discovered a technical issue that 
none of the Azure portal settings could be changed or saved. This issue has yet to be addressed.

## Related Work 

There is no directly related work to autoscaling. Currently we have a
manual process with two options that DRIs use.

1.  Use the ResoluteMultiTool to reprovision these accounts as described
    > [here](onenote:#Execute%20MultiTool%20on%20Prod%20From%20non-SAW&section-id={D4D99CD6-8FB8-492B-ADE2-F4FCCDCDD552}&page-id={B7D9FF2A-1DD7-416B-A067-8E2802B4A5C2}&end&base-path=https://microsoft.sharepoint.com/teams/msh/Shared%20Documents/Notebooks/Resolute%20Engineering/Operations/How%20to.one)

2.  Run the Update-FhirServiceApplicationConfig.ps1 as described
    > [here](onenote:#Modify%20the%20Configuration%20of%20a%20Running%20Fhir%20Service&section-id={D4D99CD6-8FB8-492B-ADE2-F4FCCDCDD552}&page-id={C3F7213E-B61A-401A-8A33-B9112F8FBF95}&end&base-path=https://microsoft.sharepoint.com/teams/msh/Shared%20Documents/Notebooks/Resolute%20Engineering/Operations/How%20to.one)

## What is Being Proposed? 

Support compute autoscaling and database autoscaling for Azure
API for FHIR

## Elevator Pitch / Press Release 

## Justification, Expected Business Impact, and Value Proposition 

We have recently seen strong demands from customers for the autoscaling
feature, especially from those who run large production workloads in Azure 
API for FHIR. Our customers especially those large customers have shared and even 
escalated their concerns over the service performance issues and 
asked that we help resolve the issues by providing the autoscaling feature.

While we can leverage the built-in autoscaling features in
Service Fabric and Cosmos DB, we must investiage what impract the change will have 
on our existing design and delployment and what changes we will have to make
to ensure a reliable service offering and a smooth customer experience.

Meanwhile, we will also undersand and communicate the extra costs resulted from the 
autoscaling offering, especially on the Cosmos DB side, which adds a 50% cost increase
when the autoscaling feature is enabled.

## Target User / Persona 

Once the autoscaling is enabled by an authorized user or administrator, no direct user interaction is required.

## Existing Solutions and Compete Info 

GCP 

The underlying infrastructure for the GCP’s healthcare API is built on
Spanner, although you won’t find it in the public documentation,
according to one Google engineer’s
blog [<u>post</u>](https://medium.com/@vneilley/are-all-fhir-apis-the-same-48be75ac4ac5).  One
of Spanner’s key feature is fully managed relational database with
unlimited scale, strong consistency, and up to 99.999% availability.

AWS 

Amazon
announced [<u>HealthLake</u>](https://aws.amazon.com/healthlake/) in
December of 2020, which is a HIPAA-eligible service. While Amazon claims
that HealthLake allows customers to store, transform, query, and analyze
health data at petabyte scale, there seems no documentation on its
scalability.

## Customers/Partners Interaction Log 


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


| Term | Definition |
|------|------------|
|      |            |
|      |            |
|      |            |
|      |            |

## Branding (PM) 

## Detailed Feature Description (PM/Dev) 

## Goals (PM/Dev) 

| Goal                                                                                                                                           | Target Release | Priority |
|------------------------------------------------------------------------------------------------------------------------------------------------|----------------|----------|
| Enable Cosmos DB autoscaling with a max throughput (RU/s) set to the existing, standard scaling max RU/s.                                      | 5/31/21        | P0       |
| Enable compute autoscaling and adjust the number of concurrent sessions accordingly.                                                           | 6/30/21        | P0       |
| Support autoscaling when DR is enabled.                                                                                                        | 6/30/21        | P1       |
| Enable autoscaling settings from the Azure portal.                                                                                             | 6/30/21        | P1       |
|                                                                                                                                                |                |          |
|                                                                                                                                                |                |          |
|                                                                                                                                                |                |          |
|                                                                                                                                                |                |          |
|                                                                                                                                                |                |          |
|                                                                                                                                                |                |          |
|                                                                                                                                                |                |          |
|                                                                                                                                                |                |          |
|                                                                                                                                                |                |          |

## Non-Goals (PM/Dev) 

| Non-Goal                                                                     | Mitigation                                                                                                                |
|------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|
| Change billing the billing service.                                          | Cosmos DB autoscaling costs 50% more than the rate for standard scaling. The existing Cosmos DB billing method ajusts consumed RU/s units by a 1.5 muliplier, thus requiring no change to the billing service for Azure ApI for FHIR. |
| Provide option for compute autoscaling.                                      | Compute autoscaling should be enabled automatically when database autoscaling is enabled.                                      |

## Scenarios and Use Cases (PM/Dev) 

| Scenario / Use Case                                     | Steps to fulfill the scenario                                     | Priority |
|---------------------------------------------------------|-------------------------------------------------------------------|----------|
| The user enables Cosmos DB autoscaling from the portal. | Change the setting to autoscaling. The max RU/s is set to the existing standard scaling RU/s.            | P0       |
| The user disables Cosmos DB autoscaling from the portal. | Change the setting to standard scaling. The max RU/s is kept unchanged.            | P0       |
| The user changes the max database throughput RU/s with autoscaling enabled.      | Change the max RU/s to a number within the supported range.       | P1       |
| The user changes the max database throughput RU/s to a number exceeding the supported range.      | Create a support ticket to request the max RU/s.       | P1         |
| The user verifies if autoscaling is enabled.      | Go to the portal to see the autoscaling setting. In case the portal setting is not available, create a support ticket to confirm the autoscaling status.       | P1         |
|                                                         |                                                                   |          |
|                                                         |                                                                   |          |

## Scenario KPIs (PM) 



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

| Feature Name              | Nature of dependency | Mitigation/Fallback | PM  | Dev |
|---------------------------|----------------------|---------------------|-----|-----|
| Service Fabric Auto Scale | Built-in             |                     |     |     |
| Cosmos DB Auto scale      | Built-in             |                     |     |     |
|                           |                      |                     |     |     |

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

Initial rollout: 05/31/2021

GA: 06/30/2021

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

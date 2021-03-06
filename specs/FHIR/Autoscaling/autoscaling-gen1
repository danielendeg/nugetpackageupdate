# FHIR Compute Autoscaling Gen 1

## Autoscale Up and Down Compute Instances

# Scenario Contacts 

**Program Manager (PM):** Benjamin Xue

**Software Engineer (Dev):** TBD

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

The Azure API for FHIR service runs in a shared environment,
specifically a Service Fabric cluster, in each Azure region. Each
customer account or application is provisioned based on a default
configuration, which includes the compute environment with 2 replicas
(instances) and 5 concurrent sessions for each instance and a Cosmos
database.

As transaction volumes increase, customers can adjust the max database
throughput from 400 RU/s to 10k RU/s through the portal, to avoid or
reduce the number of request errors, including the so called 429 errors
or “too many requests”. A customer support ticket is required to raise
the max value. Often it is necessary to adjust the default configuration
values, the number of instances and the number of concurrent sessions,
to support increased requests/second at peak times and reduce the
response times.

As we continue to expand our customer base and onboard customers with
large production workloads, we have seen a few major support incidents
due to 429 errors among other issues. For customers dealing with
performance throttling issues due to large data volumes we ended up with
increasing max database throughput, the number of instances, the number
of concurrent sessions or a combination of them.

To better serve our customers and meet their business needs, we must
look for options to improve our Gen 1 service capabilities while
minimizing engineering efforts as we focus on Jupiter release.
Fortunately, both Service Fabric and Cosmos DB support autoscaling,
which allows us to scale up (or down) computing resources automatically
and reduce customer support incidents.

Our goals are to enable compute autoscaling and database autoscaling.
However, we may enable database autoscaling for a few customers until we
are ready to support all customers. Note that Cosmos DB autoscaling
costs 50% more than standard or manual scaling and may use a different
meter in some Azure regions.

**Supporting Customer Insights**

*Guidance: This section should include direct quotes from customers,
direct quotes from the field, and summaries of interactions with
customers in which they describe the problem they are having.*

#### Cigna

Cigna, one of the large customers on our healthcare data platform, has
50TB, or 7 years of data, mostly prescription claims and provider
directory data, small resource size but large volume, 3.5 billion,
with 8 million provider resources. In
addition, Cigna processes 7 million claims/hour. They experienced 429
errors and performance throttling issues recently. To support such a
large number of requests/second, we have granted them with 1million
RU/s, 45 instances with 25 concurrent sessions for each instance.

#### Walgreens

Walgreens runs a Covid-19 vaccination program on our platform. They
recently reported some performance issues due to high transaction
volumes. We have since addressed the performance issue by increasing
their compute replicas to 16 with 25 concurrent sessions for each, and
database RU/s to 50k.

## Related Work 

*Guidance: What other features are related to this work? Please include
links.*

There is no directly related work to autoscaling. Currently we have a
manual process with two options that DRIs use.

1.  Use the ResoluteMultiTool to reprovision these accounts as described
    > [here](onenote:#Execute%20MultiTool%20on%20Prod%20From%20non-SAW&section-id={D4D99CD6-8FB8-492B-ADE2-F4FCCDCDD552}&page-id={B7D9FF2A-1DD7-416B-A067-8E2802B4A5C2}&end&base-path=https://microsoft.sharepoint.com/teams/msh/Shared%20Documents/Notebooks/Resolute%20Engineering/Operations/How%20to.one)

2.  Run the Update-FhirServiceApplicationConfig.ps1 as described
    > [here](onenote:#Modify%20the%20Configuration%20of%20a%20Running%20Fhir%20Service&section-id={D4D99CD6-8FB8-492B-ADE2-F4FCCDCDD552}&page-id={C3F7213E-B61A-401A-8A33-B9112F8FBF95}&end&base-path=https://microsoft.sharepoint.com/teams/msh/Shared%20Documents/Notebooks/Resolute%20Engineering/Operations/How%20to.one)

## What is Being Proposed? 

*Guidance: In 20 words or less, describe the proposed solution.*

Support compute autoscaling and possibly database autoscaling for Azure
API for FHIR Gen 1

## Elevator Pitch / Press Release 

*Guidance: Create a story for your scenario – detail out the customer,
their problem and/or goal, and then specific outcomes the customer will
achieve or how success would be measured. Avoid implementation details.
Think of this as the blog post announcing this feature. 500 words max.*

## Justification, Expected Business Impact, and Value Proposition 

*Guidance: Why are we tackling this scenario? What is the expected
impact? What’s the value proposition of this work?*

We have seen recently strong demands from customers for the autoscaling
feature, especially from those who run large production workloads in the
FHIR service. If we don’t address the performance throttling issue
immediately our customers especially those large customers will be
concerned that their business operations that depend on the FHIR service
will be negatively impacted or even constantly interrupted.

On the other side we can leverage the built-in autoscaling features in
Service Fabric and Cosmos DB and make necessary code change to enable
and support them. In the short term doing so means that we need to
invest engineering sources and slow down our Jupiter effort, but in the
long run we can learn from the experience and improve it in Jupiter and
future release to better serve our customers.

## Target User / Persona 

*Guidance: Specify the target user/persona(s).*

Once the autoscaling is enabled, no direct user interaction is required.

## Existing Solutions and Compete Info 

*Guidance: List the various ways in which a user may currently handle
this problem/challenge. With what expectations will customers approach
our solution? What are our competitors doing in this space?*

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

| Term | Definition |
|------|------------|
|      |            |
|      |            |
|      |            |
|      |            |

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

| Goal                                                                                                                                           | Target Release | Priority |
|------------------------------------------------------------------------------------------------------------------------------------------------|----------------|----------|
| Enable compute autoscaling based on one or more criteria with a max number of instances. Adjust the number of concurrent sessions accordingly. | 5/31/21        | P0       |
| Enable autoscaling settings from the Azure portal.                                                                                             | 6/30/21        | P0       |
| Enable Cosmos DB autoscaling with a max throughput (RU/s).                                                                                     | 6/30/21        | P1       |
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

*Guidance: This section describes the topical customer goals that this
feature is specifically not addressing, and why.*

| Non-Goal                                                                     | Mitigation                                                                                                                |
|------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|
| Change billing service to use Cosmos DB autoscaling billing meter and rates. | Cosmos DB autoscaling costs 50% more than the rate for standard scaling. Use a multiplier of 1.5 to adjust billing costs. |
|                                                                              |                                                                                                                           |

## Scenarios and Use Cases (PM/Dev) 

*Guidance: This section describes the customer scenarios that this
feature is designed to address. Include how the feature is used to solve
the scenario/use case. Following these steps should be used to validate
the feature.*

| Scenario / Use Case                                     | Steps to fulfill the scenario                                     | Priority |
|---------------------------------------------------------|-------------------------------------------------------------------|----------|
| The user enables compute autoscaling from the portal.   | Update customer account settings and reprovision the application. | P0       |
| The user enables Cosmos DB autoscaling from the portal. | Update customer account settings and reprovision the application. | P1       |
|                                                         |                                                                   |          |
|                                                         |                                                                   |          |
|                                                         |                                                                   |          |

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

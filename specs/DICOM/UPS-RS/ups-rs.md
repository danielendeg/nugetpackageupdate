Add support for Unified Procedure Step (UPS-RS) to the existing DICOM service. That means to implement a worklist service that manages a single worklist containing one or more workitems. 

# Business Justification

Unified Procedure Step instances used to represent a variety of scheduled tasks such as: Image Processing, Quality Control, 
Computer Aided Detection, Interpretation, Transcription, Report Verification, or Printing.

The UPS instance can contain details of the requested task such as when it is scheduled to be performed or Workitem Codes describing the 
requested actions. The UPS may also contain details of the input information the performer needs to do the task and the output the performer 
produced, such as: Current Images, Prior Images, Reports, Films, Presentation States, or Audio recordings.

## Customer Requirements

Zeiss wants to assign patients to specific examination or treatment devices. This is the typical modality worklist (MWL) scenario. 
The basic properties are WHO (Patient), WHEN (Planned Date/Time) and WHERE (Device/Station). Zeiss's devices perform a DIMSE C-FIND query using the DICOM Basic Worklist Management Service against a custom proxy cloud / on-prem gateway component called Dicom Forwarder. This service polls the Dicom service via UPS-RS according to query keys described below, then translates these keys into MWL format according to a mapping proposal from IHE.

To fulfill this request, we need to add support for UPS-RS to our existing service. Unified Procedure Step is part of the DICOM standard which not only help Zeiss but also future customers.

A high level diagram which explains how SCP and SCU communicates

![Dicom file](../images/SCP_SCU_Communication.jpg =500x)
![Dicom file](../images/SCP_SCU_Communication_1.jpg =500x)

## Microsoft / OSS Requirements
- Implement UPS-RS support as per the DICOM standard and not specific to any single customer.
- Create a solution that is scabale to all customers
- Expose the feature via feature flag and it can be enabled by default.
- Not all part of the UPS-RS standard needs to be implemented. SCU/SCP not required to implement all the SOP Classes. Can implement SOP Classes based on the operations it needs.

## UPS-RS
The Unified Worklist and Procedure Step Service Class includes four SOP Classes associated with UPS instances. Each SOP Class supports a few related operations.

- The UPS Push SOP Class allows an SCU to instruct the SCP to create a new UPS instance, effectively letting a system push a new work item 
onto the SCP's worklist. 
- The UPS Pull SOP Class allows an SCU to query a Worklist Manager (the SCP) for matching UPS instances, and instruct the SCP to update 
the status and contents of selected items (UPS instances). 
- The UPS Watch SOP Class allows an SCU to subscribe for status update events and retrieve the details of work items (UPS instances) 
managed by the SCP.
- The UPS Event SOP Class allows an SCP to provide the actual status update events for work items it manages to relevant (i.e., subscribed) 
SCUs.

## Scope

There are multiple classes to be implemented and that can be done in multiple iterations. For this feature, we will only implement the Push and Pull classes.

| Class | Scope |
| ------ | ------ |
| UPS Push SOP Class   | ✔ |
| UPS Pull SOP Class | ✔ |
| UPS Watch SOP Class | ❌ |
| The UPS Event SOP Class | ❌ |

| Transaction |	Method |	Payload Request	| Payload Response	|	Description | Scope |
| ------ | ------ |------ |------ |------ | ------ |
|Create	|POST|	dataset|	none | Creates a new Workitem| ✔ |
|Retrieve|	GET	|none	|dataset|	Retrieves the Target Workitem| ✔ |
|Update	|POST	|dataset	|none	|Updates the Target Workitem| ✔ |
|Change State	|PUT	|none|	none	|Changes the state of the Target Workitem| ✔ |
|Request Cancellation|	POST|	dataset|	none|	Requests that the origin server cancel a Workitem| ✔ |
|Search	|GET	|none	|results|	Searches for Workitems| ✔ |
|Subscribe|	POST|	none|	none	|Creates a Subscription to the Target Worklist or Target Workitem|❌ |
|Unsubscribe|	DELETE|	none|	none	|Cancels a Subscription from the Target Worklist or Target Workitem|❌ |


## About Workitem

In the Worklist Service, the Workitem is identified by a Workitem UID, which corresponds to the Affected SOP Instance UID and Requested SOP Instance UID used in the UPS Service.

Workitems consist of different modules
1. SOP Common Module
2. Unified Procedure Step Scheduled Procedure Information Module
3. Unified Procedure Step Relationship Module
    1. Patient Demographic Module
    2. Patient Medical Module
    3. Visit Identification Module
    4. Visit Status Module
    5. Visit Admission Module
4. Unified Procedure Step Progress Information Module

[Detailed workitem definition](https://dicom.nema.org/medical/dicom/current/output/html/part04.html#table_CC.2.5-3)

### Dicom Media types

|Media Type|Usage|
|------ | ------ |
|application/dicom+json |DEFAULT|
|multipart/related; type="application/dicom+xml"| required|

**Since our existing APIs, we only support application/dicom+json. We will do the same here. There is a work item in backlog to implement application/dicom+xml**

### Partition support

According to Zeiss, data partition is a pre-requisite to use this feaure. Similar to other APIS like (STOW, WADO, QIDO and DELETE) UPS-RS API will be supported with or without partition depends on the Data Partition feature flag state.

**If the Data partition feature flag is set, then UPS-RS API should be accessed with partition url.**

### UPS Push 

### Create workitem

**Request**

This transaction creates a Workitem on the target Worklist. It corresponds to the UPS DIMSE N-CREATE operation.

```
POST {partition path}/workitems{?AffectedSOPInstanceUID}
Accept: dicom-media-type
```

**Response**
Success - 201

**Errors**
- 400 - Bad request
- 409 - Conflict
- 403 - Forbidden


Once the request comes in
1. We validate the dataset as per the standard and store the json file in a new container, with the `WorkItemUid and WorkItemKey` as the file name. This will be useful to retrieve in a faster manner.
2. Store the primary columns in the WorkItem table and store the queryable fields in the extended query tag table.

### UPS Pull 

### Get workitem

**Request**

This transaction retrieves a Workitem. It corresponds to the UPS DIMSE N-GET operation.

```
GET {partition path}/workitems/{workitemInstance}
```

**Response**
```json
{

}
```
**If the Workitem is in the IN PROGRESS state, the returned Workitem shall not contain the Transaction UID (0008,1195)**


### Update workitem

**Request**

This transaction modifies Attributes of an existing Workitem. It corresponds to the UPS DIMSE N-SET operation.

```
POST {partition path}/workitems/{instance}{?transactionUID}
Content-Type: dicom-media-type
```
If the UPS instance is currently in the SCHEDULED state, {transactionUID} shall not be specified.
If the UPS instance is currently in the IN PROGRESS state, {transactionUID} shall be specified

**Response**
```json
{

}
```
Success - 200

**Errors**
- 400 - Bad request
- 409 - Conflict
- 404 - Not found
- 410 - Gone
- 403 - Forbidden

### Query workitems

**Request**

This transaction searches the Worklist for Workitems that match the specified Query Parameters and returns a list of matching Workitems. Each Workitem in the returned list includes return Attributes specified in the request. The transaction corresponds to the UPS DIMSE C-FIND operation.

```
GET {partition path}/workitems?{&query*}{&includefield}{&fuzzymatching}{&offset}{&limit}
Accept: dicom-media-types
```
- {query}
  - {attributeID}={value}, 0-n / {attributeID}={value} pairs allowed
- includefield={attributeID} | all, 0-n includefield / {attributeID} pairs allowed, where “all” indicates that all attributes with values should be included for each response. Each {attributeID} shall refer to an attribute of the Unified Procedure Step IOD
- fuzzymatching=true | false
- limit={maximumResults}
- offset={skippedResults} 

**Response**
```json
{

}
```
Success - 200

**Errors**
- 400 - Bad request
- 409 - Conflict
- 404 - Not found
- 410 - Gone

#### Search Attributes
The following attributes will be supported for searching workitems.

|MWL Attribute Name|UPS Mapping|
|------ | ------ |
|1. ScheduledStationAETitle | Station Name Code Sequence (0040,4025)  |
|2. ScheduledProcedureStepStartDate |  Scheduled Procedure Step Start Date and Time (0040,4005) |
|3. Modality | Scheduled Station Class Code Sequence (0040,4026) using codes from DICOM PS3.16 CID 29 Acquisition Modality|
|4. RequestedProcedure ID | Requested Procedure Code Sequence (0032,1064)|
|5. AccessionNumber | Same as Accession Number (0008,0050) |
|6. PatientName | Same as Patient Name|
|7. PatientID | Same as Patient Id |
|8. ScheduledStationGeographicLocationCodeSequence.CodeValue  | Same as ScheduledStationGeographicLocationCodeSequence ((0040,4027).(0008,0100)) |

In Zeiss HDP, there is a requirement to schedule workitem(s) to multiple devices that are located in the same department (or operating room) but belong to the same tenant. 
Since AETitle is not unique (globally, only in a Local Area Network), hence we will need to group devices based on some additional attribute(s). 
One such sequence/attribute could be the UPS ScheduledStationGeographicLocationCodeSequence.

#### Attribute Matching
The following types of matching will be supported while searching workitems:

- Exact match (Single value matching)
- Range matching for dates
- Fuzzy matching on patient name
- Limited Sequence matching for Station Name Code Sequence (AE Title), Scheduled Station Class Code Sequence (Modality) and ScheduledStationGeographicLocationCodeSequence
  - SQ elements contains data elements with VM = 1
  - Only the first level elements will be searched

### Cancel workitem

**Request**

This transaction allows a user agent that does not own a Workitem to request that it be canceled. It corresponds to the UPS DIMSE N-ACTION operation "Request UPS Cancel". 

```
POST {partition path}/workitems/{workitem}/cancelrequest
Accept: dicom-media-types
```
The request body describes a request to cancel a single Unified Procedure Step Instance.
The request may include a Reason For Cancellation and/or a proposed Procedure Step Discontinuation Reason Code Sequence.
The request may also include a Contact Display Name and/or a Contact URI for the person with whom the cancel request may be discussed

**Response**
```json
{

}
```
Success - 202

**Errors**
- 400 - Bad request
- 409 - Conflict
- 404 - Not found
- 403 - Forbidden

### Change workitem state

**Request**

This resource supports the modification of the state of an existing workitem Instance.

```
PUT {partition path}/workitems/{workitemInstance}/state
```

**Response**
```json
{

}
```

**Errors**
- 400 - Bad request
- 409 - Conflict
- 404 - Not found
- 403 - Forbidden

**UPS-RS State model**

SCHEDULED  - IN-PROGRESS  - COMPLETED
                   |
               CANCELLED

**Customer request**
From the UPS-RS state model, we only plan to use the SCHEDULED and CANCELLED states directly. 
We expect SCHEDULED to be set for new items. CANCELLED is the final state when a SCHEDULED entry was removed by the Request Cancellation transaction. 
When executing the search query, there will be a filter on SCHEDULED, since only these entries are of interest.


## Delete workitems

Workitems in CANCELLED OR COMPLETED state can be (but are not required to be) deleted after a period of time per Zeiss. We will not implement any deletion mechanism for this feature since
we are not clear about the broader market implications and needs - auditing, recoverability, etc.

##Storage

There are several implementation options:

|Options|Pros| Cons |
|------ | ------ |------ |
|1. Create a workitem table with all the columns that are part of UPS object definition.| Easy to access and query | We will index many unused tags, and will require parsing complex data structures on retrieval |
|2. Create a workitem table that only includes commonly accessed columns similar to other DICOM tables.| Easy to access and query | No good way to model searchable sequences |
|3. Create a workitem table with high level columns, and index the scoped columns that are required by Zeiss now to ExtendedQueryTags table. Stored procedures for extended query tags and workitems could both use a common internal stored procedure. | Scalable and customizable, enables searching sequences | More logic in the common stored procedures, less clear isolation between workitem tags and image tags |
|4. Create a workitem table with high level columns, and index the scoped columns that are required by Zeiss now to a new WorkitemQueryTags table. We would duplicate stored procedures for extended query tags and workitems. | Scalable and customizable, enables searching sequences | Duplicate logic - higher maintenance cost |


```sql


CREATE TABLE dbo.WorkItem (
    WorkItemKey              BIGINT             NOT NULL,             --PK
    WorkItemUid              VARCHAR(64)        NOT NULL,
    PartitionKey             INT                NOT NULL DEFAULT 1,   --FK
    --audit columns
    CreatedDate              DATETIME2(7)       NOT NULL,
) WITH (DATA_COMPRESSION = PAGE)


CREATE TABLE dbo.WorkItemQueryTag (
    TagKey                  INT                  NOT NULL, --PK
    TagPath                 VARCHAR(64)          NOT NULL,
    TagVR                   VARCHAR(2)           NOT NULL
)

ALTER TABLE dbo.ExtendedQueryTagLong

TO 

CREATE TABLE dbo.QueryTagLong (
    TagKey                  INT                  NOT NULL,              --PK
    TagValue                BIGINT               NOT NULL,
    ForeignKey1             BIGINT               NOT NULL,              --FK
    ForeignKey2             BIGINT               NULL,                  --FK
    ForeignKey3             BIGINT               NULL,                  --FK
    Watermark               BIGINT               NOT NULL,
    PartitionKey            INT                  NOT NULL DEFAULT 1     --FK
    ResourceType            TINYINT              NOT NULL DEFAULT 0     
) WITH (DATA_COMPRESSION = PAGE)


-- ForeignKey1 is used both as WorkItemKey and StudyKey

-- This table will be used both for Image Instance and UPS-RS instance.

```

## Migration

There is no specific migration Strategy, we only need to update the column names and table names.

## Roll-out Strategy

Once the complete feature is completed, we can enable the feature by default.

We will be doing this feature in multiple iterations 

Iteration 1:
 - Deliver UPS-RS Create, Retrieve, Query (without sequence matching), Cancel State

Iteration 2:
 - Query (with sequence matching), Change workitem state

# Test Strategy

# Security

# Other

# Open Questions

- Number of requests that will come from SCP
    - C-Find queries every 10 minutes. More the devices, more the requests (30 requests per hour per device). Zeiss will come back to us with some numbers
- Should we delete CANCELLED, COMPLETED workitem ?
- Do we need to support changing state to COMPLETED?
- The Attributes of the Scheduled Station Name Code Sequence shall only be retrieved with Sequence Matching. But the requirements says exact match or sequence match.
- The Attributes of the Scheduled Station Class Code Sequence shall only be retrieved with Sequence Matching. But the requirements says exact match or sequence match.
- Will a workitem involves multiple devices?
- Do we need to support MV (value multitiplicity)

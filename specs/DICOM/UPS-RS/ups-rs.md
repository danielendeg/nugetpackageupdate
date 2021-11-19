Add support for Unified Procedure Step (UPS-RS) to the existing DICOM service. That means to implement a worklist service that manages a single worklist containing one or more workitems. 

# Business Justification

Unified Procedure Step instances may be used to represent a variety of scheduled tasks such as: Image Processing, Quality Control, 
Computer Aided Detection, Interpretation, Transcription, Report Verification, or Printing.

The UPS instance can contain details of the requested task such as when it is scheduled to be performed or Workitem Codes describing the 
requested actions. The UPS may also contain details of the input information the performer needs to do the task and the output the performer 
produced, such as: Current Images, Prior Images, Reports, Films, Presentation States, or Audio recordings.

## Customer Requirements

Zeiss wants to assign patients to specific examination or treatment devices. This is the typical modality worklist scenario. 
The basic properties are WHO (Patient), WHEN (Planned Date/Time) and WHERE (Device/Station). Zeiss's devices perform a query using the DICOM Basic Worklist Management Service. 
The main query keys are the AE Title to identify the device itself and range selection on the planned procedure date.

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

There are multiple classes to be implement and that can be done in multiple iterations. Initially we wont be implementing all the classess for UPS.

| Classs | Scope |
| ------ | ------ |
| UPS Push SOP Class   | ✔ |
| UPS Pull SOP Class | ✔ |
| UPS Watch SOP Class | ✔ ❌ |
| The UPS Event SOP Class | ❌ |

For UPS Watch SOP Class, we will only do **request cancellation** of a worklist item for first iteration.

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


### UPS Push 

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


### UPS Pull 

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

**Request**

This transaction modifies Attributes of an existing Workitem. It corresponds to the UPS DIMSE N-SET operation.

```
POST {partition path}/workitems/{instance}{?transactionUID}
Content-Type: dicom-media-type
```

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

###Queryable Fields

For our initial iteration, we are going to index only few columns and make it searchable.

|MWL Attribute Name|UPS Mapping|
|------ | ------ |
|1. ScheduledStationAETitle | Station Name Code Sequence (0040,4025) putting AE Title in the code meaning with a local coding scheme |
|2. ScheduledProcedureStepStartDate |  Scheduled Procedure Step Start Date and Time (0040,4005) |
|3. Modality | Scheduled Station Class Code Sequence (0040,4026) using codes from DICOM PS3.16 CID 29 Acquisition Modality|
|4. RequestedProcedure ID | Requested Procedure Code Sequence (0032,1064)|
|5. AccessionNumber | Same as Accession Number (0008,0050) |
|6. PatientName | Same as Patient Name|
|7. PatientID | Same as Patient Id |

All these are mapped to different tag in the UPS-RS object definition



Reusing ExtendedQueryTags table with few modifications

There are two operations

1. Create duplicate tables to store the tags that are relevant to UPS-RS
2. Use the existing table, but add a new column type to differentiate between UPS-RS and normal request
    Introduce new primary key instead of 

##Storage

There are two options to implement, 
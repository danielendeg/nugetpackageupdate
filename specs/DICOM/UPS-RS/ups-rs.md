Add support for Unified Procedure Step (UPS-RS) to the existing DICOM service. That means to implement a worklist service that manages a single worklist containing one or more workitems. 

# Business Justification

Unified Procedure Step instances may be used to represent a variety of scheduled tasks such as: Image Processing, Quality Control, 
Computer Aided Detection, Interpretation, Transcription, Report Verification, or Printing.

The UPS instance can contain details of the requested task such as when it is scheduled to be performed or Workitem Codes describing the 
requested actions. The UPS may also contain details of the input information the performer needs to do the task and the output the performer 
produced, such as: Current Images, Prior Images, Reports, Films, Presentation States, or Audio recordings.

## Customer Requirements

Zeiss want to assign patients to specific examination or treatment devices. This is the typical modality worklist scenario. 
The basic properties are WHO (Patient), WHEN (Planned Date/Time) and WHERE (Device/Station). Zeiss's devices perform a query using the DICOM Basic Worklist Management Service. 
The main query keys are the AE Title to identify the device itself and range selection on the planned procedure date.

To fulfill this request, we need to add support for UPS-RS to our existing service. Unified Procedure Step is part of the DICOM standard which not only help Zeiss but also future customers.

A high level diagram which explains how SCP an SCU communicates

![Dicom file](../images/SCP_SCU_Communication_1.jpg =500x)

## Microsoft / OSS Requirements
- Implement UPS-RS support as per the DICOM standard and not specific to any single customer.
- Create a solution that is scabale to all customers
- Expose the feature via feature flag.
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


In the Worklist Service, the Workitem is identified by a Workitem UID, which corresponds to the Affected SOP Instance UID and Requested SOP Instance UID used in the PS3.4 UPS Service.




##Operations

List of operations

1.
2. 
3.

Out of scope




### UPS Push 
```
POST {partition path}/workitems{?AffectedSOPInstanceUID}
```


Queryable Fields

Reusing ExtendedQueryTags table with few modifications

There are two operations

1. Create duplicate tables to store the tags that are relevant to UPS-RS
2. Use the existing table, but add a new column type to differentiate between UPS-RS and normal request
    Introduce new primary key instead of 

###Update
```
POST {partition path}/workitems/{instance}{?transaction}
```

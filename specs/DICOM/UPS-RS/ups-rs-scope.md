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
| UPS Watch SOP Class | ❌ |
| The UPS Event SOP Class | ❌ |


| Transaction |	Method |	Payload Request	| Payload Response	|	Description | Scope |
| ------ | ------ |------ |------ |------ | ------ |
|Create	|POST|	dataset|	none | Creates a new Workitem| ✔ |
|Retrieve|	GET	|none	|dataset|	Retrieves the Target Workitem| ✔ |
|Update	|POST	|dataset	|none	|Updates the Target Workitem| ✔ |
|Change State	|PUT	|none|	none	|Changes the state of the Target Workitem| ✔ |
|Request Cancellation|	POST|	dataset|	none|	Requests that the origin server cancel a Workitem| ✔ |
|Search	|GET	|none	|results|	Searches for Workitems | ✔ |
|Subscribe|	POST|	none|	none	|Creates a Subscription to the Target Worklist or Target Workitem|❌ |
|Unsubscribe|	DELETE|	none|	none	|Cancels a Subscription from the Target Worklist or Target Workitem|❌ |


### Dicom Media types

|Media Type|Usage|
|------ | ------ |
|application/dicom+json |DEFAULT|
|multipart/related; type="application/dicom+xml"| required|

**Current existing APIs, we only support application/dicom+json. We will do the same here. There is a work item in backlog to implement application/dicom+xml**

### Partition support

Similar to other APIS like (STOW, WADO, QIDO and DELETE) UPS-RS API will be supported with or without partition depends on the Data Partition feature flag state.
**If the Data partition feature flag is set, then UPS-RS API should be accessed with partition url.**


## Search Workitem

Following attributes will be supported for searching workitems.

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

Following types of matching will be supported while searching workitems

- Exact match (Single value matching)
- Range matching for dates
- Fuzzy matching on patient name
- Limited Sequence matching for Station Name Code Sequence (AE Title), Scheduled Station Class Code Sequence (Modality) and ScheduledStationGeographicLocationCodeSequence
  - SQ elements contains data elements with VM = 1
  - Only the first level elements will be searched

## Iteration proposal

###1

- Create Workitem
- Searching workitems including all the above attribute matching types with Limited Sequence matching
- Request Cancellation

###2
- Update Workitem
- Retrieve Workitem
- Change state


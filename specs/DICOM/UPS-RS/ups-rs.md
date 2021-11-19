Add support for UPS-RS to the existing DICOM service. That means to implement a worklist service that manages a single worklist containing one or more workitems. 

# Business Justification

## Customer Requirements
Basically, we want to assign patients to specific examination or treatment devices. This is the typical modality worklist scenario. 
The basic properties are WHO (Patient), WHEN (Planned Date/Time) and WHERE (Device/Station). Our devices perform a query using the DICOM Basic Worklist Management Service. 
The main query keys are the AE Title to identify the device itself and range selection on the planned procedure date.

Unified Procedure Step instances may be used to represent a variety of scheduled tasks such as: Image Processing, Quality Control, 
Computer Aided Detection, Interpretation, Transcription, Report Verification, or Printing.
The UPS instance can contain details of the requested task such as when it is scheduled to be performed or Workitem Codes describing the 
requested actions. The UPS may also contain details of the input information the performer needs to do the task and the output the performer 
produced, such as: Current Images, Prior Images, Reports, Films, Presentation States, or Audio recordings.

- The UPS Push SOP Class allows an SCU to instruct the SCP to create a new UPS instance, effectively letting a system push a new work item 
onto the SCP's worklist. It is important to note that the SCP could be a Worklist Manager that maintains the worklist for other systems that will 
perform the work, or the SCP could be a performing system itself that manages an internal worklist.
- The UPS Pull SOP Class allows an SCU to query a Worklist Manager (the SCP) for matching UPS instances, and instruct the SCP to update 
the status and contents of selected items (UPS instances). The SCU effectively pulls work instructions from the worklist. As work progresses, 
the SCU records details of the activities performed and the results created in the UPS instance
- The UPS Watch SOP Class allows an SCU to subscribe for status update events and retrieve the details of work items (UPS instances) 
managed by the SCP.
The UPS Event SOP Class allows an SCP to provide the actual status update events for work items it manages to relevant (i.e., subscribed) 
SCUs.
4 SOP Classes can be used to
operate on a UPS object.
Each SOP Class supports a few
related operations.

## Microsoft / OSS Requirements
SCU/SCP not required to implement all the SOP Classes. Can implement SOP Classes based on the operations it needs.


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

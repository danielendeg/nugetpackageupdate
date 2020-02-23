## Business Justification
[Product Opportunity Assessment - DICOM Server.docx](https://microsoft-my.sharepoint.com/:w:/p/stborg/EWt_x-xnfb9MozLTjl_-FdoBkUqT5n0w04m2pK7im6I-2A?e=2pEbaO&xsdata=MDR8MDF8U21pdGhhLlNhbGlncmFtYUBtaWNyb3NvZnQuY29tfGE5YWQ0ZTE1Mjg4ZjQyMzA3Yzg0MDhkN2E4MTUyMDIzfDcyZjk4OGJmODZmMTQxYWY5MWFiMmQ3Y2QwMTFkYjQ3fDF8MHw2MzcxNjI2ODA5OTk3NDk2MzB8VW5rbm93bnxUV0ZwYkdac2IzZDhleUpXSWpvaU1DNHdMakF3TURBaUxDSlFJam9pVjJsdU16SWlMQ0pCVGlJNklrMWhhV3dpTENKWFZDSTZNbjA9fC0x&sdata=ZzBJWGY3STlXaE9lL2UyYis4b3lrbWZwb2VZdmRRN2QwMnBrV1UyWVRlcz0%3D)

## Scenarios
- A DICOMWeb implemetation specified in [DICOM conformance](DICOMWev-Conformance.md) for medical image archiving and Radiology workflow.
- DICOM metadata in [FHIR](https://www.hl7.org/fhir/imagingstudy.html) to integrate HIS and RIS systems for better Physician workflow.

## Design 

### Optimized for
- Large storage
- Flexibility to evolve

### Raw storage
*Azure Blob storage* will be used to store the Part 10 DICOM binary files. Since there are so many transfer syntaxes supported for both ingress and egress, we will store the incoming dcm file *as is* and transcode on the way out, if needed and supported. This will also serve as the master store for original data. We will store 2 blobs for each dicom instance
1. Original DICOM file in  the virtual path /container/{StudyUID}/{SeriesUID}/{SOPInstanceUID}/file.dcm. This file is used to serve the WADO DCIOM get.
2. Metadata portion of the DICOM file for faster metadata GET in azure Blob storage using the virtual path /container/{StudyUID}/{SeriesUID}/{SOPInstanceUID}/file_metadata.dcm. This file is used to serve the WADO metadata get.

We will also need the Study, Series and Instance UID mapping to support WADO GET on Study/Series. Where we will store this mapping will be informed by the index storage we choose below.

### Index storage
QIDO supports searching on the dicom tags. We need a efficient storage to search the supported tags quickly. We evaluated several options listed below to support QIDO and FHIR integration

Option|Pros|Cons
----------|----------|----------
FHIR| -Single index store, easy to maintain<br/>-*ExactMatch, Wildcard, ListMatch, SequenceMatch, RangeMatch and FuzzMatch supported|-Limited DICOM search support<br/>-Custom attributes for custom tag query support<br/>-DICOM is tightly coupled to FHIR which reduces business, operational and feature flexibility<br/>-Query and Ingestion performance concerns, including query paging needs to be handled by client for instance/series search<br/>-Corner cases around which service is the master of Patient resource type<br/>[Details](DicomWev-FHIR-SingleStoreTradeOff.md)|
[SQL + Async FHIR resouce creation](https://microsoft.sharepoint.com/:w:/t/msh/EY8pKt29ueRCijCrHhqBftcB1k1dTH3fiLR0s39xpyVyew)| -All QIDO requirements can be satisfied<br/>-On premise available<br/>-Inplace scaleUp<br/>-Geo-Redudancy and Backup support<br/>-Can resuse the same SQL DB across FHIR and DICOM services|-Dynamic SQL does not perform as good as known cached SQL<br/>-Joins on long table can also be relatively slow<br/>-Need to build crawler to index old data if custom tags will be supported 
COSMOS + Async FHIR resource creation| -Easy|-Diff
[Az Search + Async FHIR resource creation](https://microsoft.sharepoint.com/:w:/t/msh/EY8pKt29ueRCijCrHhqBftcB1k1dTH3fiLR0s39xpyVyew)| -All QIDO requirements can be satisfied<br/>-JSON indexer available for crawling the entier blob storage dataset<br/>-Possibility to extend support NLP and unstructured data searches|-Limited inplace index mapping changes supported<br/>-COGS higher than SQL, considering we need to manage 3 replicas for 99.9 availabilty<br/>-No in-place upgrade to a different tier<br/>-Managed geo-redudancy is not supported<br/>On-prem or dev box solution not available


## Architecture overview
With the above evaluation, we are considering the below design

1. A DICOMWeb end-point for STOW-RS, WADO-RS, QIDO-RS and delete
2. A async pipeline to publish **ImagingStudy** and its references to FHIR 

![Dicom Arch](images/DICOM-server-arch.png)

### Data consistency across stores

Possible initial SQL schema, with characteristics of
1. Table to store UID mapping 
2. Wide table for known core study/series tags that will be indexed
3. Custom tags in log table. Separate table for each SQL value type

``` sql
--Mapping table for dicom retrieval
CREATE TABLE dicom.tbl_UIDMapping (
	--instance keys
	StudyInstanceUID NVARCHAR(64) NOT NULL,
	SeriesInstanceUID NVARCHAR(64) NOT NULL,
	SOPInstanceUID NVARCHAR(64) NOT NULL,
	--audit columns
	CreatedDate DATETIME2 NOT NULL,
	CreatedBy UNIQUEIDENTIFIER NOT NULL,
	--data consitency columns
	Watermark BIGINT NOT NULL,
	Status TINYINT NOT NULL
)

--Table containing normalized standard StudySeries tags
CREATE TABLE dicom.tbl_DicomMetadataCore (
	--Key
	ID BIGINT NOT NULL, --PK
	--instance keys
	StudyInstanceUID NVARCHAR(64) NOT NULL,
	SeriesInstanceUID NVARCHAR(64) NOT NULL,
	--patient and study core
	PatientID NVARCHAR(64) NOT NULL,
	PatientName NVARCHAR(64), 
    PatientNameIndex AS REPLACE(PatientName, '^', ' '),--FT index
	ReferringPhysicianName NVARCHAR(64),
	StudyDate DATE,
	StudyDescription NVARCHAR(64),
	AccessionNumer NVARCHAR(16),
	--series core
	Modality NVARCHAR(16),
	PerformedProcedureStepStartDate DATE
)

CREATE TABLE dicom.tbl_DicomMetadataInt (
	--Key
	ID BIGINT NOT NULL, -- FK
	--instance key
	SOPInstanceUID NVARCHAR(64) NOT NULL,
	--Tag 4*4+4 10/20/30/40, 4 level deep supported
	TagPath VARCHAR(20) NOT NULL,
	--value columns
	IntValue INT,		--[IS, SL, SS, UL]
)
-- tables for each type
	--FloatValue FLOAT,	--[FL, FD]
	--TextValue NVARCHAR(64),  --[AE, AS, PN, SH, LO)
	--StringValue NVARCHAR(64), --[CS]
	--UniqueIdValue VARCHAR(62), --[UI]
	--DatetimeValue DATETIME2, --[DA, DT, TM]
	--Text NVARCHAR(MAX) --[LT, ST]

CREATE TABLE dicom.tbl_PrivateTag (
	TagPath VARCHAR(20) NOT NULL,
	TagType VARCHAR(2) NOT NULL,
	SqlDataType SMALLINT NOT NULL
)
```

#### Data Ingestion Sequence 

![Ingestion Sequence](images/DICOMWeb-Ingestion-Sequence.png)

#### Normalized indexed data for search

Within DICOM SOP Instances claiming to be from the same Patient/Study/Series we can expect inconsistencies. Below rules will be used to handle it

1. OVERWRITE: prefer the latest tag data if conflicting
2. UNION_EMPTY: prefer non-empty tags overs empty

### FHIR integration

- Sync to FHIR will be configurable.
- Async events to to create FHIR resource. 
- DICOM service will be the master for ImagingStudy resourceType. We will have s service Indentity with write access to edit ImagingStudy
- Patient, Practitioner and Encounter ResourceType: Default FHIR service is the master. Configurable.
- Delete?

DEMO

## Test Stratergy
- Unit tests
- EnE tests
- Bug bash
- Scale testing
- OHIF viewer validation and Customer validation

## Roadmap

- [Epics backlog](https://microsofthealth.visualstudio.com/Health/_backlogs/backlog/Medical%20Imaging/Epics/)
- [Feature Timeline](https://microsofthealth.visualstudio.com/Health/_backlogs/ms-devlabs.workitem-feature-timeline-extension.workitem-feature-timeline/Medical%20Imaging/Epics/)



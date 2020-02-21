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
	ID UNIQUEIDENTIFIER NOT NULL, --PK
	--instance keys
	StudyInstanceUID NVARCHAR(64) NOT NULL,
	SeriesInstanceUID NVARCHAR(64) NOT NULL,
	--patient and study core
	PatientID NVARCHAR(64) NOT NULL,
	PatientName NVARCHAR(64), --FT index
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
	ID UNIQUEIDENTIFIER NOT NULL, -- FK
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
Implement a light weight data partition scheme that will enable customers to store multiple copies of the same image with the same UUID on a single DICOM instance.

[[_TOC_]]

# Business Justification

Zeiss is 8000 practices and growing. The operational overhead of maintaining separate DICOM instances for each practice is too high. From their perspective, multi-tenancy is a single instance of DICOM service that can horizontally scale to support any number of practices.
Their key requirement is to allow resources to be cloned to allow data sharing across practices, while maintaining existing UUIDs. This means that within the same DICOM service, there could be multiple DICOM instances sharing one combination of study/series/instance identifiers.
In order to achieve that we need a data partition solution.

# Scenarios

## STOW
Users can specify an optional data partition when storing studies, series and instances. If no partition is specified, the data will be stored in a default partition.

**Request**
```
POST {API_VERSION}/{PARTITION_KEY}/studies
```

**Response**
```json
{
  "00081190":
  {
    "vr":"UR",
    "Value":["{API_VERSION}/{PARTITION_KEY}/studies/d09e8215-e1e1-4c7a-8496-b4f6641ed232"]
  }
  ...
}
```

## WADO
Users can specify an optional data partition when retrieving studies, series and instances. If no partition is specified, the retrieval will be performed against the default partition.

**Request**
```
GET {API_VERSION}/{PARTITION_KEY}/studies/{studyUid}
```

**Response**
```json
{
  "00081190":
  {
    "vr":"UR",
    "Value":["{API_VERSION}/{PARTITION_KEY}/studies/d09e8215-e1e1-4c7a-8496-b4f6641ed232"]
  }
  ...
}
```

## QIDO [Optional]
Users can search studies, series and instances within a partition. We don't support cross partition queries.

If a user doesn't specify partitionId, then the results are from default partition.

**Request**
```
GET {API_VERSION}/{PARTITION_KEY}/studies?...
```

**Response**
```json
[
  {
    "00081190":
    {
      "vr":"UR",
      "Value":["{API_VERSION}/{PARTITION_KEY}/studies/d09e8215-e1e1-4c7a-8496-b4f6641ed232"]
    }
    ...
  }
]
```

## DELETE 
User can delete studies, series and instances within a partition. If partition is unspecified, default partition record is deleted.

**Request**
```
DELETE {API_VERSION}/{PARTITION_KEY}/studies/{studyUid}
```

# Metrics

Add PartitionId as a dimension to current metrics. Whenever STOW, WADO, QIDO and DELETE operation are requested with partitionId, we should emit a metric so that we can know usage of this feature.

# Design

Approaches:
- tenant identifiers
- external metadata
- data partition

Our approach: provide data partitioning to ,...

How to indicate partition:

We want to be consistent across all APIs, and consider how changefeed and DICOM cast will be affected.

| Option | Pros | Cons |
| ------ | ---- | ---- |
| Body   |      | Requires parsing entire body; Zeiss doesn't want |
| Header |      | - FHIR deep links will break      |
| Query Parameter |  | - may break OSS viewers |
| Path Segment | - Closer to DICOM standard | |

Zeiss creates dynamic test environments. So this may result in too many support requests if we do not make it auto for the customers.

We will be introducing a optional partitionId in all the operations. If the partitionId is not given, then the default partitionId `Microsoft.Default` will be used.

- Add a new column to the below tables. It will be a Not nullable column with a default value. 
  - Max length of the paritionId will be 36 characters (guid with hyphens)
  - 

```
CREATE TABLE dbo.Study (
    StudyKey                    BIGINT                            NOT NULL, --PK
    PartitionId                 VARCHAR(36)                       NOT NULL, --PK
    StudyInstanceUid            VARCHAR(64)                       NOT NULL,
    PatientId                   NVARCHAR(64)                      NOT NULL,
    PatientName                 NVARCHAR(200)                     COLLATE SQL_Latin1_General_CP1_CI_AI NULL,
    ReferringPhysicianName      NVARCHAR(200)                     COLLATE SQL_Latin1_General_CP1_CI_AI NULL,
    StudyDate                   DATE                              NULL,
    StudyDescription            NVARCHAR(64)                      NULL,
    AccessionNumber             NVARCHAR(16)                      NULL,
    PatientNameWords            AS REPLACE(REPLACE(PatientName, '^', ' '), '=', ' ') PERSISTED,
    ReferringPhysicianNameWords AS REPLACE(REPLACE(ReferringPhysicianName, '^', ' '), '=', ' ') PERSISTED,
    PatientBirthDate            DATE                              NULL
) WITH (DATA_COMPRESSION = PAGE)
```

```
CREATE TABLE dbo.Series (
    SeriesKey                           BIGINT                     NOT NULL, --PK
    PartitionId                         VARCHAR(36)                NOT NULL, --PK
    StudyKey                            BIGINT                     NOT NULL, --FK
    SeriesInstanceUid                   VARCHAR(64)                NOT NULL,
    Modality                            NVARCHAR(16)               NULL,
    PerformedProcedureStepStartDate     DATE                       NULL,
    ManufacturerModelName               NVARCHAR(64)               NULL
) WITH (DATA_COMPRESSION = PAGE)
```

```
CREATE TABLE dbo.Instance (
    InstanceKey             BIGINT                     NOT NULL, --PK
    PartitionId             VARCHAR(36)                NOT NULL, --PK
    SeriesKey               BIGINT                     NOT NULL, --FK
    -- StudyKey needed to join directly from Study table to find a instance
    StudyKey                BIGINT                     NOT NULL, --FK
    --instance keys used in WADO
    StudyInstanceUid        VARCHAR(64)                NOT NULL,
    SeriesInstanceUid       VARCHAR(64)                NOT NULL,
    SopInstanceUid          VARCHAR(64)                NOT NULL,
    --data consitency columns
    Watermark               BIGINT                     NOT NULL,
    Status                  TINYINT                    NOT NULL,
    LastStatusUpdatedDate   DATETIME2(7)               NOT NULL,
    --audit columns
    CreatedDate             DATETIME2(7)               NOT NULL
) WITH (DATA_COMPRESSION = PAGE)
```

```
CREATE TABLE dbo.DeletedInstance
(
    StudyInstanceUid    VARCHAR(64)       NOT NULL,
    PartitionId         VARCHAR(36)       NOT NULL,
    SeriesInstanceUid   VARCHAR(64)       NOT NULL,
    SopInstanceUid      VARCHAR(64)       NOT NULL,
    Watermark           BIGINT            NOT NULL,
    DeletedDateTime     DATETIMEOFFSET(0) NOT NULL,
    RetryCount          INT               NOT NULL,
    CleanupAfter        DATETIMEOFFSET(0) NOT NULL
) WITH (DATA_COMPRESSION = PAGE)
```
```
CREATE TABLE dbo.ChangeFeed (
    Sequence                BIGINT IDENTITY(1,1) NOT NULL,
    PartitionId             VARCHAR(36)          NOT NULL,
    Timestamp               DATETIMEOFFSET(7)    NOT NULL,
    Action                  TINYINT              NOT NULL,
    StudyInstanceUid        VARCHAR(64)          NOT NULL,
    SeriesInstanceUid       VARCHAR(64)          NOT NULL,
    SopInstanceUid          VARCHAR(64)          NOT NULL,
    OriginalWatermark       BIGINT               NOT NULL,
    CurrentWatermark        BIGINT               NULL
) WITH (DATA_COMPRESSION = PAGE)
```

- All the corresponding indexes should be updated. 
- Update all the stored procedures that are related to retrieving studies, series or instances.

## Migration

## Cross partition queries

## Error Response

## PaaS Roll out

## Roll out strategy


# Test Strategy

- Add and update existing unit tests, integration test and e2e tests to use partitionId
- Test for backward compatibility. Perform WADO operation after enabling the feature flag
- Ensure all the tests are not broken

# Security

The security boundary of the DICOM service is not changed.

# Other

*Describe any impact to privacy, localization, globalization, deployment, back-compat, SOPs, ISMS, etc.*


# Question
1. Can Zeiss once on-boarded to partitioning, can they go back?
2. How will partitions be reflected in blob storage?
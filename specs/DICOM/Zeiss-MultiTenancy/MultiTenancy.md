Implement a light weight data partition scheme that will enable customers to store multiple copies of the same image with the same UUID on a single DICOM instance.

[[_TOC_]]

# Business Justification

Zeiss is 8000 practices and growing. The operational overhead of maintaining separate DICOM instances for each practice is too high. From their perspective, multi-tenancy is a single instance of DICOM service that can horizontally scale to support any number of practices.

**Their key requirement is to allow resources to be cloned to allow data sharing across practices, while maintaining existing UUIDs.** This means that within the same DICOM service, there could be multiple DICOM instances sharing one combination of study/series/instance identifiers.
In order to achieve that we need a data partition solution.

# Scenarios

In all URIs below, there is an implicit base URI, then the optional version and [data partition segments](#data-partition). 

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

### Errors
 - Invalid partition id (272)
 - Resource already exists within partition (45070)

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

### Errors
 - 400: bad partition id

## QIDO
Users can search studies, series and instances across all partitions. See [cross-partition query discussion](#cross-partition-queries).

**Request**
```
GET {API_VERSION}/studies?...
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

### Errors
 - 400: bad partition id

## DELETE 
User can delete studies, series and instances within a partition. If partition is unspecified, delete will be perform against the default partition.

**Request**
```
DELETE {API_VERSION}/{PARTITION_KEY}/studies/{studyUid}
```

### Errors
 - 400: bad partition id

# Metrics

Add PartitionId as a dimension to current metrics. Whenever STOW, WADO, QIDO and DELETE operation are requested with partitionId, we should emit a metric so that we can know usage of this feature.

# Design

## Explored Approaches

| Option | Notes | Pros ✔ | Cons ❌ |
| ------ | ----- | ---- | ---- |
| [Add `tenant` to data model](add-tenant-id.md) | - Tenants created only on STOW operations<br><br>- Tenants deleted in background when they contain no records<br><br>- Need to list tenants | - Consistent with DICOM standard<br><br>- Allows simple client logic<br><br> - Serves as a potential basis to add tenant related default tags / nested tenancy |- Does not solve cross tenant query problems<br><br> - Additional complexity related to background jobs |
| [Model tenancy with external metadata](external-metadata.md) | - All tenancy information stored in external tags | - Similar solution to FHIR<br><br> - Can be used to extend DICOM service to support tag morphing: a feature supported in VNAs. | - Difficult to guarantee uniqueness |

## Data Partition

There is a middle way between these two options, which is to partition data via a unique id, maintaining data uniqueness as in the tenancy approach while not requiring the overhead of managing the `tenant` concept. [Initial draft](data-partition.md)

It seems best to indicate the data partition as an optional URI segment, after the optional version segment. Here's why:

| Option | Pros ✔ | Cons ❌ |
| ------ | ------ | ------   |
| Body   | | Requires parsing entire body; Zeiss doesn't want |
| Header | | FHIR deep links will break |
| Query Parameter | | May break OSS viewers |
| URI Path Segment | Closer to DICOM standard | |

This allows us to maintain a consistent approach across all APIs. It also allows us to enable the feature with minimal interruption to existing services, as the partition segment is optional. 

## Partition Id

We will be introducing a optional partition id in all operations. This id will either be a unique string composed of unreserved and safe characters, or a GUID. In either case, the id will be created by the client.

If the partition id is not given, then a default value will be used. If we choose strings, the default value will be `Microsoft.Default`. If we choose GUIDs, the default value will be `Guid.Empty` (`00000000-0000-0000-0000-000000000000`). 

## SQL Data Model Updates
- Add a new column to the below tables. It will be a non-nullable column with a default value based on the decision above. The approach below assumes GUID. 
  - Max length of the partition id will be 36 characters (GUID with hyphens)

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
    PartitionId             VARCHAR(36)          NULL,
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

## Blob Storage Updates

The format of the image & metadata blobs refers to instance, study, and series. This virtual path will be optionally prefixed with the partition id, and will not be present for the default tenant. This will prevent migrate existing blobs.

## Migration

We'll create a new schema version and diff.

## Cross partition queries

Two approaches:

1. If `PartitionId` is not specified, search only the default partition. To search all partitions, specify `PartitionId` of `all`.

2. If `PartitionId` is not specified, search all partitions.

For the first iteration, we will take approach 2, with the understanding that the QIDO functionality will reamin unchanged, but will return a result set with records across potentially multiple partitions, so clients will need to parse the record URIs. In future iterations, we can allow specifying partition(s) to search once we understand the usage patterns.

## Roll-out Strategy

We need to understand how the schema update is set in the Kubernetes deployment.

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
2. Should partition ids be a [GUID or string?](#partition-id)
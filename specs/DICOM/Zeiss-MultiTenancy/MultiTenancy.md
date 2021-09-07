Implement a light-weight data partition scheme that will enable customers to store multiple copies of the same image with the same identifying study/series/instance UIDs on a single DICOM service.

# Business Justification

Zeiss has asked us to provide a solution for the following requirements:
 1. Addressable storage for multiple organizational units should be implemented in one DICOM service
 2. Duplicate study/series/instance UIDs may exist across different organizational units (but not within one unit)
 3. Querying across organizational units is not required for this iteration
 4. No CRUD operations are required for the entity representing the organizational unit
 5. The feature is understood to be irreversible once enabled

 The first requirement is due to the scale of the solution Zeiss is building. They have a service topology that will involve multiple DICOM services, but they will support thousands of organizational units, so the operational overhead of one DICOM service per organizational unit is too high.

 This only becomes complex when paired with the second requirement, since one DICOM service should be able to handle the scale as long as each organizational unit stores unique images. Currently, our DICOM service enforces uniqueness by the combination of study, series, and instance UIDs. Even this is less strict than [the DICOM standard,](http://dicom.nema.org/dicom/2013/output/chtml/part05/chapter_9.html) which specifies that UIDs should be unique "across the DICOM universe of discourse irrespective of any semantic context" - so instance id alone would be uniquely identifying. At first glance, allowing duplicates would seem to violate the DICOM standard.

There is, however, an important distinction to be made. The responsibility for creating valid UIDs in on the _producer_ of the image, not on the storage class. While _creating_ duplicate UIDs violates the standard, _allowing_ duplicates is a matter of leniency. As a storage class, the DICOM service has no way to validate any UID for uniqueness.

Granting that we _can_ allow duplicates, why _should_ we? The answer is that in practice, duplicate DICOM objects have existed for decades. It's common practice for files to be written to portable storage media by a healthcare provider and given to the patient, who then gives the files to another healthcare provider, who then transfers the files into a DICOM storage system. Thus, multiple copies of one DICOM file commonly exist in isolated DICOM systems. As this functionality is moving to the cloud, we face a difficult problem: how can we ensure the **global uniqueness** of DICOM objects in an ever-more interconnected cloud ecosystem, while also providing an on-ramp for **existing data stores and workflows?**

## Explored Approaches

### Can we simply ensure that duplicate files are modified to have unique UIDs? 
No, mainly because of Zeiss' appraisal of their end customer expectations. Zeiss doesn't want to present the end customer with files that have been altered in any way. Importantly, it doesn't matter whether this modification is performed by Zeiss, or in the DICOM service. 

_Note: the DICOM standard [does allow for coercing values during the import process,](http://dicom.nema.org/medical/dicom/current/output/html/part18.html#sect_10.5.2) and storing the original values as metadata._

### Can we store the organizational unit information in external metadata?
We can, but then it's difficult to guarantee uniqueness, especially when performing WADO requests. [(initial exploration)](OtherOptions/external-metadata.md)

### Should we create a full `tenant` concept for the user to manage?
We can, but we introduce complexity by increasing the API surface and adding background jobs to handle tenant lifecycle operations. [(initial exploration)](OtherOptions/add-tenant-id.md)

### Should we use a lighter version of the `tenant` concept?
Yes - we'll call this lighter version a `data partition`.

## Data Partition
We propose partitioning data via a unique id, maintaining object uniqueness as in the tenancy approach while not requiring the overhead of managing tenant lifecycle. The proposal is to implement the smallest version of the feature that fulfills Zeiss requirements, and to consider a more robust approach to multitenancy as we discover the market demand. [(initial investigation)](data-partition.md) 

### Partition Id

We will be introducing a optional partition id in all operations. The partition id will be:
 - unique within the scope of the DICOM service
 - a string of 1 to 32 alphanumeric characters
 - specified by the client

The default value of this id will be `Guid.Empty`: `00000000000000000000000000000000`, and all existing data at time of feature enablement will be backfilled with the default value. When this feature is enabled by the user, **partition id will be required as input for STOW, WADO, QIDO, and delete.** It will not be required for extended query tag operations.

It seems best to indicate the data partition as a required URI segment, after the optional version segment. Here's why:

| Option | Pros ✔ | Cons ❌ |
| ------ | ------ | ------   |
| Body   | | Requires parsing the entire body; Zeiss doesn't want to do this, not visible in default logging |
| Header | | FHIR deep links will break, not visible in default logging |
| Query Parameter | | May break OSS viewers |
| URI Path Segment | Closer to DICOM standard | Breaking change to APIs |

# Scenarios

In all URIs below, there is an implicit base URI, then the optional version and [data partition segments](#data-partition). 

## STOW
Users must specify a partition id when storing studies, series and instances.

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

**Errors**
Existing error behavior will remain the same, with the following clarification: if no partition id or an invalid partition id is included in the URI, a 400 status code will be returned, with a dataset including the reason code `272`.

## WADO
Users must specify a partition id when retrieving studies, series and instances.

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

**Errors**
Existing error behavior will remain the same, with the following clarification: if no partition id or an invalid partition id is included in the URI, a 400 status code will be returned.

## QIDO
Users must specify a partition id as the scope for searching studies, series and instances. See [cross-partition query discussion](#cross-partition-queries).

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

**Errors**
Existing error behavior will remain the same, with the following clarification: if no partition id or an invalid partition id is included in the URI, a 400 status code will be returned.

## DELETE 
User must specify a partition id to delete studies, series and instances.

**Request**
```
DELETE {API_VERSION}/{PARTITION_KEY}/studies/{studyUid}
```

**Errors**
Existing error behavior will remain the same, with the following clarification: if no partition id or an invalid partition id is included in the URI, a 400 status code will be returned.

## Extended Query Tags
These endpoints will remain unchanged; any extended query tag operation will be performed at the scope of the server (all partitions).

# Metrics
Add PartitionId as a dimension to current metrics. Whenever STOW, WADO, QIDO and DELETE operation are requested with partitionId, we should emit a metric so that we can know usage of this feature.

# Design

## SQL Data Model Updates
- Add a new column to the below tables.

```
CREATE TABLE dbo.Study (
    StudyKey                    BIGINT                            NOT NULL, --PK
    StudyInstanceUid            VARCHAR(64)                       NOT NULL,
    PartitionId                 VARCHAR(32)                       NOT NULL DEFAULT '00000000000000000000000000000000',
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
    StudyKey                            BIGINT                     NOT NULL, --FK
    SeriesInstanceUid                   VARCHAR(64)                NOT NULL,
    PartitionId                         VARCHAR(32)                NOT NULL DEFAULT '00000000000000000000000000000000',
    Modality                            NVARCHAR(16)               NULL,
    PerformedProcedureStepStartDate     DATE                       NULL,
    ManufacturerModelName               NVARCHAR(64)               NULL
) WITH (DATA_COMPRESSION = PAGE)
```

```
CREATE TABLE dbo.Instance (
    InstanceKey             BIGINT                     NOT NULL, --PK
    SeriesKey               BIGINT                     NOT NULL, --FK
    -- StudyKey needed to join directly from Study table to find a instance
    StudyKey                BIGINT                     NOT NULL, --FK
    --instance keys used in WADO
    StudyInstanceUid        VARCHAR(64)                NOT NULL,
    SeriesInstanceUid       VARCHAR(64)                NOT NULL,
    SopInstanceUid          VARCHAR(64)                NOT NULL,
    PartitionId             VARCHAR(32)                NOT NULL DEFAULT '00000000000000000000000000000000',
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
    PartitionId         VARCHAR(32)       NOT NULL DEFAULT '00000000000000000000000000000000',
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
    PartitionId             VARCHAR(32)          NULL DEFAULT '00000000000000000000000000000000',
    Timestamp               DATETIMEOFFSET(7)    NOT NULL,
    Action                  TINYINT              NOT NULL,
    StudyInstanceUid        VARCHAR(64)          NOT NULL,
    SeriesInstanceUid       VARCHAR(64)          NOT NULL,
    SopInstanceUid          VARCHAR(64)          NOT NULL,
    OriginalWatermark       BIGINT               NOT NULL,
    CurrentWatermark        BIGINT               NULL
) WITH (DATA_COMPRESSION = PAGE)
```

```
CREATE TABLE dbo.DeletedInstance
(
    StudyInstanceUid    VARCHAR(64)       NOT NULL,
    SeriesInstanceUid   VARCHAR(64)       NOT NULL,
    SopInstanceUid      VARCHAR(64)       NOT NULL,
    Watermark           BIGINT            NOT NULL,
    PartitionId         VARCHAR(32)       NOT NULL  DEFAULT '00000000000000000000000000000000',
    DeletedDateTime     DATETIMEOFFSET(0) NOT NULL,
    RetryCount          INT               NOT NULL,
    CleanupAfter        DATETIMEOFFSET(0) NOT NULL
) WITH (DATA_COMPRESSION = PAGE)
```

```
CREATE TABLE dbo.ChangeFeed (
    Sequence                BIGINT IDENTITY(1,1) NOT NULL,
    Timestamp               DATETIMEOFFSET(7)    NOT NULL,
    Action                  TINYINT              NOT NULL,
    StudyInstanceUid        VARCHAR(64)          NOT NULL,
    SeriesInstanceUid       VARCHAR(64)          NOT NULL,
    SopInstanceUid          VARCHAR(64)          NOT NULL,
    OriginalWatermark       BIGINT               NOT NULL,
    CurrentWatermark        BIGINT               NULL,
    PartitionId             VARCHAR(32)          NOT NULL  DEFAULT '00000000000000000000000000000000'
) WITH (DATA_COMPRESSION = PAGE)
```

- All the corresponding indexes will be updated. PartitionId will be added to UNIQUE CLUSTERED INDEX 
- Update all the stored procedures that are related to retrieving studies, series or instances.

## Blob Storage Updates

The format of the image & metadata blobs refers to instance, study, and series. This virtual path will be prefixed with the partition id. While using the existing watermark would allow us to differentiate between objects, including the partition id as part of the blob name will allow the service to be restored from blob, which was identified as an existing design goal.

## Migration

We'll create a new schema version and diff. Add the `PartitionId` as the composite primary key and fill the default value as a single transaction.

Include `PartitionId` in all the indexes.

As part of the migration script, we will update all the rows to default PartitionId `where PartitionId is NULL`.

## Cross partition queries

Two approaches:

1. If `PartitionId` is not specified, return a 400 status code.

2. If `PartitionId` is not specified, search all partitions.

For the first iteration, we will take approach 1, with the understanding that the QIDO functionality will remain unchanged within the specified partition. In future iterations, we can allow querying across partitions once we understand the requirements and usage patterns.

## Roll-out Strategy

Zeiss has requested that this feature be enabled programatically, not via manual process (IcM). Changing the base URI to include a required partition id is a breaking API change, so we will need to consider the best way to expose this feature while minimizing the complications related to API versions, documentation, and Azure Portal experience. 

Options include:
- making partition id optional to maintain back-compatibility
- creating a new API version exposed via feature flag (how would this converge with future versions?)
- updating RP to check for feature flag and set config at deploy time
- updating RP (or workspace platform?) to query storage for feature status and set config at deploy time

# Test Strategy

- Add and update existing unit tests, integration test and e2e tests to use partitionId
- Test for backward compatibility. Perform WADO operation after enabling the feature flag
- Ensure all the tests are not broken

# Security

The security boundary of the DICOM service is not changed.

# Other

*Describe any impact to privacy, localization, globalization, deployment, back-compat, SOPs, ISMS, etc.*
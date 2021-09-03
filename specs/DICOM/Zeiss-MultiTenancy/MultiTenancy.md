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
We can, but then it's difficult to guarantee uniqueness, especially when performing WADO requests. [(initial exploration)](external-metadata.md)

### Should we create a full `tenant` concept for the user to manage?
We can, but we introduce complexity by increasing the API surface and adding background jobs to handle tenant lifecycle operations. [(initial exploration)](add-tenant-id.md)

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

It seems best to indicate the data partition as an optional URI segment, after the optional version segment. Here's why:

| Option | Pros ✔ | Cons ❌ |
| ------ | ------ | ------   |
| Body   | | Requires parsing the entire body; Zeiss doesn't want to do this, not visible in default logging |
| Header | | FHIR deep links will break, not visible in default logging |
| Query Parameter | | May break OSS viewers |
| URI Path Segment | Closer to DICOM standard | Breaking change to APIs |



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

We'll create a new schema version and diff. Add the `PartitionId` as the composite primary key and fill the default value.

Include `PartitionId` in all the indexes.

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
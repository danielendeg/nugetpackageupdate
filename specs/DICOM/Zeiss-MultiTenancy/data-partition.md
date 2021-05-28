 # Zeiss multi-tenancy through data partition 

 Below is the exploration of implementing multi-tenancy using data partition concept.

 ## Summary of the solution
 - Opt-in to a data partition solution.
- System tag `microsoft.paritionid`. Accessible in DICOMWeb Apis like a external tag.
- UID uniqueness within the partition is enforced. Partitionid is required in STOW, Delete and WADO. Optional in QIDO.

 ## Enable this feature
```json
{
    EnableDataPartition: "true"
}
```

 ## DICOMWeb APIs

 ```cli
##STOW, can also be a header if easier.
##REQUEST
POST /studies
{
    content-type: application/json
    [
        {
            "key": "microsoft.partitionId",
            "value": "clinic1"
        }
    ]
    ...
    content-type: application/dicom
}



## WADO
GET /studies/{studyUid}?microsoft.partitionId=clinic1


## QIDO
GET /studies?microsoft.partitionId=clinic1
GET /studies

## Delete
DELETE /studies/{studyUid}?microsoft.partitionId=clinic1
```
- Partition value is a string with max length of 32 chars.
- PartitionId behaves like study level tag in QIDO.
- Get with multiple instance match and accept of single part will throw an exception.
- QIDO will return all matching instances.
- ExtendedQueryTag resources remain unchanged.
- Expose paritionId as private tag in QIDO and ChangeFeed if this feature is enabled.
- How do we expose this feature to customers in PaaS? Can be a especial one off for Zeiss.

## Implementation
- Add a new column `PartitionId` to
  - Instance
  - Study
  - Series
  - ChangeFeed

- Change indexes to include `PartitionId`
- Most of the stored procedures need to be changed to include `PartitionId`
- If the feature is not enabled the value will be defaulted to say `microsoft.default`, this will make all the SQL statements static regardless of if the feature is turned on/off.

 ## Pros and Cons


- ✔️ External metadata concept can be used to extend DICOM service to support tag morphing a feature supported in VNAs.
- ✔️ Allows future scaling through partitionId sharding.
- ❌ Face of a tag, body of a tenant


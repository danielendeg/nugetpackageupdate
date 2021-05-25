 # Zeiss multi-tenancy through simple external metadata support

 Below is the exploration of implementing multi-tenancy using external-metadata concept.
 This is a weakly typed solution, since it does not support any tenant specific features.

 ## Summary of the solution

 - Addition property per DICOM instance that contains tenant information, using external tag concept.
 - DICOMWeb standard supported way to interact with this property transactionally.
 - Support duplicate DICOM UIDs as an opt in feature. 

 ## DICOMWeb APIs with external metadata

 ```cli
##STOW
x-health-label: "practice": "1234"
POST /studies

## WADO
x-health-label: "practice": "1234"
GET /studies/{studyUid}
GET /studies/{studyUid}/series/{seriesUid}
GET /studies/{studyUid}/series/{seriesUid}/instances/{sopInstanceUid}

## QIDO
GET /studies?x-health-label.practice=1234

## Delete
x-health-label: "practice": "1234"
DELETE /studies/{studyUid}
DELETE /studies/{studyUid}/series/{seriesUid}
DELETE /studies/{studyUid}/series/{seriesUid}/instances/{sopInstanceUid}
```

```json
{
    allowDuplicateUids: true
}
```
- Single string value per label in V1.
- Unique UID check is removed in STOW.
- Get with multiple instance match and accept of single part will throw the right exception.
- Delete and QIDO will just delete or match all specified UIDs.

- ExtendedQueryTag resources remain unchanged.
- ChangeFeed resource remains unchanged.
- We would support CRUD on label resource in V2


 ## Pros and Cons

- ✔️ Similar solution to FHIR. Though FHIR as it a little easier with system generated unique identifier.
- ✔️ External metadata concept can be used to extend DICOM service to support tag morphing a feature supported in VNAs.
- ❌ External metadata is not enforced to being required or unique in combination with DICOM UIDs. This may result in 2 DICOM instances with same UIDs and tenant label and not being able to uniquely identify them. Managing the uniqueness becomes a client problem.
- ❌ Urls no longer uniquely represent a DICOM instance. So external reference with instance URLS may fail, if it cannot be uniquely resolved.

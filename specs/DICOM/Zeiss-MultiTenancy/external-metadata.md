 # Zeiss multi-tenancy through external metadata support

 Below is the exploration of implementing multi-tenancy using external-metadata concept.
 This is a weakly typed solution, since it does not support any tenant specific features.

 ## Summary of the solution

 - `Microsoft generated id`: Microsoft id per instance, that supports duplicate DICOM UID's. Opt-in? Standard feature?
- `External metadata`: Addition property per DICOM instance that contains tenant information, using external tag concept. DICOMWeb standard supported way to interact with this property.
 
 ## Microsoft generated id
```json
{
    EnableMicrosoftId: true
}
```

```json
## Private tag that represents ms generated id, need to have private creator element also
{
    tagId: 33330001
    tagName: ms-health-id
    VR: CS
}

```

- Unique UID check is removed in STOW.
- All returned DICOM metadata (WADO metadata, QIDO and ChangeFeed) will include microsoft id. We will not return this in get DICOM, since this cannot be part of dicom file. Is this too odd?

 ## External metadata with DICOMWeb and Microsoft id

 ```cli
##STOW
##REQUEST
POST /studies
{
    content-type: application/ms-health-label+json
    [
        {
            "key": "practice",
            "value": "1234"
        }
    ]
    ...
    content-type: application/dicom
}

##RESPONSE
{
{
  "33330001":
  {
    "vr":"CS",
    "Value":"1"
  },
}
...
}


## WADO
GET /studies/{studyUid}?ms-health-id=1
GET /studies/{studyUid}/series/{seriesUid}?33330001=1234
GET /studies/{studyUid}/series/{seriesUid}/instances/{sopInstanceUid}?ms-health-id=1234

## QIDO
GET /studies?ms-health-label.practice=1234
{
{
  "33330001":
  {
    "vr":"CS",
    "Value":"1"
  },
}
...
}

## Delete
DELETE /studies/{studyUid}?ms-health-id=1234
DELETE /studies/{studyUid}/series/{seriesUid}?ms-health-id=1234
DELETE /studies/{studyUid}/series/{seriesUid}/instances/{sopInstanceUid}?ms-health-id=1234
```


- Single string value per label in V1.
- Labels behave like study level tags in QIDO.
- Get with multiple instance match and accept of single part will throw an exception.
- Delete will just delete all specified UIDs.
- QIDO will return all matching instances.

- ExtendedQueryTag resources remain unchanged.
- We would support CRUD on label in a separate resource in V2.


 ## Pros and Cons

- ✔️ Similar solution to FHIR. Though FHIR as it a little easier with system generated unique identifier.
- ✔️ External metadata concept can be used to extend DICOM service to support tag morphing a feature supported in VNAs.
- ❌ External metadata is not enforced to being required or unique in combination with DICOM UIDs. This may result in 2 DICOM instances with same UIDs and tenant label.

